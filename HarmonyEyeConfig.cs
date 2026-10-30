using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼：9×9 巨型装置。
    ///
    /// 走配方逻辑（ComplexFabricator），不是元素转换器：
    ///     输入   氢气 50 t
    ///     耗时   1800 s（3 周期）
    ///     产出   熔融金属 12 种 / 理想生活 23 种，每种 50 t
    ///
    /// **氢气与产物全部走 ALN 频道**，建筑本身没有任何管道/运输端口：
    ///     - 进气：HarmonyEyeAlnBridge 每秒从频道取氢，补进 inStorage
    ///     - 出料：HarmonyEyeOutput 把配方产物直接 DepositToChannel
    ///     - 电力：仍由建筑自己吃电网 20 kW，不托管给 ALN 能量池
    ///
    /// 没装 ALN 时本建筑没有氢源，会静止不工作（不会崩）。
    ///
    /// 储库用 StandardSealedStorage：存进去的元素不与外界换热，即「不漏温存储」。
    ///
    /// 贴图四层堆叠（harmonyeye_kanim）：
    ///     1 ring / 2 sphere / 3 cross / 4 matrix
    /// 第 2 层的动态内容（空间扭曲 + 彩球）由 HarmonyEyeVisual 运行时渲染。
    ///
    /// 端口（相对左下角）：
    ///     W 电源   (0,0)
    /// 其余端口（气体入 / 液体出 / 气体出 / 物品出）在 ALN 化改造中全部取消。
    /// </summary>
    public class HarmonyEyeConfig : IBuildingConfig
    {
        public const string ID = "HarmonyEye";

        private const string ANIM = "harmonyeye_kanim";

        public const int WIDTH = 9;
        public const int HEIGHT = 9;

        public const float HYDROGEN_KG = 50000f;    // 每次配方消耗 50 吨氢气
        /// <summary>配方耗时的默认值；实际生效值来自 harmonyeye.cfg 的 recipe_time。</summary>
        public const float RECIPE_TIME = HarmonyEyeSettings.DefaultRecipeTime;
        public const float POWER_WATTS = 20000f;

        public const int HIT_POINTS = 2000;
        public const float CONSTRUCTION_TIME = 240f;
        public const float CONSTRUCTION_MASS = 30000f;   // 30 吨中子质
        public const float MELTING_POINT = 3600f;
        public const float STORAGE_CAPACITY = 100000f;   // 进气库：100 吨氢气
        public const float OUT_CAPACITY = 2000000f;       // 出料库（产物正常走频道，这里只做兜底）
        public const float OUTPUT_KG = 50000f;            // 每种产物 50 吨

        private static readonly CellOffset POWER_PORT = new CellOffset(0, 0);

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID,
                WIDTH,
                HEIGHT,
                ANIM,
                HIT_POINTS,
                CONSTRUCTION_TIME,
                new float[] { CONSTRUCTION_MASS },
                MATERIALS.REFINED_METALS,
                MELTING_POINT,
                BuildLocationRule.OnFloor,
                decor: BUILDINGS.DECOR.NONE,
                noise: NOISE_POLLUTION.NONE);

            // 建造材料：中子质 30 吨（ONI 建造只支持单一材料类别 + 质量）
            Element neutronium = ElementLoader.FindElementByHash(SimHashes.Unobtanium);
            // MaterialCategory 是 string[]：列出建造时允许消耗的材料标签
            def.MaterialCategory = new string[]
            {
                neutronium != null ? neutronium.tag.ToString() : "Unobtanium"
            };
            def.Mass = new float[] { CONSTRUCTION_MASS };

            def.Floodable = false;
            def.Entombable = false;
            def.Overheatable = false;
            def.AudioCategory = "Metal";
            def.AudioSize = "large";
            def.BaseTimeUntilRepair = -1f;
            def.PermittedRotations = PermittedRotations.Unrotatable;
            def.ObjectLayer = ObjectLayer.Building;
            def.SceneLayer = Grid.SceneLayer.Building;
            def.ShowInBuildMenu = true;

            // 电力保持不变：鸿蒙之眼自己吃电网，不经 ALN 能量池。
            def.RequiresPowerInput = true;
            def.EnergyConsumptionWhenActive = POWER_WATTS;
            def.SelfHeatKilowattsWhenActive = 4f;
            def.PowerInputOffset = POWER_PORT;

            // 不再声明任何管道端口：InputConduitType / OutputConduitType 保持默认 None，
            // BuildingCellVisualizer 也就不会再画 G/F/O/I 图标。

            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<BuildingComplete>().isManuallyOperated = false;

            ComplexFabricator fabricator = go.AddOrGet<ComplexFabricator>();
            fabricator.duplicantOperated = false;
            fabricator.showProgressBar = true;
            fabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;
            fabricator.storeProduced = true;
            fabricator.keepExcessLiquids = true;

            go.AddOrGet<FabricatorIngredientStatusManager>();
            go.AddOrGet<CopyBuildingSettings>();

            BuildingTemplates.CreateComplexFabricatorStorage(go, fabricator);

            fabricator.inStorage.capacityKg = STORAGE_CAPACITY;
            fabricator.inStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            fabricator.outStorage.capacityKg = OUT_CAPACITY;
            fabricator.outStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            fabricator.buildStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            fabricator.inStorage.useGunForDelivery = false;   // 禁止小人送料

            // ALN 桥接：进气 + 出料全部走频道。
            go.AddOrGet<HarmonyEyeAlnBridge>();

            // 产物滞留队列必须挂在**预制体**上。
            // 运行时用 AddOrGet 加上的组件不会进存档（KSerializer 只序列化预制体里
            // 已有的 KMonoBehaviour），表现就是读档后滞留产物凭空消失。
            go.AddOrGet<HarmonyEyeOutput>();

            ConfigureRecipe();
            Prioritizable.AddRef(go);
        }

        // 熔融金属小行星：12 种熔融物各 50 吨
        private static readonly string[] MOLTEN_OUTPUTS =
        {
            "MoltenIron", "MoltenCopper", "MoltenAluminum", "MoltenCobalt",
            "MoltenGold", "MoltenZinc", "MoltenNickel", "MoltenIridium",
            "MoltenLead", "MoltenTungsten", "MoltenNiobium", "Magma",
        };

        // 理想生活小行星：17 种固体 + 5 种液体 + 1 种气体
        private static readonly string[] LIFE_SOLIDS =
        {
            "SandStone", "SedimentaryRock", "Algae", "Cuprite", "IronOre",
            "AluminumOre", "GoldAmalgam", "NickelOre", "ZincOre", "Wolframite",
            "Graphite", "Granite", "Obsidian", "SlimeMold", "Electrum",
            "Dirt", "Carbon",
        };

        private static readonly string[] LIFE_LIQUIDS = { "Petroleum", "Water", "SaltWater", "Brine", "DirtyWater" };
        private static readonly string[] LIFE_GASES = { "Chlorine" };

        private static void AddOutputs(List<ComplexRecipe.RecipeElement> list, string[] names)
        {
            foreach (string name in names)
            {
                SimHashes hash;
                if (!System.Enum.TryParse<SimHashes>(name, true, out hash))
                {
                    Debug.LogWarning("[RingCloudTech] 未知元素，已跳过: " + name);
                    continue;
                }
                Element el = ElementLoader.FindElementByHash(hash);
                if (el == null)
                {
                    Debug.LogWarning("[RingCloudTech] 元素不存在，已跳过: " + name);
                    continue;
                }
                // 纯元素产物必须用 Melted + storeElement，否则会走 prefab 实例化崩掉
                list.Add(new ComplexRecipe.RecipeElement(
                    el.tag, OUTPUT_KG,
                    ComplexRecipe.RecipeElement.TemperatureOperation.Melted,
                    storeElement: true));
            }
        }

        private void ConfigureRecipe()
        {
            Tag hydrogenTag = ElementLoader.FindElementByHash(SimHashes.Hydrogen).tag;

            ComplexRecipe.RecipeElement[] inputs = new ComplexRecipe.RecipeElement[] { new ComplexRecipe.RecipeElement(ElementLoader.FindElementByHash(SimHashes.Hydrogen).tag, HYDROGEN_KG) };

            // ── 配方 1：熔融金属小行星 ──
            List<ComplexRecipe.RecipeElement> molten = new List<ComplexRecipe.RecipeElement>();
            AddOutputs(molten, MOLTEN_OUTPUTS);

            string id1 = ComplexRecipeManager.MakeRecipeID(ID, inputs, molten.ToArray());
            new ComplexRecipe(id1, inputs, molten.ToArray())
            {
                time = HarmonyEyeSettings.RecipeTime,
                description = "模拟熔融金属行星的形成与陨落",
                fabricators = new List<Tag> { TagManager.Create(ID) },
                sortOrder = 100,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Custom,
                customName = "熔融金属小行星",
            };

            // ── 配方 2：理想生活小行星 ──
            List<ComplexRecipe.RecipeElement> life = new List<ComplexRecipe.RecipeElement>();
            AddOutputs(life, LIFE_SOLIDS);
            AddOutputs(life, LIFE_LIQUIDS);
            AddOutputs(life, LIFE_GASES);

            string id2 = ComplexRecipeManager.MakeRecipeID(ID, inputs, life.ToArray());
            new ComplexRecipe(id2, inputs, life.ToArray())
            {
                time = HarmonyEyeSettings.RecipeTime,
                description = "模拟理想聚居地的形成与陨落",
                fabricators = new List<Tag> { TagManager.Create(ID) },
                sortOrder = 200,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Custom,
                customName = "理想生活小行星",
            };

            Debug.Log("[RingCloudTech] 已注册配方 " + id1 + " / " + id2
                      + "（熔融 " + molten.Count + " 种，理想生活 " + life.Count + " 种）");
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<HarmonyEyeVisual>();
        }
    }
}
