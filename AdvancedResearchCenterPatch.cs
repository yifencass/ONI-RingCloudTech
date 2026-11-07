using System;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 原版高级研究台（AdvancedResearchCenter，3×3）靠 ManualDeliveryKG 让小人搬水。
    /// 这里给它补一个液体管道输入口，位置按玩家给的图放在底排中间：
    ///
    ///     XXX        y = 2
    ///     XXX        y = 1
    ///     XIX        y = 0，I = (0, 0)
    ///
    /// 3×3 建筑的锚点格就是底排中间，所以 UtilityInputOffset = (0, 0)。
    /// 原版 PowerInputOffset 也是 (0, 0)，但电力登记在 Grid.Objects[cell, 29]、
    /// 液体管道在 Grid.Objects[cell, LiquidConduit]，两者不同层，同格可共存；
    /// 刻意不挪电力口，避免老存档里已经拉好的电线断连。
    ///
    /// ResearchCenter.HasMaterial() 只看 storage.MassStored() &gt; 0，
    /// ElementConverter 按 Water 5/6 kg/s 从同一个 Storage 扣料，
    /// 所以 ConduitConsumer 直接把水灌进这个 Storage 就能工作，不用改研究逻辑。
    /// </summary>
    [HarmonyPatch(typeof(AdvancedResearchCenterConfig), "CreateBuildingDef")]
    public static class AdvancedResearchCenter_CreateBuildingDef_Patch
    {
        public static void Postfix(BuildingDef __result)
        {
            try
            {
                if (__result == null)
                {
                    return;
                }

                __result.InputConduitType = ConduitType.Liquid;
                __result.UtilityInputOffset = new CellOffset(0, 0);
                __result.ViewMode = OverlayModes.LiquidConduits.ID;

                Debug.Log("[RingCloudTech] 高级研究台已启用液体管道输入（底排中间）");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 高级研究台液体口配置失败: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(AdvancedResearchCenterConfig), "ConfigureBuildingTemplate")]
    public static class AdvancedResearchCenter_ConfigureBuildingTemplate_Patch
    {
        public static void Postfix(GameObject go)
        {
            try
            {
                Storage storage = go.GetComponent<Storage>();
                if (storage == null)
                {
                    Debug.LogWarning("[RingCloudTech] 高级研究台没有 Storage，跳过液体输入");
                    return;
                }

                // ConduitConsumer.storage 默认是 null，必须显式指向研究台自己的 Storage。
                ConduitConsumer consumer = go.AddOrGet<ConduitConsumer>();
                consumer.conduitType = ConduitType.Liquid;
                consumer.storage = storage;
                consumer.capacityTag = ElementLoader.FindElementByHash(SimHashes.Water).tag;
                consumer.capacityKG = 750f;          // 与 ManualDeliveryKG.capacity 一致
                consumer.consumptionRate = 2f;       // 研究台只吃 5/6 kg/s，留足余量
                consumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
                consumer.forceAlwaysSatisfied = true; // 没接管道时不干扰原版工作判定

                // ManualDeliveryKG 刻意保留：管道把库存维持在 750 kg 时不会低于
                // refillMass(150 kg)，小人不会再被派去搬水；没接管道时它仍是后备。
                Debug.Log("[RingCloudTech] 高级研究台已接入 ConduitConsumer");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 高级研究台 ConduitConsumer 配置失败: " + e);
            }
        }
    }
}
