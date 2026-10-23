using UnityEngine;

namespace RingCloudTech
{
    public class HarmonyEyeInfo : KMonoBehaviour
    {
        [MyCmpReq] private Storage storage;
        private StatusItem item;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            try
            {
                Strings.Add("STRINGS.BUILDING.STATUSITEMS.HARMONYEYE_H2.NAME", "氢气储量");
                Strings.Add("STRINGS.BUILDING.STATUSITEMS.HARMONYEYE_H2.TOOLTIP", "选择持续模式以保证储存的气体不会自动排除");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 状态文字注册失败（不影响运行）: " + e.Message);
            }
            item = new StatusItem("HARMONYEYE_H2", "BUILDING", "", StatusItem.IconType.Info,
                                  NotificationType.Neutral, false, OverlayModes.None.ID);
            item.resolveStringCallback = (str, data) =>
            {
                Tag h2 = ElementLoader.FindElementByHash(SimHashes.Hydrogen).tag;
                float have = storage.GetMassAvailable(h2);
                float need = HarmonyEyeConfig.HYDROGEN_KG;
                return string.Format("{0:F1} / {1:F0} 吨氢气", have / 1000f, need / 1000f);
            };
            GetComponent<KSelectable>().AddStatusItem(item, null);
        }
    }
}