using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 咖啡树收获时额外掉落木材。
    ///
    /// 咖啡树大部分生长期都在长木质茎，所以收获时除了咖啡豆还给一份木材。
    /// 挂钩点是 <see cref="Harvestable"/> 在 <c>Harvest()</c> 里触发的
    /// <c>GameHashes.Harvest</c>（原版 <c>Harvestable.cs</c> 里是
    /// <c>Trigger(1272413801, this)</c>，1272413801 = <c>GameHashes.Harvest</c>）。
    ///
    /// 注意：拔除（<c>OnUprooted</c>）在可收获状态下也会走 <c>Harvest()</c>，
    /// 因此拔除同样会给木材 —— 和乔木树一致。
    /// </summary>
    public class CoffeeTreeWoodYield : KMonoBehaviour
    {
        protected override void OnSpawn()
        {
            base.OnSpawn();
            Subscribe((int)GameHashes.Harvest, OnHarvested);
        }

        private void OnHarvested(object data)
        {
            try
            {
                Element wood = ElementLoader.FindElementByHash(SimHashes.WoodLog);
                if (wood == null || wood.substance == null)
                {
                    return;
                }

                PrimaryElement pe = GetComponent<PrimaryElement>();
                float temperature = pe != null ? pe.Temperature : 293.15f;

                wood.substance.SpawnResource(
                    gameObject.transform.GetPosition(),
                    CoffeeTreeRegistrar.WOOD_ON_HARVEST,
                    temperature,
                    0,
                    0);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] CoffeeTree wood yield failed: " + e.Message);
            }
        }
    }
}
