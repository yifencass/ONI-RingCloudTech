using STRINGS;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 时间之弓：4×3 氦气发电机，额定功率 800 W。
    ///
    /// 贴图布局（以贴图左上角为第 0 行）：
    ///     X X X X
    ///     I X X O     I = 左边缘中排的气体输入口（只收氦气）
    ///     X W X X     O = 右边缘中排的固体产物生成位（不是管道口）
    ///                 W = 底排第二格的电源输出口
    ///
    /// 坐标换算（已在反编译源码里核实，不要想当然）：
    ///   BuildingDef.GenerateOffsets() 给宽度 4 的建筑生成的 PlacementOffsets 是
    ///   x = -1..2、y = 0..2，也就是说 Grid.PosToCell(建筑) 并不是贴图左下角，
    ///   而是「底排、从左往右第 2 格」。因此：
    ///     贴图左下角原点   = 建筑单元格 + (-1, 0)
    ///     I（左边缘中排）  = 原点 + (0, 1) = 建筑单元格 + (-1, 1) → UtilityInputOffset
    ///     W（底排第 2 格） = 原点 + (1, 0) = 建筑单元格 + (0, 0)  → PowerOutputOffset
    ///     O（右边缘中排）  = 原点 + (3, 1) = 建筑单元格 + (2, 1)  → TimeBowSolidSpawner 的生成偏移
    ///
    /// 氦气消耗 30 kg/周期 = 0.05 kg/s；中子质产出 500 kg/周期 = 0.8333 kg/s。
    /// </summary>
    public class TimeBowConfig : IBuildingConfig
    {
        /// <summary>建筑/预制体 id。建造菜单与科技树都用这个 id 接线（RingCloudTech.cs 已引用）。</summary>
        public const string ID = "TimeBow";

        // ───── 占地与动画 ─────
        public const int WIDTH = 4;
        public const int HEIGHT = 3;
        private const string ANIM = "timebow_kanim";

        // ───── 结构 ─────
        private const int HIT_POINTS = 100;
        private const float CONSTRUCTION_TIME = 120f;

        /// <summary>
        /// 建造材料：精炼金属 400 kg + 玻璃 200 kg + 塑料 50 kg。
        /// BuildingTemplates.CreateBuildingDef 会把 construction_materials 原样写进
        /// def.MaterialCategory，而 BuildingDef.PostProcess 按 MaterialCategory[i] 与
        /// (int)Mass[i] 一一配对生成建造配方，所以两个数组必须等长、且顺序一致。
        /// </summary>
        private static readonly float[] CONSTRUCTION_MASS = { 400f, 200f, 50f };
        private static readonly string[] CONSTRUCTION_MATERIALS =
        {
            MATERIALS.REFINED_METALS[0], // "RefinedMetal"
            MATERIALS.GLASSES[0],        // "Glasses"
            MATERIALS.PLASTICS[0],       // "Plastic"
        };

        /// <summary>熔点：和原版发电机保持一致的 2400 K。</summary>
        private const float MELTING_POINT = 2400f;

        // ───── 电力 ─────
        public const float WATTAGE = 800f;

        // ───── 装饰 ─────
        public const int DECOR_AMOUNT = 45;
        public const int DECOR_RADIUS = 8;

        // ───── 氦气 ─────
        /// <summary>每周期 30 kg ÷ 600 s = 0.05 kg/s。</summary>
        public const float HELIUM_KG_PER_SECOND = 0.05f;

        /// <summary>机内氦气储量上限，EnergyGenerator 的 formula 与 ConduitConsumer 共用。</summary>
        public const float HELIUM_STORAGE_KG = 30f;

        /// <summary>
        /// 管道每秒进料速率。比 0.05 kg/s 的消耗速率大，保证消耗端不会因为进料被卡。
        /// </summary>
        public const float HELIUM_CONSUMPTION_RATE = 0.1f;

        // ───── 中子质 ─────
        /// <summary>每周期 500 kg ÷ 600 s = 0.8333 kg/s。</summary>
        public const float NEUTRONIUM_KG_PER_SECOND = 500f / 600f;

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID, WIDTH, HEIGHT, ANIM, HIT_POINTS, CONSTRUCTION_TIME,
                CONSTRUCTION_MASS, CONSTRUCTION_MATERIALS, MELTING_POINT,
                BuildLocationRule.OnFloor,
                decor: new EffectorValues(DECOR_AMOUNT, DECOR_RADIUS),
                noise: default(EffectorValues));

            // 发电
            def.GeneratorWattageRating = WATTAGE;
            def.GeneratorBaseCapacity = def.GeneratorWattageRating;

            // 不发热、不排气
            def.ExhaustKilowattsWhenActive = 0f;
            def.SelfHeatKilowattsWhenActive = 0f;

            // 装饰 +45 / 半径 8（BuildingLoader 会按这两个字段给 DecorProvider 赋值）
            def.BaseDecor = DECOR_AMOUNT;
            def.BaseDecorRadius = DECOR_RADIUS;

            def.ViewMode = OverlayModes.Power.ID;
            def.AudioCategory = "Metal";

            // 气体输入：建筑单元格左侧相邻格、中排（贴图 I）
            def.InputConduitType = ConduitType.Gas;
            def.UtilityInputOffset = new CellOffset(-1, 1);

            // 电力输出：建筑单元格自身，即贴图底排第 2 格（贴图 W）
            def.RequiresPowerOutput = true;
            def.PowerOutputOffset = new CellOffset(0, 0);

            def.AddSearchTerms(SEARCH_TERMS.POWER);
            def.AddSearchTerms(SEARCH_TERMS.GENERATOR);

            Debug.Log(string.Format(
                "[RingCloudTech] 时间之弓: {0}×{1} 格, {2} W, 气体输入口=({3},{4}), 电力输出口=({5},{6}), 材料数={7}, 装饰=+{8}/{9}",
                def.WidthInCells, def.HeightInCells, def.GeneratorWattageRating,
                def.UtilityInputOffset.x, def.UtilityInputOffset.y,
                def.PowerOutputOffset.x, def.PowerOutputOffset.y,
                def.MaterialCategory.Length, def.BaseDecor, def.BaseDecorRadius));

            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            // 房间/建筑分类标签（和原版发电机一致）
            KPrefabID prefabID = go.GetComponent<KPrefabID>();
            prefabID.AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);
            prefabID.AddTag(RoomConstraints.ConstraintTags.PowerBuilding);
            prefabID.AddTag(RoomConstraints.ConstraintTags.GeneratorType);
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LoopingSounds>();

            // 氦气罐：ConduitConsumer 往里灌，EnergyGenerator 从里面扣
            go.AddOrGet<Storage>();

            ConduitConsumer consumer = go.AddOrGet<ConduitConsumer>();
            consumer.conduitType = ConduitType.Gas;
            consumer.consumptionRate = HELIUM_CONSUMPTION_RATE;
            consumer.capacityTag = GameTagExtensions.Create(SimHashes.Helium);
            consumer.capacityKG = HELIUM_STORAGE_KG;
            // 和原版发电机一致：管道没接满也保持「已满足」，否则建筑会一直报缺料
            consumer.forceAlwaysSatisfied = true;
            consumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;

            EnergyGenerator generator = go.AddOrGet<EnergyGenerator>();
            // 每周期 30 kg 氦气 → 800 W；max_stored_input_mass 与 Storage 容量一致
            generator.formula = EnergyGenerator.CreateSimpleFormula(
                SimHashes.Helium.CreateTag(), HELIUM_KG_PER_SECOND, HELIUM_STORAGE_KG);
            generator.powerDistributionOrder = 8;
            generator.ignoreBatteryRefillPercent = true;
            // 不显示储量表，所以 timebow_kanim 不需要 meter_* 符号
            generator.hasMeter = false;

            // 固体产物生成器：只在真正发电（Operational.IsActive）时累积产出
            go.AddOrGet<TimeBowSolidSpawner>();

            // 表盘三根指针：运行时读 timebow_clock\hand_*.png 叠在机身上转动
            go.AddOrGet<TimeBowClock>();

            go.AddOrGetDef<PoweredActiveController.Def>();
        }
    }
}
