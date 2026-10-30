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

            if (deliveries == null)
            {
                deliveries = GetComponents<ManualDeliveryKG>();
            }
            if (deliveries == null || deliveries.Length == 0)
            {
                return;
            }

            // 收集「当前排队配方」真正需要的原料标签。
            // 只判断有没有配方是不够的：排了黑咖啡配方时，
            // 火椒粒/蔗糖的搬运组件同样会被唤醒，复制人就会白搬。
            HashSet<Tag> needed = null;
            if (fabricator.HasAnyOrder)
            {
                needed = new HashSet<Tag>();
                ComplexRecipe[] all = fabricator.GetRecipes();
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        ComplexRecipe recipe = all[i];
                        if (recipe == null || !fabricator.IsRecipeQueued(recipe))
                        {
                            continue;
                        }
                        foreach (ComplexRecipe.RecipeElement ing in recipe.ingredients)
                        {
                            needed.Add(ing.material);
                        }
                    }
                }
            }

            foreach (ManualDeliveryKG d in deliveries)
            {
                if (d == null)
                {
                    continue;
                }
                bool shouldPause = needed == null || !needed.Contains(d.RequestedItemTag);
                d.allowPause = true;   // 同时允许玩家在面板手动开关
                d.Pause(shouldPause, "BeverageMixer: no queued recipe needs this");
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

        // ───── 咖啡系配方 ─────
        // 咖啡可乐：水 161 + 咖啡豆 1 + CO2 5 + 蔗糖 33 = 200
        public const float COFFEECOLA_WATER = 161f;
        public const float COFFEECOLA_BEAN = 1f;
        public const float COFFEECOLA_SUCROSE = 33f;
        public const float COFFEECOLA_CO2 = 5f;
        public const float COFFEECOLA_MASS = 200f;

        // 黑咖啡：水 98 + 咖啡豆 2 = 100
        public const float BLACKCOFFEE_WATER = 98f;
        public const float BLACKCOFFEE_BEAN = 2f;
        public const float BLACKCOFFEE_MASS = 100f;

        /// <summary>黑咖啡元素 id（对应 elements/blackcoffee.yaml）。</summary>
        public const string BLACKCOFFEE_TAG = "BlackCoffee";

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

            // 咖啡豆搬运（咖啡可乐与黑咖啡共用）
            ManualDeliveryKG bean = go.AddComponent<ManualDeliveryKG>();
            bean.SetStorage(fabricator.inStorage);
            bean.RequestedItemTag = new Tag(CoffeeTreeRegistrar.BEAN_ID);
            bean.capacity = 40f;
            bean.refillMass = 10f;
            bean.MinimumMass = COFFEECOLA_BEAN;
            bean.choreTypeIDHash = Db.Get().ChoreTypes.MachineFetch.IdHash;

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

            // ───── 咖啡可乐：水 165 + 咖啡豆 1 + 蔗糖 34 → 可乐 200 ─────
            Tag beanTag = new Tag(CoffeeTreeRegistrar.BEAN_ID);
            RegisterMeltedRecipe(
                new ComplexRecipe.RecipeElement[]
                {
                    new ComplexRecipe.RecipeElement(waterTag, COFFEECOLA_WATER),
                    new ComplexRecipe.RecipeElement(beanTag, COFFEECOLA_BEAN),
                    new ComplexRecipe.RecipeElement(co2Tag, COFFEECOLA_CO2),
                    new ComplexRecipe.RecipeElement(sucroseTag, COFFEECOLA_SUCROSE),
                },
                cola.tag, COFFEECOLA_MASS, cola.tag.ToString(),
                "可乐",
                "以咖啡豆替代火椒粒的可乐，焦香更重。");

            // ───── 黑咖啡：水 98 + 咖啡豆 2 → 黑咖啡 100 ─────
            Element blackCoffee = ElementLoader.FindElementByName(BLACKCOFFEE_TAG);
            if (blackCoffee == null)
            {
                Debug.LogWarning("[RingCloudTech] 未找到 BlackCoffee 元素，跳过黑咖啡配方");
            }
            else
            {
                RegisterMeltedRecipe(
                    new ComplexRecipe.RecipeElement[]
                    {
                        new ComplexRecipe.RecipeElement(waterTag, BLACKCOFFEE_WATER),
                        new ComplexRecipe.RecipeElement(beanTag, BLACKCOFFEE_BEAN),
                    },
                    blackCoffee.tag, BLACKCOFFEE_MASS, BLACKCOFFEE_TAG,
                    "黑咖啡",
                    "98 kg 水加 2 kg 咖啡豆冲泡而成，苦味浓烈，但足够撑起一个通宵。");
            }
        }

        /// <summary>
        /// 注册一条「产物是纯元素」的配方：必须用 Melted，
        /// AverageTemperature/Heated 分支会 Assets.GetPrefab + KInstantiate，元素没有 prefab。
        /// </summary>
        private static void RegisterMeltedRecipe(
            ComplexRecipe.RecipeElement[] inputs,
            Tag product,
            float productMass,
            string spritePrefabId,
            string displayName,
            string description)
        {
            ComplexRecipe.RecipeElement[] outputs =
            {
                new ComplexRecipe.RecipeElement(
                    product, productMass,
                    ComplexRecipe.RecipeElement.TemperatureOperation.Melted,
                    storeElement: true),
            };

            string id = ComplexRecipeManager.MakeRecipeID(ID, inputs, outputs);

            new ComplexRecipe(id, inputs, outputs)
            {
                time = RECIPE_TIME,
                description = description,
                fabricators = new List<Tag> { TagManager.Create(ID) },
                sortOrder = 120,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Custom,
                customName = displayName,
                customSpritePrefabID = spritePrefabId,
            };

            Debug.Log("[RingCloudTech] 已注册配方 " + id + " (" + displayName + ")");
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
