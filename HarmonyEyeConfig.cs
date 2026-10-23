using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼：9×9 巨型装置。
    ///
    /// 走配方逻辑（ComplexFabricator），不是元素转换器：
    ///     输入   氢气 1 kg
    ///     耗时   100 s
    ///     产出   熔融铁 100 kg     （平均每秒 1 kg）
    ///     产物用 TemperatureOperation.Melted，保证出来是液态铁
    ///
    /// 储库用 StandardSealedStorage：存进去的元素不与外界换热，即「不漏温存储」。
    ///
    /// 贴图四层堆叠（harmonyeye_kanim）：
    ///     1 ring / 2 sphere / 3 cross / 4 matrix
    /// 第 2 层的动态内容（空间扭曲 + 彩球）由 HarmonyEyeVisual 运行时渲染。
    ///
    /// 端口（相对左下角）：
    ///     W 电源   (4,0)
    ///     G 气体入 (0,4)
    ///     F 液体出 (8,4)
    ///     I 物品出 (8,2)  由整栋储库承担
    ///     O 气体出 (8,6)  配方无气体产物，预留
    /// </summary>
    public class HarmonyEyeConfig : IBuildingConfig
    {
        public const string ID = "HarmonyEye";

        private const string ANIM = "harmonyeye_kanim";

        public const int WIDTH = 9;
        public const int HEIGHT = 9;

        public const float HYDROGEN_KG = 50000f;    // 每次配方消耗 50 吨氢气
        public const float IRON_KG = 100f;
        public const float RECIPE_TIME = 1800f;     // 3 个周期
        public const float POWER_WATTS = 20000f;

        public const int HIT_POINTS = 2000;
        public const float CONSTRUCTION_TIME = 240f;
        public const float CONSTRUCTION_MASS = 4000f;
        public const float MELTING_POINT = 3600f;
        public const float STORAGE_CAPACITY = 100000f;   // 进气库：100 吨氢气
        public const float OUT_CAPACITY = 400000f;       // 出料库
        public const float OUTPUT_KG = 5000f;            // 每种产物 5 吨

        private static readonly CellOffset POWER_PORT = new CellOffset(0, 0);
        private static readonly CellOffset GAS_IN_PORT = new CellOffset(-4, 4);
        private static readonly CellOffset LIQUID_OUT_PORT = new CellOffset(4, 4);
        private static readonly CellOffset GAS_OUT_PORT = new CellOffset(4, 6);
        private static readonly CellOffset SOLID_OUT_PORT = new CellOffset(4, 2);

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

            def.RequiresPowerInput = true;
            def.EnergyConsumptionWhenActive = POWER_WATTS;
            def.SelfHeatKilowattsWhenActive = 4f;
            def.PowerInputOffset = POWER_PORT;

            // 端口类型必须写在这两个字段上：
            // BuildingCellVisualizer.CheckRequiresGasInput() 读的就是 InputConduitType
            def.InputConduitType = ConduitType.Gas;
            def.OutputConduitType = ConduitType.Liquid;

            def.UtilityInputOffset = GAS_IN_PORT;
            def.UtilityOutputOffset = LIQUID_OUT_PORT;

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

            // 氢气管道输入：用**独立的专用储气箱**，不再塞进 ComplexFabricator 的 inStorage。
            // 塞进 inStorage 时，配方队列为空会触发存储区整理，气体被装罐后丢出建筑。
            // 照储气库的做法：独立 Storage 专门存氢气，不寄生在 ComplexFabricator.inStorage 上
            Storage hydrogenStore = go.AddComponent<Storage>();
            hydrogenStore.capacityKg = STORAGE_CAPACITY;
            hydrogenStore.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            hydrogenStore.showInUI = true;

            ConduitConsumer gasIn = go.AddOrGet<ConduitConsumer>();
            gasIn.conduitType = ConduitType.Gas;
            gasIn.storage = fabricator.inStorage;
            go.AddOrGet<HarmonyEyeInfo>();
            gasIn.capacityTag = ElementLoader.FindElementByHash(SimHashes.Hydrogen).tag;
            gasIn.capacityKG = STORAGE_CAPACITY;
            gasIn.consumptionRate = 10f;
            gasIn.forceAlwaysSatisfied = true;
            fabricator.inStorage.useGunForDelivery = false;   // 禁止小人送料（含罐装气体）
            // 以前是 Dump：氢气存不进去就被倒回世界（表现为「气变回散装掉出来」）
            gasIn.wrongElementResult = ConduitConsumer.WrongElementResult.Store;

            // 熔融铁液体输出
            ConduitDispenser liquidOut = go.AddOrGet<ConduitDispenser>();
            liquidOut.conduitType = ConduitType.Liquid;
            liquidOut.storage = fabricator.outStorage;
            liquidOut.alwaysDispense = true;
            liquidOut.elementFilter = null;

            // 副端口：视觉图标由 BuildingCellVisualizer 读 ISecondaryOutput 画出。
            // O 气体出 (8,6)、I 物品出 (8,2)。配方目前没有这两类产物，先占位。
            ConduitSecondaryOutput gasOut = go.AddComponent<ConduitSecondaryOutput>();
            gasOut.portInfo = new ConduitPortInfo(ConduitType.Gas, GAS_OUT_PORT);

            ConduitSecondaryOutput solidOut = go.AddComponent<ConduitSecondaryOutput>();
            solidOut.portInfo = new ConduitPortInfo(ConduitType.Solid, SOLID_OUT_PORT);

            ConfigureRecipe();
            Prioritizable.AddRef(go);
        }

        // 熔融金属小行星：12 种熔融物各 5 吨（银在游戏里不存在）
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
                time = RECIPE_TIME,
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
                time = RECIPE_TIME,
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