using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KMod;
using Klei.AI;
using STRINGS;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>模组入口。</summary>
    public class RingCloudTechMod : UserMod2
    {
        public const string EFFECT_ID = "DuplicantDrankCola";
        public const string COLA_TAG = "Cola";
        public const string BLACKCOFFEE_TAG = "BlackCoffee";
        public const string BLACKCOFFEE_EFFECT_ID = "DuplicantDrankBlackCoffee";

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            // 鸿蒙之眼「产品滞留」状态条目文案。
            // 用 (key, 文本) 重载：单参 LocString 重载只填一种语言的槽位，
            // 会在 StringTable.Add 里越界（历史崩溃）。
            try
            {
                Strings.Add("STRINGS.BUILDING.STATUSITEMS.HARMONYEYE_PENDING.NAME", "产品滞留");
                Strings.Add("STRINGS.BUILDING.STATUSITEMS.HARMONYEYE_PENDING.TOOLTIP",
                    "频道空间不足，产物暂存在建筑内部。接入更多存储单元或清空频道后会自动继续输出。");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 注册状态条本文案失败: " + e);
            }

            try
            {
                ModUtil.AddBuildingToPlanScreen("Food", BeverageMixerConfig.ID);
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 注册建造菜单失败: " + e);
            }

            try
            {
                // 放进电力 → 电池子类
                ModUtil.AddBuildingToPlanScreen("Power", SuperCapacitorConfig.ID, "batteries");
                Db_Initialize_Patch.RegisterSubcategorySorting(SuperCapacitorConfig.ID, "batteries");

                // 鸿蒙之眼：软依赖 ALN。没有 ALN 就没有氢源也没有产物出口，
                // 建出来也只是个不工作的空壳，索性不进建造菜单。
                if (HarmonyEyeAln.Installed)
                {
                    ModUtil.AddBuildingToPlanScreen("Base", HarmonyEyeConfig.ID, "misc");
                }
                else
                {
                    Debug.Log("[RingCloudTech] 未检测到 ALN，鸿蒙之眼不注册到建造菜单");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 注册超级电容库到建造菜单失败: " + e);
            }

            try
            {
                // 时间之弓：800 W 氦气发电机，惰性产物中子质从右侧 O 格排出
                ModUtil.AddBuildingToPlanScreen("Power", TimeBowConfig.ID, "generators");

                // AddBuildingToPlanScreen 只决定「进哪个子类」，建造菜单给按钮分组的
                // 子类归属还得补一张 PLANSUBCATEGORYSORTING 表，否则游戏会刷
                // "has not been added to plan screen subcategory organization" 警告，
                // 按钮也会落到 uncategorized 分组里。
                Db_Initialize_Patch.RegisterSubcategorySorting(TimeBowConfig.ID, "generators");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 注册时间之弓到建造菜单失败: " + e);
            }

            try
            {
                // 智能液冷机：4×3，冷却到目标温度。放到 公用 → 温度 子类（原版液冷机同款）
                ModUtil.AddBuildingToPlanScreen("Utilities", SmartChillerConfig.ID, "temperature");
                Db_Initialize_Patch.RegisterSubcategorySorting(SmartChillerConfig.ID, "temperature");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 注册智能液冷机到建造菜单失败: " + e);
            }

            try
            {
                // 放进基地 → 站台子类
                // [实验性] 暂时停用研究台：ModUtil.AddBuildingToPlanScreen("Base", BiologyResearchCenterConfig.ID, "stations");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 注册生物学研究台到建造菜单失败: " + e);
            }

            // 暂时移除中子质管道：注册清空，下面的循环不会执行
            string[] conduits = new string[0];
            foreach (string conduit in conduits)
            {
                try
                {
                    ModUtil.AddBuildingToPlanScreen("Base", conduit, "pipes");
                }
                catch (Exception)
                {
                    try
                    {
                        ModUtil.AddBuildingToPlanScreen("Base", conduit);
                    }
                    catch (Exception e2)
                    {
                        Debug.LogError("[RingCloudTech] 注册管道失败: " + conduit + " / " + e2);
                    }
                }
            }



            Debug.Log("[RingCloudTech] 环云科技已加载");
        }
    }

    /// <summary>
    /// 给可乐补 Substance：渲染材质 + 液滴动画。
    ///
    /// 时序：Assets.CreatePrefabs 里先跑 SubstanceListHookup，再跑 CreatePrefabs
    /// （后者会调 LegacyModMain.Load → LoadOre → GeneratedOre.LoadGeneratedOre）。
    /// 自定义元素没有 SubstanceTable 条目，substance 是兜底 new Substance()，
    /// material 和 anim 都为空，导致液体渲染异常、也造不出液滴 prefab。
    /// </summary>
    [HarmonyPatch(typeof(Assets), "SubstanceListHookup")]
    public static class Assets_SubstanceListHookup_Patch
    {
        public static void Postfix()
        {
            try
            {
                Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
                if (cola == null || cola.substance == null)
                {
                    Debug.LogWarning("[RingCloudTech] 未找到 Cola 元素");
                    return;
                }

                Substance source = Assets.instance?.substanceTable?.GetSubstance(SimHashes.Water);
                if (source == null)
                {
                    Debug.LogError("[RingCloudTech] 找不到水的 Substance 作为模板");
                    return;
                }

                if (source.material != null)
                {
                    cola.substance.material = new Material(source.material) { name = "matCola" };
                }
                cola.substance.anim = source.anim;

                Debug.Log("[RingCloudTech] 已配置可乐 Substance");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 配置 Substance 失败: " + e);
            }
        }
    }

    /// <summary>
    /// 后备：若 SubstanceListHookup 没能补上 anim，这里再补一次，
    /// 保证 LoadGeneratedOre 能给可乐生成液滴 prefab。
    /// </summary>
    [HarmonyPatch(typeof(GeneratedOre), nameof(GeneratedOre.LoadGeneratedOre))]
    public static class GeneratedOre_LoadGeneratedOre_Patch
    {
        public static void Prefix()
        {
            try
            {
                Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
                if (cola?.substance == null || cola.substance.anim != null)
                {
                    return;
                }

                Element water = ElementLoader.FindElementByHash(SimHashes.Water);
                if (water?.substance?.anim != null)
                {
                    cola.substance.anim = water.substance.anim;
                    Debug.Log("[RingCloudTech] 后备补齐可乐液滴动画");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 后备补动画失败: " + e);
            }
        }

        /// <summary>
        /// 时间之弓的固体产物是「中子质（Unobtanium）」。
        ///
        /// 原版 LoadGeneratedOre 只在两种情况下给元素注册可落地物件 prefab：
        ///   1) 该元素有 IOreConfig 实现；或
        ///   2) element.substance.anim 非空（走 EntityTemplates.CreateSolidOreEntity）。
        /// 中子质两者都不满足（它只作地图边界/遗迹砖出现），于是 Assets.GetPrefab("Unobtanium")
        /// 为空，Substance.SpawnResource 会在 KInstantiate(null) 处抛异常。
        ///
        /// 所以在原版跑完之后补一次：缺了就照原版兜底分支的写法造一个固体物件。
        /// 必须在此刻（LegacyModMain.Load 的 LoadOre 阶段）做 —— 这时
        /// EntityTemplates.CreateBaseOreTemplates() 已就绪、DestroyBaseOreTemplates() 还没调。
        /// </summary>
        public static void Postfix()
        {
            try
            {
                EnsureUnobtaniumOrePrefab();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 中子质物件兜底注册失败: " + e);
            }
        }

        /// <summary>退用动画的参照元素：铁矿石，原版必然有自己的固体 chunk 美术。</summary>
        private static readonly SimHashes UNOBTANIUM_ANIM_FALLBACK = SimHashes.IronOre;

        private static void EnsureUnobtaniumOrePrefab()
        {
            Element element = ElementLoader.FindElementByHash(SimHashes.Unobtanium);
            if (element == null)
            {
                Debug.LogWarning("[RingCloudTech] 找不到 Unobtanium（中子质）元素，跳过物件兜底注册");
                return;
            }

            // 原版（或其它 mod）已经注册过就什么都不做
            if (Assets.TryGetPrefab(element.tag) != null)
            {
                return;
            }

            GameObject go = EntityTemplates.CreateSolidOreEntity(SimHashes.Unobtanium, null);
            if (go == null)
            {
                Debug.LogWarning("[RingCloudTech] Unobtanium 物件创建失败");
                return;
            }

            // 美术兜底：substance 没有 anim 时 CreateOreEntity 会把 null 塞进
            // KBatchedAnimController.AnimFiles，实例化会出错；换成铁矿石的 chunk 动画并染色。
            KBatchedAnimController anim = go.GetComponent<KBatchedAnimController>();
            if (anim != null)
            {
                bool hasAnim = anim.AnimFiles != null && anim.AnimFiles.Length > 0 && anim.AnimFiles[0] != null;
                if (!hasAnim)
                {
                    Element fallback = ElementLoader.FindElementByHash(UNOBTANIUM_ANIM_FALLBACK);
                    KAnimFile fallbackAnim = fallback != null && fallback.substance != null ? fallback.substance.anim : null;
                    if (fallbackAnim != null)
                    {
                        anim.AnimFiles = new KAnimFile[1] { fallbackAnim };
                    }
                    else
                    {
                        Debug.LogWarning("[RingCloudTech] 找不到可退用的固体 chunk 动画，中子质物件可能不可见");
                    }
                }

                // 染色：中子质的 substance 可能是 ElementLoader 兜底 new 出来的空 Substance，
                // 它的 colour 是默认 (0,0,0,0)（alpha=0 会让物件直接看不见）。
                // 空色时退回铁矿石的颜色，再不行给一个中性灰。
                Color32 tint = element.substance != null ? element.substance.colour : default(Color32);
                if (tint.a == 0)
                {
                    Element tintSource = ElementLoader.FindElementByHash(UNOBTANIUM_ANIM_FALLBACK);
                    tint = tintSource != null && tintSource.substance != null ? tintSource.substance.colour : default(Color32);
                    if (tint.a == 0)
                    {
                        tint = new Color32(150, 150, 160, 255);
                    }
                }
                anim.TintColour = tint;
            }

            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            Debug.Log("[RingCloudTech] 已为 Unobtanium（中子质）注册可落地固体物件 prefab，时间之弓的固体产物可正常生成");
        }
    }

    /// <summary>
    /// Db.Initialize 之后：注册效果、修正元素文本、解锁科技。
    /// 此时元素已加载完毕（LoadOre 已跑过）。
    /// </summary>
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class Db_Initialize_Patch
    {
        public static void Postfix()
        {
            try { RegisterColaEffect(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 注册效果失败: " + e); }

            try { RegisterBlackCoffeeEffect(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 注册黑咖啡效果失败: " + e); }

            try { CustomizeColaElement(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 修正元素失败: " + e); }

            try { CustomizeBlackCoffeeElement(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 修正黑咖啡元素失败: " + e); }



            try { UnlockBeverageMixer(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 解锁建筑失败: " + e); }

            try { UnlockSuperCapacitor(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 解锁超级电容库失败: " + e); }

            try { UnlockSmartChiller(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 解锁智能液冷机失败: " + e); }

            // 时间之弓 / 鸿蒙之眼不再挂在原版科技上，改为自建科技节点（无源能源 / 口袋宇宙）
            try { CustomTechs.Install(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 自建科技失败: " + e); }

            try { RepairMissingSubstanceAnims(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 修复缺失液滴动画失败: " + e); }
        }

        // Substance.anims 是 internal KAnimFile[]，由 SubstanceTable.BindAnimList()
        // 从 Substance.anim 自动填充一次。
        // 但「加载之后再设置 anim」的元素（比如我们的 Cola，以及其它 mod 用 YAML 加的元素）
        // 会漏掉这一步，anims 保持 null。
        // 后果：空瓶器 BottleEmptier.StartMeter() 里 SwapAnims(substance.anims) 直接 NRE。
        // 这里在 Db.Initialize 之后统一补一遍。
        private static readonly FieldInfo SubstanceAnimsField =
            typeof(Substance).GetField("anims", BindingFlags.Instance | BindingFlags.NonPublic);

        private static KAnimFile[] GetSubstanceAnims(Substance s)
        {
            return SubstanceAnimsField?.GetValue(s) as KAnimFile[];
        }

        private static void SetSubstanceAnims(Substance s, KAnimFile[] value)
        {
            SubstanceAnimsField?.SetValue(s, value);
        }

        private static void RepairMissingSubstanceAnims()
        {
            if (SubstanceAnimsField == null)
            {
                Debug.LogWarning("[RingCloudTech] 找不到 Substance.anims 字段，跳过修复");
                return;
            }

            // 同状态的模板元素（原版一定有液滴动画）。
            // 这里只是「借一份能用的动画数组」，不针对任何具体元素或 mod。
            Element gasDonor = ElementLoader.FindElementByHash(SimHashes.Hydrogen);
            Element liquidDonor = ElementLoader.FindElementByHash(SimHashes.Water);

            int fixedWithOwn = 0, fixedWithTemplate = 0, stillBroken = 0;

            foreach (Element el in ElementLoader.elements)
            {
                Substance sub = el?.substance;
                if (sub == null) continue;

                // 只处理流体。固体的液滴动画本来就可能为空，空瓶器也不会碰它。
                if (!el.IsGas && !el.IsLiquid) continue;

                // 已经正常的元素一律不动
                if (GetSubstanceAnims(sub) != null) continue;

                // 情况 1：自己就有动画，只是没被 BindAnimList 绑上
                //         （典型：加载之后才设置 anim 的元素）
                if (sub.anim != null)
                {
                    SetSubstanceAnims(sub, new KAnimFile[] { sub.anim });
                    fixedWithOwn++;
                    continue;
                }

                // 情况 2：连 anim 都没有
                //         （典型：用 YAML 加元素、没写 substance 动画）
                //         借同状态原版元素的液滴动画
                Element donor = el.IsGas ? gasDonor : liquidDonor;
                KAnimFile[] template = GetSubstanceAnims(donor?.substance);
                if (template == null)
                {
                    stillBroken++;
                    continue;
                }

                sub.anim = donor.substance.anim;
                SetSubstanceAnims(sub, template);
                fixedWithTemplate++;
            }

            if (fixedWithOwn + fixedWithTemplate + stillBroken > 0)
            {
                Debug.Log(string.Format(
                    "[RingCloudTech] 液滴动画补齐: 自有动画 {0} 个, 借用模板 {1} 个, 仍缺失 {2} 个",
                    fixedWithOwn, fixedWithTemplate, stillBroken));
            }
        }

        private static void CustomizeColaElement()
        {
            Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
            if (cola == null)
            {
                Debug.LogWarning("[RingCloudTech] 未找到 Cola 元素");
                return;
            }

            // .po 覆盖的是 LocString 树，元素自身的 name/description
            // 是加载 YAML 时按 localizationID 取的，这里显式赋值。
            cola.name = STRINGS.ELEMENTS.COLA.NAME;
            cola.description = STRINGS.ELEMENTS.COLA.DESC;

            if (cola.substance != null)
            {
                Color32 deep = new Color32(38, 20, 10, 255);
                cola.substance.colour = deep;
                cola.substance.uiColour = deep;
                cola.substance.conduitColour = new Color32(70, 40, 20, 255);
            }
        }

        /// <summary>黑咖啡元素的显示名、描述与颜色。</summary>
        private static void CustomizeBlackCoffeeElement()
        {
            Element bc = ElementLoader.FindElementByName(RingCloudTechMod.BLACKCOFFEE_TAG);
            if (bc == null)
            {
                Debug.LogWarning("[RingCloudTech] 未找到 BlackCoffee 元素");
                return;
            }

            bc.name = STRINGS.ELEMENTS.BLACKCOFFEE.NAME;
            bc.description = STRINGS.ELEMENTS.BLACKCOFFEE.DESC;

            if (bc.substance != null)
            {
                Color32 deep = new Color32(32, 20, 12, 255);
                bc.substance.colour = deep;
                bc.substance.uiColour = deep;
                bc.substance.conduitColour = new Color32(58, 38, 24, 255);
            }
        }

        /// <summary>
        /// 给自定义液体元素补一个「液滴 prefab」。
        ///
        /// 为什么必须补：原版 GeneratedOre.RegisterOreElements 只在
        ///     element.substance != null && element.substance.anim != null
        /// 时才 EntityTemplates.CreateLiquidOreEntity + Assets.AddPrefab；
        /// 而 yaml 注册的 mod 元素在 SubstanceTable 里查不到，ElementLoader
        /// 只会塞一个 new Substance()（anim / material 全为 null），于是永远没有 prefab。
        /// 没有 prefab 的后果：Storage.AddLiquid 里 GeneratedOre.CreateChunk 会
        /// KInstantiate(null) 抛 NRE；ALN 之类按 Assets.GetPrefab 判品类的第三方逻辑
        /// 会把它当成固体。
        ///
        /// 约定：新增自定义液/气体元素时，必须在 Db.Initialize 后调用本方法一次。
        /// </summary>
        internal static void EnsureLiquidElementPrefab(string elementId)
        {
            Element element = ElementLoader.FindElementByName(elementId);
            if (element == null)
            {
                Debug.LogWarning("[RingCloudTech] prefab 注册跳过：找不到元素 " + elementId);
                return;
            }

            if (Assets.GetPrefab(element.tag) != null)
            {
                Debug.Log("[RingCloudTech] " + elementId + " 已有 prefab，跳过");
                return;
            }

            // 借用原版水的 substance 视觉资源（material / kanim）
            Element water = ElementLoader.GetElement(SimHashes.Water.CreateTag());
            if (water == null || water.substance == null || water.substance.anim == null)
            {
                Debug.LogWarning("[RingCloudTech] prefab 注册跳过：找不到水的 substance");
                return;
            }
            if (element.substance == null)
            {
                Debug.LogWarning("[RingCloudTech] prefab 注册跳过：" + elementId + " 没有 substance");
                return;
            }

            element.substance.anim = water.substance.anim;
            element.substance.material = water.substance.material;

            GameObject go = EntityTemplates.CreateLiquidOreEntity(element.id);
            if (go == null)
            {
                Debug.LogWarning("[RingCloudTech] prefab 注册跳过：CreateLiquidOreEntity 返回 null");
                return;
            }

            KBatchedAnimController kbac = go.GetComponent<KBatchedAnimController>();
            if (kbac != null)
            {
                kbac.TintColour = element.substance.colour;
            }

            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            Debug.Log("[RingCloudTech] 已注册 " + elementId + " 的液滴 prefab");
        }

        /// <summary>黑咖啡的饮用效果：+100 体力/周期，+15 压力/周期，+4 运动。</summary>
        private static void RegisterBlackCoffeeEffect()
        {
            if (Db.Get().effects.Exists(RingCloudTechMod.BLACKCOFFEE_EFFECT_ID))
            {
                return;
            }

            Effect effect = new Effect(
                id: RingCloudTechMod.BLACKCOFFEE_EFFECT_ID,
                name: "黑咖啡",
                description: "浓烈的黑咖啡：整夜不困，但也让人心跳加速、心神不宁。",
                duration: 600f,
                show_in_ui: true,
                trigger_floating_text: true,
                is_bad: false);

            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Stamina.deltaAttribute.Id, 100f / 600f, "黑咖啡：体力"));
            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Stress.deltaAttribute.Id, 15f / 600f, "黑咖啡：压力"));
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.Athletics.Id, 4f, "黑咖啡：运动"));

            Db.Get().effects.Add(effect);
            Debug.Log("[RingCloudTech] 已注册效果 " + RingCloudTechMod.BLACKCOFFEE_EFFECT_ID);
        }

        private static void RegisterColaEffect()
        {
            // 用 Exists 而非 Get：Get 查不到会 LogError，其堆栈会让
            // SearchForModsInStackTrace 把所有 patch 了 Db.Initialize 的 mod 标记为可疑。
            if (Db.Get().effects.Exists(RingCloudTechMod.EFFECT_ID))
            {
                return;
            }

            Effect effect = new Effect(
                id: RingCloudTechMod.EFFECT_ID,
                name: STRINGS.DUPLICANTS.MODIFIERS.DUPLICANTDRANKCOLA.NAME,
                description: STRINGS.DUPLICANTS.MODIFIERS.DUPLICANTDRANKCOLA.TOOLTIP,
                duration: 600f,                 // 1 个周期
                show_in_ui: true,
                trigger_floating_text: true,
                is_bad: false);

            // deltaAttribute 单位是「每秒」，1 周期 = 600 秒。
            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Stress.deltaAttribute.Id, -20f / 600f, "可乐：压力"));
            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Stamina.deltaAttribute.Id, 20f / 600f, "可乐：体力"));
            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Calories.deltaAttribute.Id, 500f * 1000f / 600f, "可乐：卡路里"));

            // 即时属性：士气 / 运动
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.QualityOfLife.Id, 3f, "可乐：士气"));
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.Athletics.Id, 4f, "可乐：运动"));

            Db.Get().effects.Add(effect);
            Debug.Log("[RingCloudTech] 已注册效果 " + RingCloudTechMod.EFFECT_ID);
        }

        private static void UnlockBeverageMixer()
        {
            Tech tech = Db.Get().Techs.Get("FoodRepurposing");
            if (tech == null)
            {
                Debug.LogWarning("[RingCloudTech] 找不到 FoodRepurposing 科技");
                return;
            }
            if (!tech.unlockedItemIDs.Contains(BeverageMixerConfig.ID))
            {
                tech.AddUnlockedItemIDs(BeverageMixerConfig.ID);
            }
        }

        private static void UnlockSuperCapacitor()
        {
            // 大容量储能，放在电力规整的进阶科技
            Tech tech = Db.Get().Techs.Get("AdvancedPowerRegulation");
            if (tech == null)
            {
                Debug.LogWarning("[RingCloudTech] 找不到 AdvancedPowerRegulation 科技");
                return;
            }
            AddUnlock(tech, SuperCapacitorConfig.ID);
            // 鸿蒙之眼已改挂自建科技「口袋宇宙」，这里不再重复解锁。
        }

        [Obsolete("时间之弓改为自建科技「无源能源」，见 CustomTechs.Install()")]
        private static void UnlockTimeBow()
        {
            CustomTechs.Install();
        }

        /// <summary>智能液冷机挂在原版「液体温度」科技下（和原版液冷机同一格）。</summary>
        private static void UnlockSmartChiller()
        {
            Tech tech = Db.Get().Techs.Get("LiquidTemperature");
            if (tech == null)
            {
                Debug.LogWarning("[RingCloudTech] 找不到 LiquidTemperature 科技");
                return;
            }
            AddUnlock(tech, SmartChillerConfig.ID);
        }

        private static void AddUnlock(Tech tech, string id)
        {
            if (!tech.unlockedItemIDs.Contains(id))
            {
                tech.AddUnlockedItemIDs(id);
            }
        }

        /// <summary>
        /// 建造菜单按钮的分组归属：ModUtil.AddBuildingToPlanScreen 只负责把建筑塞进某个子类列表，
        /// PlanBuildingToggle 生成按钮时还会查 TUNING.BUILDINGS.PLANSUBCATEGORYSORTING，
        /// 查不到就退化成 uncategorized 并刷警告。
        /// </summary>
        internal static void RegisterSubcategorySorting(string buildingId, string subcategory)
        {
            try
            {
                Dictionary<string, string> table = TUNING.BUILDINGS.PLANSUBCATEGORYSORTING;
                if (table != null)
                {
                    table[buildingId] = subcategory;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 注册建造菜单子类归属失败 " + buildingId + ": " + e.Message);
            }
        }
    }

    /// <summary>
    /// 自定义元素的 prefab 必须在这里补：
    /// Assets.CreatePrefabs 的顺序是  Db.Get()  →  AddPrefab(PrefabAssets)  →  LegacyModMain.Load()  →  Db.PostProcess()。
    /// 而 EntityTemplates.baseOreTemplate 是在 LegacyModMain.Load() 里才由
    /// EntityTemplates.CreateBaseOreTemplates() 建出来的，
    /// 所以放在 Db.Initialize 的 Postfix 里调用 CreateLiquidOreEntity 会
    /// Object.Instantiate(null) 抛 ArgumentException。
    /// </summary>
    [HarmonyPatch(typeof(Assets), "CreatePrefabs")]
    public static class Assets_CreatePrefabs_Patch
    {
        public static void Postfix()
        {
            try { Db_Initialize_Patch.EnsureLiquidElementPrefab(RingCloudTechMod.COLA_TAG); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 注册可乐 prefab 失败: " + e); }

            try { Db_Initialize_Patch.EnsureLiquidElementPrefab(RingCloudTechMod.BLACKCOFFEE_TAG); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 注册黑咖啡 prefab 失败: " + e); }
        }
    }


    /// <summary>
    /// 让可乐的 Tag 显示成「可乐」而非「Cola」。
    /// TagManager.ProperNames 只收录原版 Tag，FillMissingProperNames 也不会新增 key，
    /// 所以自定义元素查询时会回退到 tag.Name。
    /// </summary>
    [HarmonyPatch(typeof(TagManager), nameof(TagManager.GetProperName))]
    public static class TagManager_GetProperName_Patch
    {
        public static bool Prefix(Tag tag, bool stripLink, ref string __result)
        {
            if (tag.Name == RingCloudTechMod.COLA_TAG)
            {
                __result = STRINGS.ELEMENTS.COLA.NAME;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// ComplexRecipe.GetUIIcon 里 Assets.GetPrefab(tag).GetComponent 不判空，
    /// 元素产物没有 prefab 就会 NRE。改走 Def.GetUISprite 的元素分支。
    /// </summary>
    [HarmonyPatch(typeof(ComplexRecipe), nameof(ComplexRecipe.GetUIIcon))]
    public static class ComplexRecipe_GetUIIcon_Patch
    {
        public static bool Prefix(ComplexRecipe __instance, ref Sprite __result)
        {
            try
            {
                if (__instance.results == null || __instance.results.Length == 0)
                {
                    __result = null;
                    return false;
                }

                Tag tag = (__instance.nameDisplay == ComplexRecipe.RecipeNameDisplay.Ingredient
                           && __instance.ingredients != null
                           && __instance.ingredients.Length > 0)
                    ? __instance.ingredients[0].material
                    : __instance.results[0].material;

                if (__instance.nameDisplay == ComplexRecipe.RecipeNameDisplay.Custom
                    && !string.IsNullOrEmpty(__instance.customSpritePrefabID))
                {
                    tag = __instance.customSpritePrefabID;
                }

                if (Assets.GetPrefab(tag) != null)
                {
                    return true;
                }

                __result = Def.GetUISprite((object)tag, "ui", false).first;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] GetUIIcon 保护失败: " + e);
                __result = null;
                return false;
            }
        }
    }

    /// <summary>
    /// SelectedRecipeQueueScreen.GetIngredientDescription 里
    /// prefab.GetProperName() 不判空，元素原料会 NRE。
    /// 同时原版只统计世界库存，材料搬进机器后就显示缺料 —— 这里合并机器内部库存。
    /// </summary>
    [HarmonyPatch(typeof(SelectedRecipeQueueScreen), "GetIngredientDescription")]
    public static class SelectedRecipeQueueScreen_GetIngredientDescription_Patch
    {
        public static bool Prefix(ComplexRecipe.RecipeElement ingredient,
                                  out bool hasEnoughMaterial,
                                  ref string __result,
                                  SelectedRecipeQueueScreen __instance)
        {
            hasEnoughMaterial = false;
            try
            {
                string amountText = GameUtil.GetFormattedByTag(ingredient.material, ingredient.amount);

                float available = 0f;
                ComplexFabricator target = Traverse.Create(__instance)
                    .Field("target").GetValue<ComplexFabricator>();
                if (target != null)
                {
                    if (target.GetMyWorld() != null)
                    {
                        available = target.GetMyWorld().worldInventory
                            .GetAmount(ingredient.material, includeRelatedWorlds: true);
                    }
                    if (target.inStorage != null)
                    {
                        available += target.inStorage.GetAmountAvailable(ingredient.material);
                    }
                }

                hasEnoughMaterial = available >= ingredient.amount;
                string availText = GameUtil.GetFormattedByTag(ingredient.material, available);

                string text = GameUtil.SafeStringFormat(
                    UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_REQUIREMENT,
                    ingredient.material.ProperName(),
                    amountText) + "\n";

                string avail = GameUtil.SafeStringFormat(
                    UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_AVAILABLE, availText);

                __result = hasEnoughMaterial
                    ? text + "<size=12>" + avail + "</size>"
                    : text + "<size=12><color=#E68280>" + avail + "</color></size>";

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] GetIngredientDescription 保护失败: " + e);
                __result = ingredient.material.ProperName();
                return false;
            }
        }
    }

    /// <summary>
    /// Codex 的 CodexRecipePanel.ConfigureComplexRecipe 对元素原料会 NRE
    /// （多处 Assets.GetPrefab(...).GetComponent 不判空）。
    /// 检测到非 prefab 的 tag 就跳过图标渲染，只写标题。
    /// </summary>
    [HarmonyPatch(typeof(CodexRecipePanel), "ConfigureComplexRecipe")]
    public static class CodexRecipePanel_ConfigureComplexRecipe_Patch
    {
        public static bool Prefix(CodexRecipePanel __instance)
        {
            try
            {
                ComplexRecipe recipe = Traverse.Create(__instance)
                    .Field("complexRecipe").GetValue<ComplexRecipe>();
                if (recipe == null)
                {
                    return true;
                }

                bool hasNonPrefab = false;
                if (recipe.ingredients != null)
                {
                    foreach (ComplexRecipe.RecipeElement ing in recipe.ingredients)
                    {
                        if (Assets.GetPrefab(ing.material) == null) { hasNonPrefab = true; break; }
                    }
                }
                if (!hasNonPrefab && recipe.results != null)
                {
                    foreach (ComplexRecipe.RecipeElement res in recipe.results)
                    {
                        if (Assets.GetPrefab(res.material) == null) { hasNonPrefab = true; break; }
                    }
                }

                if (!hasNonPrefab)
                {
                    return true;
                }

                object titleObj = Traverse.Create(__instance).Field("title").GetValue();
                if (titleObj != null && recipe.results != null && recipe.results.Length > 0)
                {
                    var textProp = titleObj.GetType().GetProperty("text");
                    textProp?.SetValue(titleObj, recipe.results[0].material.ProperName());
                }

                Debug.Log("[RingCloudTech] 跳过元素配方在 Codex 里的图标渲染");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] Codex 配方保护失败: " + e);
                return false;
            }
        }
    }

    /// <summary>
    /// Def.GetUISprite(Tag, string) 无判空：Assets.GetPrefab(prefabID).GetComponent
    /// 对元素 Tag 直接 NRE。prefab 不存在时改走 object 重载（会先查 ElementLoader）。
    /// </summary>
    [HarmonyPatch(typeof(Def), nameof(Def.GetUISprite), new Type[] { typeof(Tag), typeof(string) })]
    public static class Def_GetUISprite_Patch
    {
        public static bool Prefix(Tag prefabID, string facadeID, ref Tuple<Sprite, Color> __result)
        {
            if (Assets.GetPrefab(prefabID) != null)
            {
                return true;
            }

            __result = Def.GetUISprite((object)prefabID, "ui", false);
            return false;
        }
    }

    /// <summary>把可乐加进饮水机的可选饮品。</summary>
    [HarmonyPatch(typeof(WaterCoolerConfig), "CreateBuildingDef")]
    public static class WaterCoolerConfig_CreateBuildingDef_Patch
    {
        public static void Postfix()
        {
            try
            {
                List<Tuple<Tag, string>> options =
                    new List<Tuple<Tag, string>>(WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS);

                AddOption(options, ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG),
                          RingCloudTechMod.EFFECT_ID);
                AddOption(options, ElementLoader.FindElementByName(RingCloudTechMod.BLACKCOFFEE_TAG),
                          RingCloudTechMod.BLACKCOFFEE_EFFECT_ID);

                WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS = options.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 扩展饮水机失败: " + e);
            }
        }

        /// <summary>元素存在且尚未登记时，追加一条饮品选项。</summary>
        private static void AddOption(List<Tuple<Tag, string>> options, Element element, string effectId)
        {
            if (element == null)
            {
                Debug.LogWarning("[RingCloudTech] 饮品元素不存在，跳过");
                return;
            }

            foreach (var o in options)
            {
                if (o.first == element.tag)
                {
                    return;
                }
            }

            options.Add(new Tuple<Tag, string>(element.tag, effectId));
            Debug.Log("[RingCloudTech] 饮水机新增饮品 " + element.tag);
        }
    }

    /// <summary>
    /// 饮水机选项的 tooltip：原版取 OPTION_TOOLTIPS.&lt;TAG&gt;，自定义液体没有这个 key。
    /// 这里重写可乐那一条，首行用 UI.FormatAsLink 包出标题色。
    /// </summary>
    [HarmonyPatch(typeof(WaterCooler), "GetOptions")]
    public static class WaterCooler_GetOptions_Patch
    {
        public static void Postfix(ref FewOptionSideScreen.IFewOptionSideScreen.Option[] __result)
        {
            try
            {
                if (__result == null)
                {
                    return;
                }

                FixOption(__result, RingCloudTechMod.COLA_TAG, STRINGS.ELEMENTS.COLA.NAME,
                          STRINGS.ELEMENTS.COLA.DESC, RingCloudTechMod.EFFECT_ID, "COLA");

                FixOption(__result, RingCloudTechMod.BLACKCOFFEE_TAG, STRINGS.ELEMENTS.BLACKCOFFEE.NAME,
                          STRINGS.ELEMENTS.BLACKCOFFEE.DESC, RingCloudTechMod.BLACKCOFFEE_EFFECT_ID,
                          "BLACKCOFFEE");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 修正饮水机文本失败: " + e);
            }
        }

        /// <summary>重写某一条饮品选项的标题与 tooltip。</summary>
        private static void FixOption(
            FewOptionSideScreen.IFewOptionSideScreen.Option[] result,
            string elementId,
            LocString fallbackName,
            LocString description,
            string effectId,
            string linkId)
        {
            Element element = ElementLoader.FindElementByName(elementId);
            if (element == null)
            {
                return;
            }

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i].tag != element.tag)
                {
                    continue;
                }

                result[i].labelText =
                    string.IsNullOrEmpty(element.name) ? fallbackName : element.name;

                string tip = UI.FormatAsLink(result[i].labelText, linkId)
                           + "\n" + description;

                Effect effect = Db.Get().effects.Get(effectId);
                if (effect != null)
                {
                    tip += "\n\n" + Effect.CreateTooltip(effect, showDuration: true);
                }
                result[i].tooltipText = tip;
                return;
            }
        }
    }

    /// <summary>
    /// 让鸿蒙之眼的 inStorage 成为「被动储气罐」。
    ///
    /// ComplexFabricator 有两处会把气体吐出来 / 招来小人：
    ///   1. DropExcessIngredients：没有排队配方时，把不属于当前配方原料的东西全部 Drop。
    ///      氢气不属于任何「正在排队」的配方原料时就被倒回世界。
    ///   2. UpdateFetches：原料不足时创建 FetchList，派小人手动送料。
    /// 对鸿蒙之眼这两个行为都跳过：氢气只从管道进，配方直接吃箱子里的存量。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "DropExcessIngredients")]
    public static class ComplexFabricator_DropExcessIngredients_Patch
    {
        public static bool Prefix(ComplexFabricator __instance)
        {
            return !HarmonyEyeGuard.IsHarmonyEye(__instance);
        }
    }

    [HarmonyPatch(typeof(ComplexFabricator), "UpdateFetches")]
    public static class ComplexFabricator_UpdateFetches_Patch
    {
        public static bool Prefix(ComplexFabricator __instance)
        {
            return !HarmonyEyeGuard.IsHarmonyEye(__instance);
        }
    }

    /// <summary>
    /// 跳过后必须同时清掉 materialNeedCache，否则 ComplexFabricator 每帧刷
    /// 「has material needs cached, but no open fetches」警告。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "UpdateMaterialNeeds")]
    public static class ComplexFabricator_UpdateMaterialNeeds_Patch
    {
        public static bool Prefix(ComplexFabricator __instance)
        {
            return !HarmonyEyeGuard.IsHarmonyEye(__instance);
        }
    }

    /// <summary>
    /// 鸿蒙之眼的产物输出。
    ///
    /// 原版 Melted 分支走 Storage.AddLiquid，而 AddLiquid 会
    /// LiquidSourceManager.CreateChunk → GeneratedOre.CreateChunk →
    /// Assets.GetPrefab(element.tag)。熔融金属没有液滴 prefab，于是
    /// KInstantiate(null) 抛 NullReferenceException。
    /// 这里接管：固体照旧进箱子，液体/气体交给 HarmonyEyeOutput 写进管道。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
    public static class ComplexFabricator_SpawnOrderProduct_Patch
    {
        public static bool Prefix(ComplexFabricator __instance, ComplexRecipe recipe)
        {
            if (!HarmonyEyeGuard.IsHarmonyEye(__instance))
            {
                return true;
            }

            try
            {
                Spawn(__instance, recipe);
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 鸿蒙之眼产物输出失败: " + e);
            }

            return false;
        }

        private static void Spawn(ComplexFabricator fab, ComplexRecipe recipe)
        {
            Storage build = fab.buildStorage;
            if (build == null)
            {
                return;
            }

            // 原版在这一步把原料从 buildStorage 里扣掉，接管后必须自己扣。
            foreach (ComplexRecipe.RecipeElement ingredient in recipe.ingredients)
            {
                build.ConsumeAndGetDisease(ingredient.material, ingredient.amount,
                    out float _consumed, out var _disease, out float _temperature);
            }

            // 产物不再生成实体块、也不写管道/箱子：统一进 HarmonyEyeOutput 队列，
            // 由它按 ALN 频道能接受的速度 DepositToChannel。
            // 顺带彻底避开 Melted 液体没有 prefab 导致的 KInstantiate 崩溃。
            // 组件在预制体里（HarmonyEyeConfig.ConfigureBuildingTemplate），
            // 这里只取；万一拿到 null 才补挂，保证旧存档也能跑。
            HarmonyEyeOutput buffer = fab.GetComponent<HarmonyEyeOutput>()
                                      ?? fab.gameObject.AddOrGet<HarmonyEyeOutput>();

            foreach (ComplexRecipe.RecipeElement result in recipe.results)
            {
                Element element = ElementLoader.GetElement(result.material);
                if (element == null)
                {
                    continue;
                }

                buffer.Enqueue(element, result.amount, element.defaultValues.temperature);
            }
        }
    }

    /// <summary>
    /// 拦掉鸿蒙之眼对「非氢气」的摄入。
    ///
    /// ConduitConsumer.Consume 对不匹配 capacityTag 的元素**无条件**触发
    /// BuildingHP.DamageSourceInfo { damage = 1, source = BAD_INPUT_ELEMENT }，
    /// 与 wrongElementResult 是 Store 还是 Dump 无关；同时错误气体一旦进箱就出不来。
    /// 所以这里在消费之前直接跳过：气体留在管道里形成背压，既不吞也不挨伤害。
    /// </summary>
    [HarmonyPatch(typeof(ConduitConsumer), "Consume")]
    public static class ConduitConsumer_Consume_Patch
    {
        public static bool Prefix(ConduitConsumer __instance, ConduitFlow conduit_mgr)
        {
            try
            {
                if (__instance == null || !HarmonyEyeGuard.IsHarmonyEye(__instance.gameObject))
                {
                    return true;
                }

                if (conduit_mgr == null)
                {
                    return true;
                }

                int cell = HarmonyEyeGuard.GetConsumerCell(__instance);
                if (cell < 0)
                {
                    return true;
                }

                ConduitFlow.ConduitContents contents = conduit_mgr.GetContents(cell);
                if (contents.mass <= 0f)
                {
                    return true;
                }

                Element element = ElementLoader.FindElementByHash(contents.element);
                if (element == null)
                {
                    return false;
                }

                // 只放行氢气，其余元素原样留在管道里。
                if (element.HasTag(ElementLoader.FindElementByHash(SimHashes.Hydrogen).tag))
                {
                    return true;
                }

                HarmonyEyeGuard.LogRejected(element);
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
    /// <summary>判断一个 ComplexFabricator 是不是鸿蒙之眼。</summary>
    public static class HarmonyEyeGuard
    {
        private static readonly System.Reflection.MethodInfo GetInputCellMethod =
            AccessTools.Method(typeof(ConduitConsumer), "GetInputCell");

        /// <summary>
        /// 取 ConduitConsumer 的输入格。直接调 protected virtual 的 GetInputCell(conduitType)，
        /// 比自己读私有字段 utilityCell 稳（字段名随版本改动就静默失效）。
        /// </summary>
        public static int GetConsumerCell(ConduitConsumer consumer)
        {
            try
            {
                if (consumer == null || GetInputCellMethod == null)
                {
                    return -1;
                }

                object cell = GetInputCellMethod.Invoke(consumer, new object[] { consumer.conduitType });
                return cell is int value ? value : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static readonly HashSet<Tag> LoggedRejects = new HashSet<Tag>();

        public static void LogRejected(Element element)
        {
            try
            {
                if (element == null)
                {
                    return;
                }

                if (LoggedRejects.Add(element.tag))
                {
                    Debug.Log("[RingCloudTech] 鸿蒙之眼拒绝非氢气输入: " + element.tag);
                }
            }
            catch (Exception)
            {
            }
        }

        public static bool IsHarmonyEye(GameObject go)
        {
            try
            {
                if (go == null)
                {
                    return false;
                }

                KPrefabID id = go.GetComponent<KPrefabID>();
                return id != null && id.PrefabTag == new Tag(HarmonyEyeConfig.ID);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsHarmonyEye(ComplexFabricator fabricator)
        {
            try
            {
                if (fabricator == null)
                {
                    return false;
                }

                KPrefabID id = fabricator.GetComponent<KPrefabID>();
                return id != null && id.PrefabTag == new Tag(HarmonyEyeConfig.ID);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 鸿蒙之眼的「真软依赖」：没装 ALN 就不注册这个建筑。
    ///
    /// 只跳过 ModUtil.AddBuildingToPlanScreen 是不够的 —— 那只是不进建造菜单，
    /// 而原版 LegacyModMain.LoadBuildings(types) 会把所有装配里实现了
    /// IBuildingConfig 的类型一次性交给 GeneratedBuildings.LoadGeneratedBuildings，
    /// BuildingDef 与 prefab 照样会被注册（调试菜单里还能刷出来）。
    ///
    /// 这里直接在入口把类型从列表里剔掉，等于这个建筑在这局游戏里根本不存在。
    /// 装回 ALN 后重启即可恢复，不影响存档。
    /// </summary>
    [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
    public static class GeneratedBuildings_LoadGeneratedBuildings_Patch
    {
        private static bool logged;

        public static void Prefix(ref List<Type> types)
        {
            if (types == null)
            {
                return;
            }

            // 生物学研究台：总开关关掉时同样要整座建筑消失。
            // 只要 BiologyResearchCenterConfig 还被注册，Research.GetMissingResearchBuildingName
            // 就会在 researchCenterPrefabs 里找到它、认为该科技「有对应研究站」，
            // 于是给玩家挂一条常驻的「缺少研究站」提示；研究侧屏再顺着 biology 成本键去
            // GetResearchType("biology").name，直接空引用崩溃。
            if (!BiologyResearch.ENABLED)
            {
                int biologyRemoved = types.RemoveAll(t => t == typeof(BiologyResearchCenterConfig));
                if (biologyRemoved > 0)
                {
                    Debug.Log("[RingCloudTech] 生物学研究已停用，已跳过生物学研究台的建筑注册（移除 " +
                              biologyRemoved + " 个配置类型）");
                }
            }

            if (HarmonyEyeAln.Installed)
            {
                return;
            }

            int removed = types.RemoveAll(t => t == typeof(HarmonyEyeConfig));
            if (removed > 0 && !logged)
            {
                logged = true;
                Debug.Log("[RingCloudTech] 未检测到 ALN，已跳过鸿蒙之眼的建筑注册（移除 " +
                          removed + " 个配置类型）");
            }
        }
    }

    /// <summary>
    /// 时间之弓没有 ManualDeliveryKG（氦气走气体管道，不需要小人送料），
    /// 但 EnergyGenerator 本身实现了 ISingleSliderControl（原版发电机用它调「补充燃料百分比」）。
    ///
    /// 原版 SingleSliderSideScreen 只按预制体名字黑名单排除了氢气/甲烷/石油/开发者发电机，
    /// 我们的建筑不在名单里 → 选中它就弹出那个滑块，而 EnergyGenerator.GetSliderTooltip
    /// 会去 GetComponent&lt;ManualDeliveryKG&gt;() 并直接解引用 → NullReferenceException，
    /// 整个详情面板刷新时崩溃。
    ///
    /// 这里把所有「带 EnergyGenerator 但没有 ManualDeliveryKG」的建筑统一排除在滑块侧屏之外，
    /// 比往黑名单里加 prefab 名更通用。
    /// </summary>
    [HarmonyPatch(typeof(SingleSliderSideScreen), "IsValidForTarget")]
    public static class SingleSliderSideScreen_IsValidForTarget_Patch
    {
        public static bool Prefix(GameObject target, ref bool __result)
        {
            try
            {
                if (target != null && target.GetComponent<EnergyGenerator>() != null &&
                    target.GetComponent<ManualDeliveryKG>() == null)
                {
                    __result = false;
                    return false;
                }
            }
            catch (Exception)
            {
            }

            return true;
        }
    }
}
