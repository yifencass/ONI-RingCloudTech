using System.Collections.Generic;
using System.Globalization;
using KSerialization;
using STRINGS;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 智能液冷机的核心逻辑：把管道里的液体冷却到「目标温度」，同时兼任目标温度侧屏
    /// （ISingleSliderControl → SingleSliderSideScreen 只给一个滑块 + 数字输入框，
    ///   不继承温度传感器那套「检测环境温度 / 输出信号」的阈值语义）。
    ///
    /// 与原本版 AirConditioner 的区别：原版是固定 ΔT（-14 K），这里是「冷却到指定温度」——
    ///   进液温度 &gt; 目标温度 → 冷却到目标温度再输出（消耗电力）；
    ///   进液温度 ≤ 目标温度 → 原样输出，不降温也不耗电。
    ///
    /// 吞吐：进料 1000 kg/s、内部缓冲罐 1 吨；每 tick 处理一件（一包管道液体）就推到出口。
    ///
    /// 冷却带走的热量全部灌进建筑自身的结构温度（和原版液冷机一致，不会凭空消失），
    /// 所以机器会越来越热，需要玩家自己给机器降温。
    /// </summary>
    public class SmartChiller : KMonoBehaviour, ISaveLoadable, ISim200ms, ISingleSliderControl
    {
        // ───── 目标温度范围 ─────
        /// <summary>默认目标温度 20 °C。</summary>
        public const float DEFAULT_TARGET_TEMPERATURE = 293.15f;

        /// <summary>最低目标 -100 °C（再低没有实际用途，还容易把管道冻裂）。</summary>
        public const float RANGE_MIN = 173.15f;

        /// <summary>最高目标 200 °C。</summary>
        public const float RANGE_MAX = 473.15f;

        /// <summary>输出液体温度的硬下限：引擎对 ≤0 K 的元素会断言崩溃。</summary>
        private const float OUTPUT_TEMPERATURE_FLOOR = 1f;

        /// <summary>温差小于这个值就认为「已经够冷了」，避免浮点抖动导致反复进出冷却状态。</summary>
        private const float TEMPERATURE_EPSILON = 0.01f;

        /// <summary>目标温度（Kelvin）。侧屏滑块与数字框都写这个字段。</summary>
        [Serialize]
        private float targetTemperature = DEFAULT_TARGET_TEMPERATURE;

        private KSelectable selectable;
        private Storage storage;
        private Operational operational;
        private ConduitConsumer consumer;
        private BuildingComplete building;

        private HandleVector<int>.Handle structureTemperature;
        private int outputCell = -1;
        private float lastSampleTime = -1f;

        private bool cooling;
        private bool hasFlow;

        // ───── 供 SmartChillerVisual 读取的状态 ─────

        /// <summary>本 tick 是否真的在冷却（进液高于目标温度）。</summary>
        public bool IsCooling => cooling;

        /// <summary>本 tick 是否有液体通过（含直通不冷却）。</summary>
        public bool HasLiquidFlow => hasFlow;

        /// <summary>当前目标温度（Kelvin）。</summary>
        public float TargetTemperature => targetTemperature;

        /// <summary>本机是否已通电（用于运行指针）。</summary>
        public bool IsPowered
        {
            get
            {
                EnergyConsumer ec = GetComponent<EnergyConsumer>();
                return ec != null && ec.IsPowered;
            }
        }

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            // 数值范围保护：老存档里可能是 0 或越界值
            if (targetTemperature < RANGE_MIN || targetTemperature > RANGE_MAX)
            {
                targetTemperature = Mathf.Clamp(targetTemperature, RANGE_MIN, RANGE_MAX);
            }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            selectable = GetComponent<KSelectable>();
            storage = GetComponent<Storage>();
            operational = GetComponent<Operational>();
            consumer = GetComponent<ConduitConsumer>();
            building = GetComponent<BuildingComplete>();

            if (building == null || storage == null || operational == null || consumer == null)
            {
                // 蓝图/建造中的预制体上没有这些组件，直接退出即可
                enabled = false;
                return;
            }

            try
            {
                structureTemperature = GameComps.StructureTemperatures.GetHandle(gameObject);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 智能液冷机取结构温度句柄失败: " + e.Message);
            }

            outputCell = building.GetUtilityOutputCell();

            Debug.Log(string.Format(
                "[RingCloudTech] 智能液冷机就位：目标 {0:0.#} K（{1:0.#} °C），液体出口格={2}",
                targetTemperature, targetTemperature - 273.15f, outputCell));

            // 一次性核对建造材料（BuildingGasket 是「个数」不是 kg，容易配错）
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                Recipe recipe = building.Def.CraftRecipe;
                if (recipe != null && recipe.Ingredients != null)
                {
                    for (int i = 0; i < recipe.Ingredients.Count; i++)
                    {
                        Recipe.Ingredient ing = recipe.Ingredients[i];
                        if (i > 0)
                        {
                            sb.Append(" + ");
                        }
                        sb.AppendFormat("{0} {1:0.#}", ing.tag.ProperName(), ing.amount);
                    }
                }
                Debug.Log("[RingCloudTech] 智能液冷机建造材料：" + sb);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 打印建造材料失败: " + e.Message);
            }
        }

        // ───── 冷却逻辑 ─────

        public void Sim200ms(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            if (operational == null || !operational.IsOperational)
            {
                cooling = false;
                hasFlow = false;
                operational.SetActive(false);
                return;
            }

            if (storage == null || outputCell < 0 || building == null)
            {
                return;
            }

            bool didCool = false;
            bool didMove = false;
            List<GameObject> drained = null;

            for (int i = 0; i < storage.items.Count; i++)
            {
                GameObject item = storage.items[i];
                if (item == null)
                {
                    continue;
                }

                PrimaryElement pe = item.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= 0f || pe.Element == null || !pe.Element.IsLiquid)
                {
                    continue;
                }

                float inputTemperature = pe.Temperature;

                bool needsCooling = inputTemperature > targetTemperature + TEMPERATURE_EPSILON;
                float outputTemperature = needsCooling
                    ? Mathf.Max(targetTemperature, OUTPUT_TEMPERATURE_FLOOR)
                    : inputTemperature;

                float moved = Game.Instance.liquidConduitFlow.AddElement(
                    outputCell, pe.ElementID, pe.Mass, outputTemperature, pe.DiseaseIdx, pe.DiseaseCount);

                if (moved <= 0f)
                {
                    // 输出管道满了，这一 tick 不处理
                    break;
                }

                pe.KeepZeroMassObject = true;
                float movedRatio = moved / pe.Mass;
                int diseaseDelta = (int)(pe.DiseaseCount * movedRatio);

                // 先算出搬走这部分带走（或带入）的热量，再改质量
                float dtu = (outputTemperature - inputTemperature)
                            * pe.Element.specificHeatCapacity * moved;

                pe.Mass -= moved;
                if (diseaseDelta != 0)
                {
                    pe.ModifyDiseaseCount(-diseaseDelta, "SmartChiller.Sim200ms");
                }

                if (needsCooling)
                {
                    didCool = true;

                    float displayDt = lastSampleTime > 0f ? Time.time - lastSampleTime : Mathf.Max(dt, 0.001f);
                    lastSampleTime = Time.time;

                    // 抽走的热量灌回建筑本体（0f - dtu：dtu 为负 → 建筑得热）
                    GameComps.StructureTemperatures.ProduceEnergy(
                        structureTemperature, 0f - dtu,
                        BUILDING.STATUSITEMS.OPERATINGENERGY.PIPECONTENTS_TRANSFER, displayDt);
                }

                if (pe.Mass <= 0.01f && drained == null)
                {
                    drained = new List<GameObject>();
                }
                if (pe.Mass <= 0.01f)
                {
                    drained.Add(item);
                }

                didMove = true;
                break;
            }

            // 抽空的液体物件交给 Storage 清掉（迭代中不能删，否则会打乱 items 列表）
            if (drained != null)
            {
                for (int i = 0; i < drained.Count; i++)
                {
                    if (drained[i] != null)
                    {
                        Util.KDestroyGameObject(drained[i]);
                    }
                }
            }

            cooling = didCool;
            hasFlow = didMove;
            operational.SetActive(didCool);
        }

        // ───── ISingleSliderControl：目标温度滑块 ─────
        // 只要一个「设定温度」的滑块 + 数字框，所以不实现 IThresholdSwitch：
        // 那套是给温度传感器用的（检测环境温度、高于/低于判定、对外输出信号），本机都不需要。

        private const string SLIDER_TITLE_KEY = "STRINGS.UI.SMARTCHILLER.TITLE";
        private const string SLIDER_TOOLTIP_KEY = "STRINGS.UI.SMARTCHILLER.TOOLTIP";

        public string SliderTitleKey => SLIDER_TITLE_KEY;

        public string SliderUnits
        {
            get
            {
                switch (GameUtil.temperatureUnit)
                {
                    case GameUtil.TemperatureUnit.Fahrenheit:
                        return UI.UNITSUFFIXES.TEMPERATURE.FAHRENHEIT;
                    case GameUtil.TemperatureUnit.Kelvin:
                        return UI.UNITSUFFIXES.TEMPERATURE.KELVIN;
                    default:
                        return UI.UNITSUFFIXES.TEMPERATURE.CELSIUS;
                }
            }
        }

        /// <summary>数字框保留 1 位小数（0.1 K 精度）。</summary>
        public int SliderDecimalPlaces(int index) => 1;

        public float GetSliderMin(int index) => GameUtil.GetConvertedTemperature(RANGE_MIN);

        public float GetSliderMax(int index) => GameUtil.GetConvertedTemperature(RANGE_MAX);

        public float GetSliderValue(int index) => GameUtil.GetConvertedTemperature(targetTemperature);

        /// <summary>滑块/数字框改动时回写目标温度；输入是显示单位（°C/°F/K）。</summary>
        public void SetSliderValue(float value, int index)
        {
            targetTemperature = Mathf.Clamp(
                GameUtil.GetTemperatureConvertedToKelvin(value), RANGE_MIN, RANGE_MAX);
        }

        public string GetSliderTooltipKey(int index) => SLIDER_TOOLTIP_KEY;

        public string GetSliderTooltip(int index) => STRINGS.UI.SMARTCHILLER.TOOLTIP;
    }
}
