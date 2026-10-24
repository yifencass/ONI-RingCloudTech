using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Klei.AI;
using Klei.AI.DiseaseGrowthRules;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 灭菌规则：
    ///   1) 盐水 / 浓盐水本身杀灭其内部的一切病菌（含食物中毒、粘液肺、僵尸孢子）
    ///   2) 铜 / 铜矿建造的液体、气体管道会杀灭管内运输物质上的病菌
    ///   3) 铜 / 铜矿建造的容器（储液库、储气库等）会杀灭储存物上的病菌
    /// 所有衰减对「任何病菌」统一生效，因此三种病菌都被覆盖。
    /// </summary>
    public static class GermSanitizer
    {
        /// <summary>游戏暂停判定：SpeedControlScreen 的暂停计数 + timeScale 双保险。</summary>
        public static bool IsGamePaused()
        {
            if (Time.timeScale == 0f) return true;
            SpeedControlScreen screen = SpeedControlScreen.Instance;
            return screen != null && screen.IsPaused;
        }

        /// 盐水/浓盐水：每秒杀灭 10%（对数值下降）
        public const float SALT_KILL_PER_SEC = 0.10f;

        /// 铜管道：每 0.2s 杀灭 50%
        public const float COPPER_PIPE_KILL = 0.50f;

        /// 铜容器：每 0.5s 杀灭 35%
        public const float COPPER_STORAGE_KILL = 0.35f;

        public static bool IsCopperTag(Tag tag)
        {
            return tag == SimHashes.Copper.CreateTag()
                || tag == SimHashes.Cuprite.CreateTag();
        }

        /// <summary>建筑是否由铜 / 铜矿建造。</summary>
        public static bool IsCopperBuilding(GameObject go)
        {
            if (go == null) return false;

            PrimaryElement pe = go.GetComponent<PrimaryElement>();
            if (pe != null && pe.Element != null && IsCopperTag(pe.Element.tag))
                return true;

            Deconstructable dec = go.GetComponent<Deconstructable>();
            if (dec != null && dec.constructionElements != null)
            {
                for (int i = 0; i < dec.constructionElements.Length; i++)
                    if (IsCopperTag(dec.constructionElements[i])) return true;
            }
            return false;
        }

        public static int Decay(int count, float ratio)
        {
            int delta = Mathf.Max(1, (int)(count * ratio));
            int next = count - delta;
            return next < 0 ? 0 : next;
        }

        // Grid.DiseaseCount / DiseaseIdx 的索引器只读，直接拿底层指针写
        private static unsafe int* diseaseCountPtr;
        private static unsafe byte* diseaseIdxPtr;
        private static bool pointersResolved;

        private static unsafe bool ResolvePointers()
        {
            if (pointersResolved) return diseaseCountPtr != null;
            try
            {
                var fCount = AccessTools.Field(typeof(Grid), "diseaseCount");
                var fIdx = AccessTools.Field(typeof(Grid), "diseaseIdx");
                if (fCount != null) diseaseCountPtr = (int*)Pointer.Unbox(fCount.GetValue(null));
                if (fIdx != null) diseaseIdxPtr = (byte*)Pointer.Unbox(fIdx.GetValue(null));
                pointersResolved = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 病菌网格指针解析失败: " + e.Message);
                pointersResolved = true;
            }
            return diseaseCountPtr != null;
        }

        /// <summary>
        /// 用 ONI 自己的疾病模型标记「盐水 / 浓盐水是灭菌物」：
        /// 每种 Disease 都有一张 ElemGrowthInfo[]（按元素索引），
        /// 元素规则直接决定 ConduitDiseaseManager 等托管侧的病菌增减。
        /// </summary>
        private static bool elementRulesApplied;

        public static void ApplySaltWaterElementRules()
        {
            if (elementRulesApplied) return;
            if (Db.Get() == null || ElementLoader.elements == null) return;

            Element saltWater = ElementLoader.FindElementByHash(SimHashes.SaltWater);
            Element brine = ElementLoader.FindElementByHash(SimHashes.Brine);
            Element copper = ElementLoader.FindElementByHash(SimHashes.Copper);
            Element cuprite = ElementLoader.FindElementByHash(SimHashes.Cuprite);
            if (saltWater == null && brine == null && copper == null && cuprite == null) return;

            object diseaseSet = Db.Get().Diseases;
            int patched = 0;
            if (diseaseSet != null)
            {

                foreach (FieldInfo field in diseaseSet.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!typeof(Disease).IsAssignableFrom(field.FieldType)) continue;
                    Disease disease = field.GetValue(diseaseSet) as Disease;
                    if (disease == null) continue;

                    if (disease.growthRules == null) disease.growthRules = new List<GrowthRule>();
                    AddDisinfectantRule(disease, saltWater, disease.elemGrowthInfo);
                    AddDisinfectantRule(disease, brine, disease.elemGrowthInfo);
                    AddDisinfectantRule(disease, copper, disease.elemGrowthInfo);
                    AddDisinfectantRule(disease, cuprite, disease.elemGrowthInfo);
                    patched++;
                }
            }
            Debug.Log("[RingCloudTech] 盐水/浓盐水/铜/铜矿已登记为灭菌物，共 " + patched + " 种疾病");

            elementRulesApplied = true;

        }

        private static bool scanDisabled;

        /// <summary>
        /// 把某个元素加成「灭菌物」——就是氯气在游戏里的写法：
        /// 往疾病的 growthRules 里塞一条 TagGrowthRule，再用 rule.Apply 写进 elemGrowthInfo。
        /// 注意不能调 PopulateElemGrowthInfo：子类（FoodGerms 等）会在里面 AddGrowthRule，
        /// 重复调用会触发 "Subsequent rules should not be base rules" 断言。
        /// 数据库里的灭菌物列表读的也是这张 growthRules。
        /// </summary>
        private static void AddDisinfectantRule(Disease disease, Element element, ElemGrowthInfo[] target)
        {
            if (element == null) return;

            GrowthRule rule = new TagGrowthRule(element.tag)
            {
                underPopulationDeathRate = 50000f,   // 低于最低浓度时的线性杀灭速度
                populationHalfLife = 0.2f,           // 常规浓度：0.2 秒半衰
                overPopulationHalfLife = 0.2f,       // 超浓度：同样 0.2 秒
                diffusionScale = 0f,                 // 不向外扩散
                minCountPerKG = 0f,
                maxCountPerKG = 1e9f,
                minDiffusionCount = int.MaxValue,
                minDiffusionInfestationTickCount = byte.MaxValue
            };

            disease.growthRules.Add(rule);       // 数据库读这张表
            if (target != null) rule.Apply(target);   // 直接烘焙进本轮数据，不重跑 PopulateElemGrowthInfo
        }

        /// <summary>登记为灭菌物的四种元素：盐水、浓盐水、铜、铜矿。</summary>
        private static bool IsDisinfectantElement(Element element)
        {
            if (element == null) return false;
            SimHashes id = element.id;
            return id == SimHashes.SaltWater
                || id == SimHashes.Brine
                || id == SimHashes.Copper
                || id == SimHashes.Cuprite;
        }

        /// <summary>扫描全图，衰减盐水 / 浓盐水 / 铜 / 铜矿格子里的病菌。</summary>
        public static unsafe void SanitizeSaltWaterCells()
        {
            if (scanDisabled) return;

            try
            {
                if (!ResolvePointers()) { scanDisabled = true; return; }

                Element[] elements = Grid.Element;
                if (elements == null || elements.Length <= 0) return;

                int cellCount = Grid.CellCount;
                if (cellCount <= 0) return;
                if (cellCount > elements.Length) cellCount = elements.Length;

                for (int cell = 0; cell < cellCount; cell++)
                {
                    Element element = elements[cell];
                    if (!IsDisinfectantElement(element)) continue;

                    int count = diseaseCountPtr[cell];
                    if (count <= 0) continue;

                    int next = Decay(count, SALT_KILL_PER_SEC);
                    diseaseCountPtr[cell] = next;
                    if (next <= 0 && diseaseIdxPtr != null) diseaseIdxPtr[cell] = byte.MaxValue;
                }
            }
            catch (System.Exception e)
            {
                scanDisabled = true;
                Debug.LogWarning("[RingCloudTech] 盐水格子扫描已停用: " + e);
            }
        }
    }

    /// <summary>数据库生成前先把盐水标记成灭菌物，否则条目里看不到。</summary>
    [HarmonyPatch(typeof(CodexCache), "CodexCacheInit")]
    public static class Patch_CodexCache_ForSanitizer
    {
        public static void Prefix()
        {
            try { GermSanitizer.ApplySaltWaterElementRules(); }
            catch (System.Exception e) { Debug.LogWarning("[RingCloudTech] 提前应用盐水规则失败: " + e.Message); }
        }
    }

    /// <summary>
    /// 借 ConduitDiseaseManager.Sim200ms（每 200ms 必调一次）当计时器，
    /// 每 5 次 = 1 秒执行一次全图盐水灭菌扫描。
    /// </summary>
    [HarmonyPatch(typeof(ConduitDiseaseManager), "Sim200ms")]
    public static class Patch_ConduitDiseaseManager_Sim200ms
    {
        private static int frame;

        public static void Postfix()
        {
            if (++frame < 5) return;
            frame = 0;
            GermSanitizer.ApplySaltWaterElementRules();
            GermSanitizer.SanitizeSaltWaterCells();
        }
    }

    /// <summary>铜管道：杀灭管内流动物质上的病菌。</summary>
    [HarmonyPatch(typeof(Conduit), "OnSpawn")]
    public static class Patch_Conduit_OnSpawn
    {
        public static void Postfix(Conduit __instance)
        {
            if (__instance == null) return;
            GameObject go = __instance.gameObject;
            if (!GermSanitizer.IsCopperBuilding(go)) return;
            if (go.GetComponent<CopperConduitSanitizer>() != null) return;
            go.AddComponent<CopperConduitSanitizer>();
        }
    }

    public class CopperConduitSanitizer : KMonoBehaviour
    {
        private Conduit conduit;
        private float acc;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            conduit = GetComponent<Conduit>();
        }

        private void Update()
        {
            if (conduit == null || Game.Instance == null) return;

            if (GermSanitizer.IsGamePaused()) return;   // 暂停时不消毒
            acc += Time.deltaTime;               // 受 timeScale 影响，暂停时自然为 0
            if (acc < 0.2f) return;
            acc = 0f;

            ConduitFlow flow =
                conduit.type == ConduitType.Liquid ? Game.Instance.liquidConduitFlow :
                conduit.type == ConduitType.Gas ? Game.Instance.gasConduitFlow : null;
            if (flow == null) return;

            int cell = Grid.PosToCell(this);
            ConduitFlow.ConduitContents contents = flow.GetContents(cell);
            if (contents.diseaseCount <= 0) return;

            contents.diseaseCount = GermSanitizer.Decay(contents.diseaseCount, GermSanitizer.COPPER_PIPE_KILL);
            if (contents.diseaseCount <= 0) contents.diseaseIdx = byte.MaxValue;
            flow.SetContents(cell, contents);
        }
    }

    /// <summary>铜容器：杀灭储存物上的病菌。</summary>
    [HarmonyPatch(typeof(Storage), "OnSpawn")]
    public static class Patch_Storage_OnSpawn
    {
        public static void Postfix(Storage __instance)
        {
            if (__instance == null) return;
            GameObject go = __instance.gameObject;
            if (!GermSanitizer.IsCopperBuilding(go)) return;
            if (go.GetComponent<CopperStorageSanitizer>() != null) return;
            go.AddComponent<CopperStorageSanitizer>();
        }
    }

    public class CopperStorageSanitizer : KMonoBehaviour
    {
        private Storage storage;
        private float acc;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            storage = GetComponent<Storage>();
        }

        private void Update()
        {
            if (storage == null || storage.items == null) return;

            if (GermSanitizer.IsGamePaused()) return;   // 暂停时不消毒
            acc += Time.deltaTime;
            if (acc < 0.5f) return;
            acc = 0f;

            for (int i = 0; i < storage.items.Count; i++)
            {
                GameObject item = storage.items[i];
                if (item == null) continue;

                PrimaryElement pe = item.GetComponent<PrimaryElement>();
                if (pe == null || pe.DiseaseCount <= 0) continue;

                int count = pe.DiseaseCount;
                int next = GermSanitizer.Decay(count, GermSanitizer.COPPER_STORAGE_KILL);
                pe.ModifyDiseaseCount(next - count, "RingCloudTech.CopperStorage");
            }
        }
    }
}
