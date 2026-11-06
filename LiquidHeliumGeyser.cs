using System;
using System.Collections.Generic;
using HarmonyLib;
using Klei;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 液氦泉的注册参数。
    ///
    /// 原版注册链路（反编译 GeyserGenericConfig / GeyserConfigurator 确认）：
    ///   1. GeyserGenericConfig.GenerateConfigs() 生成 List&lt;GeyserPrefabParams&gt;；
    ///   2. 每构造一个 GeyserConfigurator.GeyserType，构造函数会把自己塞进静态
    ///      geyserTypes 列表（FindType 按 id 查它）；
    ///   3. CreatePrefabs() 为每个参数建一个 id = "GeyserGeneric_" + type.id 的 prefab；
    ///   4. isGenericGeyser = true 的参数留在列表里，交给 "GeyserGeneric" 随机生成器，
    ///      世界生成时按 globalWorldSeed 抽一个实例化 —— 这就是自然生成的入口。
    /// 所以只需 Postfix 往 GenerateConfigs 的结果里 Add 一项即可完整注册。
    /// </summary>
    public static class LiquidHeliumGeyser
    {
        public const string ID = "liquid_helium";
        public const string PREFAB_ID = "GeyserGeneric_" + ID;

        // 原版 elements/liquid.yaml：LiquidHelium 的 highTemp = 4.22 K，
        // 高于它就相变成 Helium。喷发温度取 4.15 K，保证出泉口时是液体，
        // 一接触常温环境立刻汽化 —— 同时拿到极强冷源与氦气（正好喂时间之弓）。
        private const float ERUPTION_TEMPERATURE = 4.15f;

        // 参数模板照抄原版 liquid_co2 泉（同样是「喷出即汽化」的低温液体泉），
        // 只把速率调高一点，让冷量/氦气产量对得起「十分有用」。
        private const float MIN_RATE_PER_CYCLE = 300f;
        private const float MAX_RATE_PER_CYCLE = 600f;
        private const float MAX_PRESSURE = 50f;

        // 是否加入世界生成的随机间歇泉池。改成 false 仍可用调试/沙盒按 PREFAB_ID 放置。
        private const bool ADD_TO_RANDOM_POOL = true;

        /// <summary>把液氦泉追加进原版间歇泉表。</summary>
        [HarmonyPatch(typeof(GeyserGenericConfig), "GenerateConfigs")]
        public static class GeyserGenericConfig_GenerateConfigs_Patch
        {
            public static void Postfix(List<GeyserGenericConfig.GeyserPrefabParams> __result)
            {
                try
                {
                    if (__result == null)
                    {
                        return;
                    }

                    // 幂等：GenerateConfigs 可能在一次进程里被重复调用，
                    // 而 GeyserType 的构造函数会自动往静态列表里 Add，不能重复构造。
                    foreach (GeyserGenericConfig.GeyserPrefabParams p in __result)
                    {
                        if (p.id == PREFAB_ID)
                        {
                            return;
                        }
                    }

                    GeyserConfigurator.GeyserType type = new GeyserConfigurator.GeyserType(
                        ID,
                        SimHashes.LiquidHelium,
                        GeyserConfigurator.GeyserShape.Liquid,
                        ERUPTION_TEMPERATURE,
                        MIN_RATE_PER_CYCLE,
                        MAX_RATE_PER_CYCLE,
                        MAX_PRESSURE,
                        null,                       // requiredDlcIds：不限制 DLC
                        null,                       // forbiddenDlcIds
                        60f, 1140f, 0.1f, 0.9f,    // 喷发/休眠周期
                        15000f, 135000f, 0.4f, 0.8f, // 活跃年周期
                        ERUPTION_TEMPERATURE);      // 建筑本体温度

                    // 外观借用原版液态 CO2 泉的 kanim（同为 4×2 低温液体泉），
                    // 不新增美术资源；原版自己也有两个类型共用一张 kanim 的先例。
                    __result.Add(new GeyserGenericConfig.GeyserPrefabParams(
                        "geyser_liquid_co2_kanim", 4, 2, type, ADD_TO_RANDOM_POOL));

                    // 地理调谐器对没登记的间歇泉会打 DevLogError 再回退默认档，
                    // 这里显式登记 DEFAULT 档，行为可预期、日志干净。
                    GeoTunerConfig.geotunerGeyserSettings[new HashedString(ID)] =
                        GeoTunerConfig.CategorySettings[GeoTunerConfig.Category.DEFAULT_CATEGORY];

                    Debug.Log("[RingCloudTech] 已注册间歇泉 " + PREFAB_ID);
                }
                catch (Exception e)
                {
                    Debug.LogError("[RingCloudTech] 注册液氦泉失败: " + e);
                }
            }
        }

        /// <summary>
        /// 原版把 Helium / LiquidHelium 标成 isDisabled。反编译确认这个标记只影响
        /// 数据库、元素过滤器、材料选择器和沙盒刷子（Element.disabled），Sim 照常模拟。
        /// 但玩家若要在气体过滤器里选中氦气送给时间之弓，元素必须在过滤器中可见，
        /// 所以加载完元素表后把这两个的 disabled 放开。
        /// </summary>
        [HarmonyPatch(typeof(ElementLoader), "Load")]
        public static class ElementLoader_Load_EnableHelium_Patch
        {
            public static void Postfix()
            {
                try
                {
                    Enable(SimHashes.LiquidHelium);
                    Enable(SimHashes.Helium);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[RingCloudTech] 启用氦元素失败: " + e);
                }
            }

            private static void Enable(SimHashes hash)
            {
                Element element = ElementLoader.FindElementByHash(hash);
                if (element != null)
                {
                    element.disabled = false;
                }
            }
        }
    }
}
