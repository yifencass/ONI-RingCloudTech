using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 超级电容库：4×3 大型储能建筑。
    /// 直接继承原版 BaseBatteryConfig，并复用 BatterySmart 的自动化输出逻辑，
    /// 所以「按当前电量输出信号」的行为和原版智能电池完全一致。
    /// </summary>
    public class SuperCapacitorConfig : BaseBatteryConfig
    {
        public const string ID = "SuperCapacitor";

        // 占地面积
        public const int WIDTH = 4;
        public const int HEIGHT = 3;

        // ───── 电力 ─────
        /// <summary>容量 800 kJ（Battery.capacity 单位是焦耳）。</summary>
        public const float CAPACITY = 800000f;

        /// <summary>
        /// 泄漏：游戏内按周期显示，Battery 的说明描述用的是
        /// GameUtil.TimeSlice.PerCycle，换算系数 600（一个周期 600 秒）。
        /// 要求「每周期漏 60 焦耳」，所以：
        ///     60 J/周期 ÷ 600 = 0.1 J/s
        /// 填 0.1 时游戏会显示成「60.0 焦耳/周期」。
        /// </summary>
        public const float JOULES_LOST_PER_SECOND = 0.1f;

        // ───── 建筑 ─────
        public const float CONSTRUCTION_TIME = 60f;
        public const float CONSTRUCTION_MASS = 1200f;

        /// <summary>产热 50 复制热/秒。BuildingDef 里单位是千瓦，1 kW = 1000 DTU/s。</summary>
        public const float SELF_HEAT_KILOWATTS = 0.05f;

        /// <summary>过热温度 50℃。BuildingDef 里用开尔文。</summary>
        public const float OVERHEAT_CELSIUS = 50f;

        /// <summary>导热系数。</summary>
        public const float THERMAL_CONDUCTIVITY = 1f;

        /// <summary>熔点，精炼金属建筑沿用原版 800K。</summary>
        private const float MELTING_POINT = 800f;

        /// <summary>耐久度，和原版电池一致。</summary>
        private const int HIT_POINTS = 30;

        // ───── 接口位置（以左下角为原点）─────
        //   XXXX   ← 第 3 行 (y=2)
        //   XXXX   ← 第 2 行 (y=1)
        //   XWNX   ← 第 1 行 (y=0)
        //    ^^ 电源接口 x=1，信号源 x=2
        private static readonly CellOffset PowerPortOffset = new CellOffset(1, 0);
        private static readonly CellOffset LogicPortOffset = new CellOffset(2, 0);

        /// <summary>
        /// 自制外观（4×3 电容柜）。
        /// 由 sp_editor/src/make_supercap_kanim.py 生成，
        /// 自带 Battery 需要的 meter / logicmeter 符号与动画。
        /// </summary>
        private const string ANIM = "supercapacitor_kanim";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = base.CreateBuildingDef(
                ID,
                WIDTH,
                HEIGHT,
                HIT_POINTS,
                ANIM,
                CONSTRUCTION_TIME,
                new float[] { CONSTRUCTION_MASS },
                MATERIALS.REFINED_METALS,
                MELTING_POINT,
                0f,                       // 不排气
                SELF_HEAT_KILOWATTS,
                TUNING.BUILDINGS.DECOR.PENALTY.TIER2,
                NOISE_POLLUTION.NOISY.TIER1);

            def.OverheatTemperature = OVERHEAT_CELSIUS + 273.15f;
            def.ThermalConductivity = THERMAL_CONDUCTIVITY;
            def.Floodable = true;         // 可淹没

            // 电源接口
            // 注意：Battery.OnSpawn() 里是 PowerCell = GetPowerInputCell()，
            // 而 BuildingCellVisualizer 画的是 PowerOutputOffset。
            // 两个偏移必须一致，否则「线的连接点在 A、电路实际在 B」，电网不认这台电池。
            // 原版电池两个偏移都默认 (0,0)，所以没暴露这个问题。
            def.PowerOutputOffset = PowerPortOffset;
            def.PowerInputOffset = PowerPortOffset;

            // 自动化输出（智能电池同款端口）
            def.LogicOutputPorts = new List<LogicPorts.Port>
            {
                LogicPorts.Port.OutputPort(
                    BatterySmart.PORT_ID,
                    LogicPortOffset,
                    STRINGS.BUILDINGS.PREFABS.SUPERCAPACITOR.LOGIC_PORT,
                    STRINGS.BUILDINGS.PREFABS.SUPERCAPACITOR.LOGIC_PORT_ACTIVE,
                    STRINGS.BUILDINGS.PREFABS.SUPERCAPACITOR.LOGIC_PORT_INACTIVE,
                    show_wire_missing_icon: true)
            };

            def.AddSearchTerms(SEARCH_TERMS.POWER);
            def.AddSearchTerms(SEARCH_TERMS.BATTERY);

            Debug.Log(string.Format(
                "[RingCloudTech] 超级电容库: {0}×{1} 格, 旋转={2}, 电力口=({3},{4}), 信号口=({5},{6}), 过热={7}K",
                def.WidthInCells, def.HeightInCells, def.PermittedRotations,
                def.PowerInputOffset.x, def.PowerInputOffset.y,
                LogicPortOffset.x, LogicPortOffset.y,
                def.OverheatTemperature));

            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            base.ConfigureBuildingTemplate(go, prefab_tag);
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.PowerBuilding);
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            BatterySmart battery = go.AddOrGet<BatterySmart>();
            battery.capacity = CAPACITY;
            battery.joulesLostPerSecond = JOULES_LOST_PER_SECOND;
            battery.powerSortOrder = 1000;

            base.DoPostConfigureComplete(go);
        }
    }
}
