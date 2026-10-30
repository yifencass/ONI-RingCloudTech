using System.Collections.Generic;
using HarmonyLib;
using Klei.AI;
using STRINGS;
using TUNING;
using UnityEngine;
using UnityEngine.UI;

namespace RingCloudTech
{
    /// <summary>
    /// 生物学研究：在原版「基础/高级/太空/核能/轨道」之外新增一种研究类型。
    ///
    /// 机制完全走原版数据：
    ///   ResearchTypes.Types 里加一条  → 研究界面自动多一个标签页
    ///   ResearchPointInventory.PointsByTypeID 里加一个键  → 点数能记账
    ///   研究台 research_point_type_id = "biology"  → 该建筑产这种点
    ///   某个 Tech.costsByResearchTypeID["biology"] = N  → 该科技吃这种点
    /// </summary>
    public static class BiologyResearch
    {
        /// <summary>实验性功能总开关：关掉后不注入研究类型、不注册菜单、不改研究界面图标。</summary>
        public const bool ENABLED = false;

        public const string TYPE_ID = "biology";
        public const string ICON_KANIM = "biology_atom_kanim";
        public const string STATION_ID = "BiologyResearchCenter";
        public const string MASS_PER_POINT = "50";

        private static bool registered;

        /// <summary>
        /// 只把研究类型塞进 ResearchTypes.Types，不依赖 Research.Instance。
        /// 数据库（Codex）生成时会去 ResearchTypes 里查名字，所以必须在那之前就存在。
        /// </summary>
        public static bool RegisterTypeOnly(ResearchTypes types)
        {
            // 总开关关掉时，连研究类型都不注册——否则研究界面仍会多出一条「生物学研究」研究条。
            if (!ENABLED)
            {
                return false;
            }
            if (types == null)
            {
                return false;
            }

            foreach (ResearchType existing in types.Types)
            {
                if (existing.id == TYPE_ID)
                {
                    return false;
                }
            }

            Element algae = ElementLoader.FindElementByHash(SimHashes.Algae);
            Tag algaeTag = algae != null ? algae.tag : GameTags.Dirt;

            ResearchType biology = new ResearchType(
                TYPE_ID,
                (LocString)"生物学研究",
                (LocString)"研究生命的运转方式。把藻类喂进研究台，换取生物学研究点数。",
                Assets.GetSprite("research_type_alpha_icon"),
                new Color(0.42f, 0.86f, 0.40f),
                new Recipe.Ingredient[1] { new Recipe.Ingredient(algaeTag, 25f) },
                1200f,
                "research_center_kanim",
                new string[1] { STATION_ID },
                (LocString)"消耗 25 千克藻类换取生物学研究点数。");

            types.Types.Add(biology);
            Debug.Log("[RingCloudTech] 已注入生物学研究类型（早期）");
            return true;
        }
        public static void Register()
        {
            if (registered)
            {
                return;
            }

            Research research = Research.Instance;
            if (research == null || research.researchTypes == null || research.globalPointInventory == null)
            {
                return;
            }

            RegisterTypeOnly(research.researchTypes);

            if (!research.globalPointInventory.PointsByTypeID.ContainsKey(TYPE_ID))
            {
                research.globalPointInventory.PointsByTypeID[TYPE_ID] = 0f;
            }

            // 让一个原版科技也吃生物学点数：这样研究台一运转就能看到真实流程
            try
            {
                Tech farm = Db.Get().Techs.Get("FarmingTech");
                if (farm != null && !farm.costsByResearchTypeID.ContainsKey(TYPE_ID))
                {
                    farm.costsByResearchTypeID[TYPE_ID] = 100f;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 生物学研究挂科技失败: " + e.Message);
            }

            registered = true;
            Debug.Log("[RingCloudTech] 已注册生物学研究类型");
        }
    }

    // [实验性] 暂时停用：
        [HarmonyPatch(typeof(Research), "OnSpawn")]
    public static class Research_OnSpawn_Biology
    {
        public static void Postfix()
        {
            BiologyResearch.Register();
        }
    }

    /// <summary>
    /// 生物学研究台：照抄原版 ResearchCenter 的结构，只换输入材料（藻类）与研究类型。
    /// 贴图沿用 research_center_kanim（工作台本身的美术）。
    /// </summary>
    public class BiologyResearchCenterConfig : IBuildingConfig
    {
        public const string ID = "BiologyResearchCenter";

        private const float BASE_MASS_PER_SECOND = 1.1111112f;
        private const float MASS_PER_POINT = 50f;
        private const float CAPACITY = 750f;

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID,
                2,
                2,
                "research_center_kanim",
                30,
                30f,
                TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER4,
                MATERIALS.ALL_METALS,
                1600f,
                BuildLocationRule.OnFloor,
                noise: NOISE_POLLUTION.NOISY.TIER0,
                decor: TUNING.BUILDINGS.DECOR.NONE);

            def.RequiresPowerInput = true;
            def.EnergyConsumptionWhenActive = 60f;
            def.ViewMode = OverlayModes.Power.ID;
            def.ExhaustKilowattsWhenActive = 0.125f;
            def.SelfHeatKilowattsWhenActive = 1f;
            def.AudioCategory = "Metal";
            def.AudioSize = "large";
            def.AddSearchTerms(SEARCH_TERMS.RESEARCH);
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.ScienceBuilding);
            go.AddOrGet<BuildingComplete>().isManuallyOperated = true;
            Prioritizable.AddRef(go);

            Element algae = ElementLoader.FindElementByHash(SimHashes.Algae);
            Tag input = algae != null ? algae.tag : GameTags.Dirt;

            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = 1000f;
            storage.showInUI = true;

            ManualDeliveryKG delivery = go.AddOrGet<ManualDeliveryKG>();
            delivery.SetStorage(storage);
            delivery.RequestedItemTag = input;
            delivery.refillMass = 150f;
            delivery.capacity = CAPACITY;
            delivery.choreTypeIDHash = Db.Get().ChoreTypes.ResearchFetch.IdHash;

            ResearchCenter center = go.AddOrGet<ResearchCenter>();
            center.overrideAnims = new KAnimFile[1] { Assets.GetAnim("anim_interacts_research_center_kanim") };
            center.research_point_type_id = BiologyResearch.TYPE_ID;
            center.inputMaterial = input;
            center.mass_per_point = MASS_PER_POINT;

            ElementConverter converter = go.AddOrGet<ElementConverter>();
            converter.consumedElements = new ElementConverter.ConsumedElement[1]
            {
                new ElementConverter.ConsumedElement(input, BASE_MASS_PER_SECOND)
            };
            converter.showDescriptors = false;
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGetDef<PoweredController.Def>();
        }
    }
}
namespace RingCloudTech
{
    /// <summary>生物学研究点数用官方精灵管线：一个自带 ui 符号的小 kanim。</summary>
        [HarmonyPatch(typeof(ResearchScreen), "OnPrefabInit")]
    public static class ResearchScreen_BiologyIcon
    {
        public static void Postfix(ResearchScreen __instance)
        {
            try
            {
                System.Reflection.FieldInfo field = AccessTools.Field(typeof(ResearchScreen), "pointDisplayMap");
                if (field == null)
                {
                    return;
                }

                var map = field.GetValue(__instance) as System.Collections.Generic.Dictionary<string, LocText>;
                if (map == null || !map.ContainsKey(BiologyResearch.TYPE_ID))
                {
                    return;
                }

                KAnimFile anim = Assets.GetAnim(BiologyResearch.ICON_KANIM);
                if (anim == null)
                {
                    Debug.LogWarning("[RingCloudTech] 找不到 " + BiologyResearch.ICON_KANIM);
                    return;
                }

                Sprite sprite = Def.GetUISpriteFromMultiObjectAnim(anim);
                if (sprite == null)
                {
                    Debug.LogWarning("[RingCloudTech] 原子图标精灵为空");
                    return;
                }

                Image image = map[BiologyResearch.TYPE_ID].transform.parent.GetComponentInChildren<Image>();
                if (image != null)
                {
                    image.sprite = sprite;
                    image.color = Color.white;
                }

                Debug.Log("[RingCloudTech] 生物学研究点数已换用自带原子图标");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 生物学研究图标替换失败: " + e.Message);
            }
        }
    }

    /// <summary>ResearchTypes 一建出来就把生物学塞进去，早于数据库生成。</summary>
        [HarmonyPatch(typeof(ResearchTypes), MethodType.Constructor)]
    public static class ResearchTypes_Ctor_Biology
    {
        public static void Postfix(ResearchTypes __instance)
        {
            try
            {
                BiologyResearch.RegisterTypeOnly(__instance);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[RingCloudTech] 早期注入生物学研究类型失败: " + e);
            }
        }
    }

    /// <summary>
    /// 兜底：数据库生成（CodexCacheInit）会在 Research 还没就绪时调 ResearchCenter.GetDescriptors，
    /// 原版直接 Research.Instance.researchTypes.GetResearchType(id).name 会空引用炸掉整个游戏。
    /// </summary>
        [HarmonyPatch(typeof(ResearchCenter), "GetDescriptors")]
    public static class ResearchCenter_GetDescriptors_Safe
    {
        public static bool Prefix(GameObject go, ref System.Collections.Generic.List<Descriptor> __result)
        {
            Research research = Research.Instance;
            if (research != null && research.researchTypes != null)
            {
                BiologyResearch.RegisterTypeOnly(research.researchTypes);
                return true;
            }

            try
            {
                var list = new System.Collections.Generic.List<Descriptor>();
                list.Add(new Descriptor("消耗藻类换取生物学研究点数", "研究生命的运转方式。", Descriptor.DescriptorType.Requirement));
                __result = list;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 研究台描述兜底失败: " + e.Message);
                __result = new System.Collections.Generic.List<Descriptor>();
            }

            return false;
        }
    }
}