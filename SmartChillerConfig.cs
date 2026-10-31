using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 智能液冷机：4×3 液体冷却器，把管道里的液体降到「屏幕上设定的目标温度」。
    ///
    /// 贴图布局（以贴图左上角为第 0 行、第 0 列）：
    ///     X X X X
    ///     I X X O     I = 液体输入口（左边缘中排）
    ///     X W T X     O = 液体输出口（右边缘中排）
    ///                 W = 电源接口（底排第 2 格，也就是建筑单元格本身）
    ///                 T = 信号接口（底排第 3 格，逻辑输入：关闭时停止工作）
    ///
    /// 坐标换算（BuildingDef.GenerateOffsets：宽 4 → x = -1..2，高 3 → y = 0..2，
    /// 建筑单元格 = 底排第 2 格，不是贴图左下角）：
    ///     贴图左下角   = 建筑单元格 + (-1, 0)
    ///     I（第 1 列中排） = 建筑单元格 + (-1, 1)  → UtilityInputOffset
    ///     O（第 4 列中排） = 建筑单元格 + ( 2, 1)  → UtilityOutputOffset
    ///     W（第 2 列底排） = 建筑单元格 + ( 0, 0)  → PowerInputOffset
    ///     T（第 3 列底排） = 建筑单元格 + ( 1, 0)  → LogicInputPorts
    /// </summary>
    public class SmartChillerConfig : IBuildingConfig
    {
        public const string ID = "SmartChiller";

        // ───── 占地与动画 ─────
        public const int WIDTH = 4;
        public const int HEIGHT = 3;
        private const string ANIM = "smartchiller_kanim";

        // ───── 结构 ─────
        private const int HIT_POINTS = 100;
        private const float CONSTRUCTION_TIME = 180f;
        private const float MELTING_POINT = 1600f;

        /// <summary>
        /// 建造材料：精炼金属 1200 kg + 密封圈 200 kg。
        /// Mass 与 MaterialCategory 必须等长且顺序一致 —— BuildingDef.PostProcess
        /// 按 (MaterialCategory[i], Mass[i]) 逐条生成建造配方的 Ingredient。
        /// </summary>
        private static readonly float[] CONSTRUCTION_MASS = { 1200f, 200f };
        private static readonly string[] CONSTRUCTION_MATERIALS =
        {
            "RefinedMetal",     // MATERIALS.REFINED_METALS[0]
            "BuildingGasket",   // MATERIALS.GASKET —— 密封圈
        };

        // ───── 电力 ─────
        public const float WATTAGE = 1800f;

        // ───── 热学 ─────
        /// <summary>导热系数（BuildingDef.ThermalConductivity，原版默认 1）。</summary>
        public const float THERMAL_CONDUCTIVITY = 8f;

        /// <summary>过热温度 200 °C。</summary>
        public const float OVERHEAT_TEMPERATURE = 200f + 273.15f;

        // ───── 管道 ─────
        /// <summary>进料速率 kg/s（与 ConduitConsumer.consumptionRate 共用）。</summary>
        public const float CONSUMPTION_RATE = 10f;

        private static readonly List<Storage.StoredItemModifier> StoredItemModifiers =
            new List<Storage.StoredItemModifier>
            {
                Storage.StoredItemModifier.Hide,
                Storage.StoredItemModifier.Insulate,
                Storage.StoredItemModifier.Seal,
            };

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID, WIDTH, HEIGHT, ANIM, HIT_POINTS, CONSTRUCTION_TIME,
                CONSTRUCTION_MASS, CONSTRUCTION_MATERIALS, MELTING_POINT,
                BuildLocationRule.OnFloor,
                decor: TUNING.BUILDINGS.DECOR.NONE,
                noise: NOISE_POLLUTION.NOISY.TIER2);

            // 电力建筑：RequiresPowerInput / ViewMode=Power / AudioCategory=HollowMetal
            BuildingTemplates.CreateElectricalBuildingDef(def);
            def.EnergyConsumptionWhenActive = WATTAGE;
            def.SelfHeatKilowattsWhenActive = 0f;
            def.ExhaustKilowattsWhenActive = 0f;

            // 液体进出：I 左边缘中排、O 右边缘中排
            def.InputConduitType = ConduitType.Liquid;
            def.OutputConduitType = ConduitType.Liquid;
            def.UtilityInputOffset = new CellOffset(-1, 1);
            def.UtilityOutputOffset = new CellOffset(2, 1);

            // W 电源口：底排第 2 格 = 建筑单元格
            def.PowerInputOffset = new CellOffset(0, 0);

            // 可潜水 = Floodable false（BuildingLoader 只在 Floodable 为真时挂 Floodable 组件）
            def.Floodable = false;
            def.Entombable = true;

            // 水平翻转
            def.PermittedRotations = PermittedRotations.FlipH;

            def.OverheatTemperature = OVERHEAT_TEMPERATURE;
            def.ThermalConductivity = THERMAL_CONDUCTIVITY;

            // 液体管道覆盖层（原版液冷机同款）
            def.ViewMode = OverlayModes.LiquidConduits.ID;
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.LiquidVentIDs, ID);

            // T 信号口：底排第 3 格，逻辑输入（作为 LogicOperationalController 的使能口）
            def.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(1, 0));

            def.AddSearchTerms(SEARCH_TERMS.POWER);
            def.AddSearchTerms(SEARCH_TERMS.WATER);
            def.AddSearchTerms(SEARCH_TERMS.METAL);

            Debug.Log(string.Format(
                "[RingCloudTech] 智能液冷机: {0}×{1} 格, {2} W, 液体入口=({3},{4}), 液体出口=({5},{6}), 电源口=({7},{8}), 信号口=({9},{10}), 材料数={11}, 导热={12}, 过热={13:0.#} K",
                def.WidthInCells, def.HeightInCells, def.EnergyConsumptionWhenActive,
                def.UtilityInputOffset.x, def.UtilityInputOffset.y,
                def.UtilityOutputOffset.x, def.UtilityOutputOffset.y,
                def.PowerInputOffset.x, def.PowerInputOffset.y,
                def.LogicInputPorts != null && def.LogicInputPorts.Count > 0 ? def.LogicInputPorts[0].cellOffset.x : 0,
                def.LogicInputPorts != null && def.LogicInputPorts.Count > 0 ? def.LogicInputPorts[0].cellOffset.y : 0,
                def.MaterialCategory.Length, def.ThermalConductivity, def.OverheatTemperature));

            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            KPrefabID prefabID = go.GetComponent<KPrefabID>();
            prefabID.AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LoopingSounds>();

            // 冷却产生的热会灌进建筑自身结构温度（和原版液冷机一样），用这个效果画热浪
            go.AddOrGet<KBatchedAnimHeatPostProcessingEffect>();

            // 液体缓冲罐：ConduitConsumer 灌进来，SmartChiller 冷却后推到输出口
            ConduitConsumer consumer = go.AddOrGet<ConduitConsumer>();
            consumer.conduitType = ConduitType.Liquid;
            consumer.consumptionRate = CONSUMPTION_RATE;

            Storage storage = BuildingTemplates.CreateDefaultStorage(go);
            storage.showInUI = true;
            storage.capacityKg = 2f * consumer.consumptionRate;
            storage.SetDefaultStoredItemModifiers(StoredItemModifiers);

            // 逻辑使能口（T），无信号时按未接线处理 → 正常工作
            go.AddOrGet<LogicOperationalController>();

            // 冷却逻辑 + 目标温度滑块（IThresholdSwitch）
            go.AddOrGet<SmartChiller>();

            // 涡轮 / 两根指针 / 屏幕温度文本（运行时 sprite 叠加）
            go.AddOrGet<SmartChillerVisual>();

            go.AddOrGetDef<PoweredActiveController.Def>();
        }
    }
}
