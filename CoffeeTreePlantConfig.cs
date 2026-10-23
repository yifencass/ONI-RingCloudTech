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
    /// 美术：用原版虫果芽 `wormwood_kanim` 的 **super_*（正常版/不贫瘠）** 动画组。
    ///   原版 WormPlant（贫瘠）用 basic_*，SuperWormPlant（正常）用 super_*，
    ///   两者共用同一张图集。这里走 super_*。
    ///
    /// 预览：`coffee_tree_preview_kanim` —— 把同一图集整体转成白色线稿的副本，
    ///   配合 super_idle_full 得到「正常版植株的白描」。
    ///   原版只有 WormPlant 注册了预览且用的是 basic 造型的 place 剪辑，
    ///   正常版没有白描素材，所以必须自绘。
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
        /// 正常版动画组。对应原版 SuperWormPlantConfig 里的 animSet。
        /// wilt_base 会被 StandardCropPlant 自动拼上 1/2/3。
        /// </summary>
        private static readonly StandardCropPlant.AnimSet ANIMS = new StandardCropPlant.AnimSet
        {
            grow = "super_grow",
            grow_pst = "super_grow_pst",
            idle_full = "super_idle_full",
            wilt_base = "super_wilt",
            harvest = "super_harvest",
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
                max_age: 2400f,
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

            // 预览：直接复用本体 kanim 里的 "place" 白描剪辑。
            //
            // 和我们自己的生长动画一样，都来自 coffee_tree_kanim（原版虫果芽
            // 图集的完整拷贝），所以不需要额外的预览专用 kanim。
            //
            // 注意 "place" 画的是原版【贫瘠版】虫果芽的造型，而不是我们生长时
            // 用的 super_*（正常版）。这一点是刻意接受的：原版只为 WormPlant
            // 注册了预览、也只画了一套白描，正常版根本没有对应素材。
            // 与其自绘一张，不如直接复用它 —— 预览只是放置时的轮廓提示，
            // 和实际长出来的植株形态不一致是可以接受的。
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
