using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 甜蔗（植物本体）。
    ///
    /// 结构参考原版 SaltySticksPlantConfig（咸蔗），因为两者定位一致：
    ///   - 灌溉型作物（ExtendPlantToIrrigated）
    ///   - 1x2 直立，BuildingBack 图层
    ///   - 收获后产出可直接食用的茎秆
    ///
    /// 数值见 SweetCaneRegistrar。
    /// 动画使用本体自带的 sweet_cane_kanim（由 _assets/ 部署到 anim/ 目录）。
    /// </summary>
    public class SweetCanePlantConfig : IEntityConfig
    {
        public const string ID = SweetCaneRegistrar.PLANT_ID;

        public const string SEED_ID = SweetCaneRegistrar.SEED_ID;

        public GameObject CreatePrefab()
        {
            // 必须早于 prefab 生成，否则 ExtendEntityToBasicPlant 内部会抛异常
            SweetCaneRegistrar.EnsureRegistered();

            GameObject plant = EntityTemplates.CreatePlacedEntity(
                ID,
                STRINGS.CREATURES.SPECIES.SWEETCANE.NAME,
                STRINGS.CREATURES.SPECIES.SWEETCANE.DESC,
                1f,
                decor: DECOR.PENALTY.TIER1,
                anim: Assets.GetAnim("sweet_cane_kanim"),
                initialAnim: "idle_empty",
                sceneLayer: Grid.SceneLayer.BuildingBack,
                width: 1,
                height: 2);

            EntityTemplates.ExtendEntityToBasicPlant(
                plant,
                temperature_lethal_low: 273.15f,   //   0 °C
                temperature_warning_low: 283.15f,  //  10 °C
                temperature_warning_high: 313.15f, //  40 °C
                temperature_lethal_high: 318.15f,  //  45 °C
                safe_elements: new SimHashes[3]
                {
                    SimHashes.Oxygen,
                    SimHashes.ContaminatedOxygen,
                    SimHashes.CarbonDioxide
                },
                pressure_sensitive: true,
                pressure_lethal_low: 0f,
                pressure_warning_low: 0.15f,
                crop_id: SweetCaneRegistrar.FRUIT_ID,
                can_drown: true,
                can_tinker: true,
                require_solid_tile: true,
                require_Backwall_Foundation: false,
                should_grow_old: true,
                max_age: 2400f,
                min_radiation: 0f,
                max_radiation: 9800f,
                baseTraitId: ID + "Original",
                baseTraitName: STRINGS.CREATURES.SPECIES.SWEETCANE.NAME);

            // 灌溉：10 kg/周期净水
            EntityTemplates.ExtendPlantToIrrigated(plant, new PlantElementAbsorber.ConsumeInfo[1]
            {
                new PlantElementAbsorber.ConsumeInfo
                {
                    tag = GameTags.Water,
                    massConsumptionRate = SweetCaneRegistrar.WATER_RATE
                }
            });

            plant.AddOrGet<StandardCropPlant>();
            plant.AddOrGet<LoopingSounds>();
            plant.AddOrGet<DirectlyEdiblePlant_Growth>();

            // 野生生长速率：改成养殖的一半，使野生恰好是 12 周期
            // （默认 1/2400 会让 6 周期的作物在野生下变成 24 周期）
            Growing growing = plant.AddOrGet<Growing>();
            growing.WILD_GROWTH_RATE = SweetCaneRegistrar.WILD_GROWTH_RATE;

            // 种子
            IHasDlcRestrictions dlcRestrictions = this as IHasDlcRestrictions;
            List<Tag> additionalTags = new List<Tag> { GameTags.CropSeed };

            EntityTemplates.CreateAndRegisterPreviewForPlant(
                EntityTemplates.CreateAndRegisterSeedForPlant(
                    plant,
                    dlcRestrictions,
                    SeedProducer.ProductionType.Harvest,
                    SEED_ID,
                    STRINGS.CREATURES.SPECIES.SEEDS.SWEETCANE.NAME,
                    STRINGS.CREATURES.SPECIES.SEEDS.SWEETCANE.DESC,
                    Assets.GetAnim("seed_sweet_cane_kanim"),
                    "object",
                    1,
                    additionalTags,
                    SingleEntityReceptacle.ReceptacleDirection.Top,
                    default,
                    1,
                    STRINGS.CREATURES.SPECIES.SWEETCANE.DOMESTICATEDDESC),
                ID + "_preview",
                Assets.GetAnim("sweet_cane_kanim"),
                "place",
                1,
                2);

            return plant;
        }

        public void OnPrefabInit(GameObject inst)
        {
        }

        public void OnSpawn(GameObject inst)
        {
        }
    }
}
