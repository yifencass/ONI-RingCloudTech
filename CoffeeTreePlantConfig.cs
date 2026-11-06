using System.Collections.Generic;
using Klei.AI;
using STRINGS;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 咖啡树 —— 火椒树的近亲，1×2 直立作物。
    ///
    /// 美术：史前 DLC 的 GardenFoodPlant 植株（`spike_fruit_kanim`）图集，
    ///   用换色工坊的「翠金」预设上色后改名成 `coffee_tree_kanim` 自带一份，
    ///   不依赖原版/DLC 资产是否加载。剪辑名见下方 ANIMS。
    ///
    /// 预览：不另建 kanim，直接复用本体 kanim 自带的 "place" 白描剪辑。
    ///
    /// 光照：900 勒克斯。实现方式是给 MinLightLux 属性挂 AttributeModifier，
    ///   再挂 IlluminationVulnerable（注意 SetPrefersDarkness() 无参 = false = 需要光照）。
    ///   缺光时它会枯萎停止生长，这正是「需要充足光照才能生长」的效果。
    ///
    /// 施肥：**刻意不做**。玩家设定咖啡树只需光照即可生长，不需要施肥。
    ///   所以这里不调用 EntityTemplates.ExtendPlantToFertilizable。
    /// </summary>
    public class CoffeeTreePlantConfig : IEntityConfig
    {
        /// <summary>
        /// 生长动画组。
        ///
        /// 咖啡树的美术现在换成史前 DLC 的 GardenFoodPlant（植株 spike_fruit_kanim），
        /// 所以剪辑名和之前用虫果芽（super_*）时完全不同：
        ///
        ///     虫果芽（旧）         史前花园植物（现）
        ///     super_grow           grow
        ///     super_grow_pst       grow_pst
        ///     super_idle_full      idle_full
        ///     super_wilt (+1/2/3)  wilt (+1/2/3)
        ///     super_harvest        harvest
        ///
        /// wilt_base 会被 StandardCropPlant 自动拼上 1/2/3，所以写 "wilt" 即可。
        /// </summary>
        private static readonly StandardCropPlant.AnimSet ANIMS = new StandardCropPlant.AnimSet
        {
            grow = "grow",
            grow_pst = "grow_pst",
            idle_full = "idle_full",
            wilt_base = "wilt",
            harvest = "harvest",
        };

        public GameObject CreatePrefab()
        {
            CoffeeTreeRegistrar.EnsureRegistered();

            GameObject plant = EntityTemplates.CreatePlacedEntity(
                CoffeeTreeRegistrar.PLANT_ID,
                STRINGS.CREATURES.SPECIES.COFFEETREE.NAME,
                STRINGS.CREATURES.SPECIES.COFFEETREE.DESC,
                1f,
                decor: DECOR.BONUS.TIER1,
                anim: Assets.GetAnim(CoffeeTreeRegistrar.PLANT_ANIM),
                initialAnim: "idle_empty",
                sceneLayer: Grid.SceneLayer.BuildingBack,
                width: 1,
                height: 2);

            EntityTemplates.ExtendEntityToBasicPlant(
                plant,
                CoffeeTreeRegistrar.TEMP_LETHAL_LOW,
                CoffeeTreeRegistrar.TEMP_WARNING_LOW,
                CoffeeTreeRegistrar.TEMP_WARNING_HIGH,
                CoffeeTreeRegistrar.TEMP_LETHAL_HIGH,
                new SimHashes[]
                {
                    SimHashes.Oxygen,
                    SimHashes.ContaminatedOxygen,
                    SimHashes.CarbonDioxide,
                },
                pressure_sensitive: true,
                pressure_lethal_low: 0f,
                pressure_warning_low: 0.15f,
                crop_id: CoffeeTreeRegistrar.BEAN_ID,
                can_drown: true,
                can_tinker: true,
                require_solid_tile: true,
                should_grow_old: true,
                // 原版默认 2400 s（4 周期）比 18 周期的生长期短得多，
                // 一旦成熟就会被 Growing 的 oldAge 判定强制立刻自动收获。
                // 给到 20 周期，让玩家能自己决定什么时候收。
                max_age: 12000f,
                min_radiation: 0f,
                max_radiation: 9800f,
                baseTraitId: CoffeeTreeRegistrar.PLANT_ID + "Original",
                baseTraitName: STRINGS.CREATURES.SPECIES.COFFEETREE.NAME);

            // 灌溉：不做。
            //
            // 玩家设定：咖啡树「只需要充足的光照即可生长」。
            // 既不需要施肥（不调用 ExtendPlantToFertilizable），
            // 也不需要灌溉（不调用 ExtendPlantToIrrigated）。
            // 它唯一的环境要求就是 900 勒克斯光照，见下方。

            // ── 光照需求 900 勒克斯 ──────────────────────────
            // 原版 PrickleFlowerConfig 的做法：
            //   1) 给初始 trait 加 MinLightLux 修正（UI 会显示需求）
            //   2) initialAttributes 里声明这个属性
            //   3) 挂 IlluminationVulnerable 并「不偏好黑暗」
            Modifiers modifiers = plant.GetComponent<Modifiers>();
            Db.Get().traits.Get(modifiers.initialTraits[0]).Add(
                new AttributeModifier(
                    Db.Get().PlantAttributes.MinLightLux.Id,
                    CoffeeTreeRegistrar.MIN_LIGHT_LUX,
                    STRINGS.CREATURES.SPECIES.COFFEETREE.NAME));
            modifiers.initialAttributes.Add(Db.Get().PlantAttributes.MinLightLux.Id);

            // 注意：SetPrefersDarkness(bool prefersDarkness = false) 参数默认是 false，
            // 无参调用等于「不偏好黑暗」= 需要光照。方法名有迷惑性。
            plant.AddOrGet<IlluminationVulnerable>().SetPrefersDarkness();

            // 不施肥：刻意省略 ExtendPlantToFertilizable



            plant.AddOrGet<StandardCropPlant>();
            plant.AddOrGet<LoopingSounds>();
            plant.AddOrGet<PlantFiberProducer>().amount = CoffeeTreeRegistrar.FIBER_ON_UPROOT;

            // 种子。这里用不带 IHasDlcRestrictions 的重载（3 参起始那个），
            // 我们的 config 没有实现 IHasDlcRestrictions。
            GameObject seed = EntityTemplates.CreateAndRegisterSeedForPlant(
                plant,
                SeedProducer.ProductionType.Harvest,
                CoffeeTreeRegistrar.SEED_ID,
                STRINGS.CREATURES.SPECIES.SEEDS.COFFEETREE.NAME,
                STRINGS.CREATURES.SPECIES.SEEDS.COFFEETREE.DESC,
                Assets.GetAnim(CoffeeTreeRegistrar.SEED_ANIM),
                "object",
                1,
                new List<Tag> { GameTags.CropSeed },
                SingleEntityReceptacle.ReceptacleDirection.Top,
                default,
                2,
                STRINGS.CREATURES.SPECIES.COFFEETREE.DOMESTICATEDDESC,
                EntityTemplates.CollisionShape.CIRCLE,
                0.3f,
                0.3f);

            // 预览：直接复用本体 kanim（史前花园植物 spike_fruit 图集）里的
            // "place" 白描剪辑，不再另建预览专用 kanim。
            //
            // 早期版本曾把整张图集转成白色线稿做成 coffee_tree_preview_kanim，
            // 但游戏里显示不出来；改用资产自带的 place 后预览稳定可见。
            // 预览只是放置时的轮廓提示，与长成后的翠金配色植株不必一致。
            EntityTemplates.CreateAndRegisterPreviewForPlant(
                seed,
                CoffeeTreeRegistrar.PREVIEW_ID,
                Assets.GetAnim(CoffeeTreeRegistrar.PLANT_ANIM),
                "place",
                1,
                2);

            return plant;
        }

        public void OnPrefabInit(GameObject prefab)
        {
            // 必须在 prefab 阶段设置动画组，StandardCropPlant 会用这个来做生长/收获动画
            prefab.AddOrGet<StandardCropPlant>().anims = ANIMS;
        }

        public void OnSpawn(GameObject inst)
        {
        }
    }
}
