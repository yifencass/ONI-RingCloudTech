using System.Collections.Generic;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 把非氢气的气体从鸿蒙之眼的输入箱里清出去。
    ///
    /// 为什么需要：ConduitConsumer.Consume 里有一段无条件伤害逻辑——
    ///   if (num &gt; 0f &amp;&amp; capacityTag != GameTags.Any &amp;&amp; !element.HasTag(capacityTag))
    ///       BoxingTrigger(-794517298, new BuildingHP.DamageSourceInfo { damage = 1, ... });
    /// 只要吞进一包不是 capacityTag 的气体，每吞一次就砸 1 点血；而错误气体一旦进了
    /// inStorage 就再也出不去（配方只认氢气），于是变成「持续损坏 + 修不回来」的死循环。
    ///
    /// 拦入口的补丁在 RingCloudTech.cs 的 Patch_ConduitConsumer_Consume；
    /// 这个组件负责清理旧存档里已经灌进去的残气。
    /// </summary>
    public class HarmonyEyeGasPurge : KMonoBehaviour
    {
        private const float INTERVAL = 2f;

        private ComplexFabricator fabricator;
        private float timer;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            fabricator = GetComponent<ComplexFabricator>();
            timer = INTERVAL;   // 落地立刻清一次，旧存档的残气不用等
            Debug.Log("[RingCloudTech] 鸿蒙之眼残气清理组件已挂载");
        }

        private void Update()
        {
            if (fabricator == null || fabricator.inStorage == null)
            {
                return;
            }

            timer += Time.deltaTime;
            if (timer < INTERVAL)
            {
                return;
            }

            timer = 0f;

            try
            {
                Purge(fabricator.inStorage);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 鸿蒙之眼残气清理失败: " + e.Message);
            }
        }

        private static void Purge(Storage storage)
        {
            List<GameObject> items = storage.items;
            if (items == null || items.Count == 0)
            {
                return;
            }

            Tag hydrogen = ElementLoader.FindElementByHash(SimHashes.Hydrogen).tag;
            List<Tag> wrong = null;

            for (int i = 0; i < items.Count; i++)
            {
                GameObject item = items[i];
                if (item == null)
                {
                    continue;
                }

                KPrefabID id = item.GetComponent<KPrefabID>();
                if (id == null || id.PrefabTag == hydrogen)
                {
                    continue;
                }

                if (wrong == null)
                {
                    wrong = new List<Tag>();
                }

                wrong.Add(id.PrefabTag);
            }

            if (wrong == null)
            {
                return;
            }

            for (int i = 0; i < wrong.Count; i++)
            {
                Debug.Log("[RingCloudTech] 鸿蒙之眼清出错误气体: " + wrong[i]);
                storage.Drop(wrong[i]);
            }
        }
    }
}
