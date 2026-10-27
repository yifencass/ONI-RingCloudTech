using KSerialization;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼 × ALN 的桥接组件。
    ///
    /// 职责只有两件：
    ///   1. **进气**：每秒从 `channelID` 指定的频道取氢气，补进 `ComplexFabricator.inStorage`。
    ///      原来是 ConduitConsumer 走气体管道，现在管道口已移除，完全由频道供给。
    ///   2. **出料**：给 HarmonyEyeOutput 提供 `Deposit`，把配方产物写回同一个频道。
    ///
    /// 频道由玩家在详情面板的「ALN 频道」页签里选（用的是 ALN 自带的频道下拉），
    /// 选择结果存在 `channelID` 里、随存档保存。没选之前不接任何频道。
    ///
    /// **电力不托管**：本组件完全不碰 ALN 的能量池，鸿蒙之眼仍然吃电网的 20 kW。
    /// </summary>
    public class HarmonyEyeAlnBridge : KMonoBehaviour, ISim1000ms
    {
        /// <summary>每秒最多从频道搬多少氢气进箱（kg）。</summary>
        private const float PULL_PER_SECOND = 5000f;

        /// <summary>玩家选定的频道；-1 表示尚未接入。</summary>
        [Serialize]
        public int channelID = -1;

        private ComplexFabricator fabricator;
        private Tag hydrogenTag;

        public bool HasChannel
        {
            get { return channelID > 0; }
        }

        public int Channel
        {
            get { return channelID; }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            fabricator = GetComponent<ComplexFabricator>();

            Element hydrogen = ElementLoader.FindElementByHash(SimHashes.Hydrogen);
            hydrogenTag = hydrogen != null ? hydrogen.tag : Tag.Invalid;

            HarmonyEyeAln.Init();
            HarmonyEyeAln.SuppressAutoRequester(gameObject);

            if (channelID > 0)
            {
                Debug.Log("[RingCloudTech] 鸿蒙之眼已接入 ALN 频道 " + channelID + "（存档恢复）");
            }
        }

        /// <summary>由频道侧屏调用。</summary>
        public void SetChannel(int channel)
        {
            if (channel == channelID)
            {
                return;
            }

            channelID = channel;
            Debug.Log("[RingCloudTech] 鸿蒙之眼切换到 ALN 频道 " + channelID);
        }

        public void Sim1000ms(float dt)
        {
            if (!HarmonyEyeAln.Available || channelID <= 0)
            {
                return;
            }

            try
            {
                PullHydrogen();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 鸿蒙之眼从 ALN 取氢失败: " + e.Message);
            }
        }

        /// <summary>把产物写进频道，返回实际存入的质量。</summary>
        public float Deposit(Tag tag, float mass, int cell, float temperature)
        {
            return HarmonyEyeAln.Deposit(channelID, tag, mass, cell, temperature);
        }

        private void PullHydrogen()
        {
            if (fabricator == null || fabricator.inStorage == null || !hydrogenTag.IsValid)
            {
                return;
            }

            Storage inStorage = fabricator.inStorage;
            float room = inStorage.capacityKg - inStorage.MassStored();
            if (room <= 1f)
            {
                return;
            }

            float want = Mathf.Min(room, PULL_PER_SECOND);
            float available = HarmonyEyeAln.GetAmount(channelID, hydrogenTag);
            if (available <= 0.01f)
            {
                return;
            }

            HarmonyEyeAln.Withdraw(channelID, hydrogenTag, Mathf.Min(want, available), inStorage,
                gameObject.name);
        }
    }
}
