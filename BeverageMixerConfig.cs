using System;
using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 搬运闸门：没有排配方时暂停 ManualDeliveryKG，避免复制人无故搬料。
    /// 默认行为是"storage 没满就派活"，和配方队列无关。
    /// </summary>
    public class BeverageMixerDeliveryGate : KMonoBehaviour, ISim1000ms
    {
        private ComplexFabricator fabricator;
        private ManualDeliveryKG[] deliveries;
        private bool lastPaused;
        private bool initialized;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            fabricator = GetComponent<ComplexFabricator>();
            deliveries = GetComponents<ManualDeliveryKG>();
        }

        public void Sim1000ms(float dt)
        {
            if (fabricator == null)
            {
                return;
            }

            bool shouldPause = !fabricator.HasAnyOrder;
            if (initialized && shouldPause == lastPaused)
            {
                return;
            }
            initialized = true;
            lastPaused = shouldPause;

            if (deliveries == null)
            {
                deliveries = GetComponents<ManualDeliveryKG>();
            }
            foreach (ManualDeliveryKG d in deliveries)
            {
                if (d == null)
                {
                    continue;
                }
                d.allowPause = true;   // 同时允许玩家在面板手动开关
                d.Pause(shouldPause, "BeverageMixer: no queued recipe");
            }
        }
    }

    /// <summary>
    /// 饮料调配机：2x2，复用汽水机贴图。
    /// 端口：液体输入(1,1) / 液体输出(0,0) / 电力(1,0)。
    /// CO2 与火椒粒靠复制人搬运。
    /// </summary>
    public class BeverageMixerConfig : IBuildingConfig
    {
        public const string ID = "BeverageMixer";

        // 配方比例（每批）
        // 每批输入合计必须等于 COLA_MASS，否则会凭空造/灭质量
        //   水 80.00 + 二氧化碳 4.55 + 火椒粒 0.91 + 蔗糖 14.54 = 100.00
        public const float WATER_MASS = 80f;
        public const float CO2_MASS = 4.55f;
        public const float SPICE_MASS = 0.91f;     // 原 4.55 降到 1/5
        public const float SUCROSE_MASS = 14.54f;
        public const float COLA_MASS = 100f;

        // ───── 配方时间 ─────
        // 本体复用 sodamaker_kanim，ComplexFabricator 的动画时序是：
        //   working_pre(播一次) → working_loop(循环) → 工作结束切 working_pst
        // 所以配方时间必须是「前摇 + 整数个循环」，
        // 否则工作会在循环播到一半时结束，动画被硬切，看着像卡了一下。
        //   working_pre  = 131 帧
        //   working_loop =  64 帧
        //   帧率 = 30 fps                          （数据来自 ANIM v5）
        private const int ANIM_PRE_FRAMES = 131;
        private const int ANIM_LOOP_FRAMES = 64;
        private const float ANIM_FPS = 30f;

        // 按要求直接定 8 秒。
        // 注意：8 秒不等于「前摇 + 整数个循环」，工作会在 working_loop 播到中途时结束，
        // 动画会硬切回 working_pst。想要对齐的话，最近的几个档位是：
        //   k=1 → 195/30 = 6.5000 秒
        //   k=2 → 259/30 = 8.6333 秒
        // 把 RECIPE_TIME 换成 (ANIM_PRE_FRAMES + k * ANIM_LOOP_FRAMES) / ANIM_FPS 即可。
        public const float RECIPE_TIME = 8f;

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID, 2, 2, "sodamaker_kanim", 100, 120f,
                TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER3,
                MATERIALS.REFINED_METALS, 1600f,
                BuildLocationRule.OnFloor,
                noise: NOISE_POLLUTION.NONE,
                decor: TUNING.BUILDINGS.DECOR.BONUS.TIER1);

            def.ViewMode = OverlayModes.LiquidConduits.ID;
            def.AudioCategory = "Metal";
            def.AudioSize = "large";
            def.Overheatable = true;

            def.InputConduitType = ConduitType.Liquid;
            def.UtilityInputOffset = new CellOffset(1, 1);

            def.OutputConduitType = ConduitType.Liquid;
            def.UtilityOutputOffset = new CellOffset(0, 0);

            def.RequiresPowerInput = true;
            def.PowerInputOffset = new CellOffset(1, 0);
            def.EnergyConsumptionWhenActive = 480f;
            def.SelfHeatKilowattsWhenActive = 2f;

            def.PermittedRotations = PermittedRotations.FlipH;
            def.AddSearchTerms(SEARCH_TERMS.FOOD);
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);

            go.AddOrGet<DropAllWorkable>();
            go.AddOrGet<BuildingComplete>().isManuallyOperated = false;

            ComplexFabricator fabricator = go.AddOrGet<ComplexFabricator>();
            fabricator.duplicantOperated = false;
            fabricator.showProgressBar = true;
            fabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;
            go.AddOrGet<FabricatorIngredientStatusManager>();
            go.AddOrGet<CopyBuildingSettings>();

            // 创建 inStorage / buildStorage / outStorage（没有主 Storage）
            BuildingTemplates.CreateComplexFabricatorStorage(go, fabricator);

            // 原料产物都是液体元素，需要保留余料
            fabricator.keepExcessLiquids = true;
            fabricator.storeProduced = true;
            fabricator.inStorage.capacityKg = 1000f;
            fabricator.inStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            fabricator.buildStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            fabricator.outStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);

            // 液体输入：ConduitConsumer.storage 默认 null，必须显式指向 inStorage
            ConduitConsumer consumer = go.AddOrGet<ConduitConsumer>();
            consumer.conduitType = ConduitType.Liquid;
            consumer.storage = fabricator.inStorage;
            consumer.capacityTag = ElementLoader.FindElementByHash(SimHashes.Water).tag;
            consumer.capacityKG = 500f;
            consumer.consumptionRate = 5f;
            consumer.forceAlwaysSatisfied = true;
            consumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;

            // 液体输出：产物在 outStorage
            ConduitDispenser dispenser = go.AddOrGet<ConduitDispenser>();
            dispenser.conduitType = ConduitType.Liquid;
            dispenser.alwaysDispense = true;
            dispenser.elementFilter = null;
            dispenser.storage = fabricator.outStorage;

            // CO2 搬运
            ManualDeliveryKG co2 = go.AddOrGet<ManualDeliveryKG>();
            co2.SetStorage(fabricator.inStorage);
            co2.RequestedItemTag = SimHashes.CarbonDioxide.CreateTag();
            co2.capacity = 20f;
            co2.refillMass = 10f;
            co2.MinimumMass = CO2_MASS;
            co2.choreTypeIDHash = Db.Get().ChoreTypes.MachineFetch.IdHash;

            // 火椒粒搬运（游戏内 Tag 是 SpiceNut）
            ManualDeliveryKG spice = go.AddComponent<ManualDeliveryKG>();
            spice.SetStorage(fabricator.inStorage);
            spice.RequestedItemTag = new Tag("SpiceNut");
            spice.capacity = 20f;
            spice.refillMass = 10f;
            spice.MinimumMass = SPICE_MASS;
            spice.choreTypeIDHash = Db.Get().ChoreTypes.MachineFetch.IdHash;

            // 蔗糖搬运（本体资源 SimHashes.Sucrose，甜盒虫/甜盒树产线）
            ManualDeliveryKG sucrose = go.AddComponent<ManualDeliveryKG>();
            sucrose.SetStorage(fabricator.inStorage);
            sucrose.RequestedItemTag = SimHashes.Sucrose.CreateTag();
            sucrose.capacity = 100f;
            sucrose.refillMass = 30f;
            sucrose.MinimumMass = SUCROSE_MASS;
            sucrose.choreTypeIDHash = Db.Get().ChoreTypes.MachineFetch.IdHash;

            ConfigureRecipes();
            Prioritizable.AddRef(go);
        }

        private void ConfigureRecipes()
        {
            Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
            if (cola == null)
            {
                Debug.LogError("[RingCloudTech] 可乐元素未加载，无法配置配方");
                return;
            }

            Tag waterTag = ElementLoader.FindElementByHash(SimHashes.Water).tag;
            Tag co2Tag = SimHashes.CarbonDioxide.CreateTag();
            Tag spiceTag = new Tag("SpiceNut");
            Tag sucroseTag = SimHashes.Sucrose.CreateTag();

            ComplexRecipe.RecipeElement[] inputs =
            {
                new ComplexRecipe.RecipeElement(waterTag, WATER_MASS),
                new ComplexRecipe.RecipeElement(co2Tag, CO2_MASS),
                new ComplexRecipe.RecipeElement(spiceTag, SPICE_MASS),
                new ComplexRecipe.RecipeElement(sucroseTag, SUCROSE_MASS),
            };

            // 产物是纯元素，必须用 Melted：
            // AverageTemperature/Heated 分支会 Assets.GetPrefab + KInstantiate，元素没有 prefab。
            ComplexRecipe.RecipeElement[] outputs =
            {
                new ComplexRecipe.RecipeElement(
                    cola.tag, COLA_MASS,
                    ComplexRecipe.RecipeElement.TemperatureOperation.Melted,
                    storeElement: true),
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID(ID, inputs, outputs);

            new ComplexRecipe(recipeId, inputs, outputs)
            {
                time = RECIPE_TIME,
                description = STRINGS.UI.BEVERAGEMIXER.RECIPE_DESC,
                fabricators = new List<Tag> { TagManager.Create(ID) },
                sortOrder = 100,

                // 元素没有专属 prefab 图标，用 Custom 模式：
                // customSpritePrefabID 会走 Def.GetUISprite(string)，内部先查元素。
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Custom,
                customName = STRINGS.UI.BEVERAGEMIXER.RECIPE_NAME,
                customSpritePrefabID = RingCloudTechMod.COLA_TAG,
            };

            Debug.Log("[RingCloudTech] 已注册配方 " + recipeId);
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LogicOperationalController>();
            go.AddOrGetDef<PoweredActiveController.Def>();
            SymbolOverrideControllerUtil.AddToPrefab(go);
            go.AddOrGet<BeverageMixerDeliveryGate>();
        }
    }
}
