using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 把 SolidConduitDispenser 的 storage 纠正为 ComplexFabricator.outStorage。
    ///
    /// 原因：SolidConduitDispenser.storage 标了 [MyCmpReq]，运行时会被自动注入成
    /// 建筑上「第一个」Storage —— 对本建筑来说很可能是 inStorage。
    /// 我们在 OnSpawn 里（一定晚于 MyCmpReq 注入）重新指过去。
    /// </summary>
    public class HarmonyEyeSolidDispenser : KMonoBehaviour
    {
        protected override void OnSpawn()
        {
            base.OnSpawn();

            try
            {
                ComplexFabricator fabricator = GetComponent<ComplexFabricator>();
                SolidConduitDispenser dispenser = GetComponent<SolidConduitDispenser>();
                if (fabricator != null && dispenser != null && fabricator.outStorage != null)
                {
                    dispenser.storage = fabricator.outStorage;
                    dispenser.solidOnly = true;
                    dispenser.useSecondaryOutput = true;
                    dispenser.alwaysDispense = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 鸿蒙之眼固体输出口初始化失败: " + e.Message);
            }
        }
    }
}
