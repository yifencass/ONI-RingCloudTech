using System.Collections.Generic;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼的产物出口 —— 直接写进 ALN 频道。
    ///
    /// 为什么不能走原版：Melted 分支会调 Storage.AddLiquid，
    /// 进而走 LiquidSourceManager.CreateChunk → GeneratedOre.CreateChunk →
    /// Assets.GetPrefab(element.tag)，而熔融金属/岩浆没有液滴 prefab，
    /// GetPrefab 返回 null 后 KInstantiate 抛 NullReferenceException。
    /// 频道存的是「Tag + 质量」，不需要任何 prefab，正好绕开这个坑。
    ///
    /// 之前这层是「攒着 → 往管道/世界格写」，现在改成「攒着 → 往频道写」。
    /// 频道满（或没接上频道）时留在队列里慢慢重试，不丢产物。
    /// </summary>
    public class HarmonyEyeOutput : KMonoBehaviour, ISim200ms
    {
        /// <summary>每 200ms 每个条目最多存入的质量，避免一次性把频道压满。</summary>
        private const float MAX_PER_TICK = 5000f;

        private struct Pending
        {
            public Tag tag;
            public float mass;
            public float temp;
        }

        private readonly List<Pending> pending = new List<Pending>();

        private HarmonyEyeAlnBridge bridge;
        private bool loggedNoChannel;

        /// <summary>产物入队，由 SpawnOrderProduct 补丁调用。</summary>
        public void Enqueue(Element element, float mass, float temp)
        {
            if (element == null || mass <= 0f)
            {
                return;
            }

            pending.Add(new Pending
            {
                tag = element.tag,
                mass = mass,
                temp = temp,
            });
        }

        public void Sim200ms(float dt)
        {
            if (pending.Count == 0)
            {
                return;
            }

            if (bridge == null)
            {
                bridge = GetComponent<HarmonyEyeAlnBridge>();
            }

            if (bridge == null || !bridge.HasChannel)
            {
                if (!loggedNoChannel)
                {
                    loggedNoChannel = true;
                    Debug.Log("[RingCloudTech] 鸿蒙之眼产物暂存（尚未接入 ALN 频道），累计 " +
                              pending.Count + " 项");
                }

                return;
            }

            loggedNoChannel = false;
            int cell = Grid.PosToCell(gameObject);

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending p = pending[i];
                float chunk = Mathf.Min(p.mass, MAX_PER_TICK);
                float stored = bridge.Deposit(p.tag, chunk, cell, p.temp);

                p.mass -= stored;

                if (p.mass <= 0.01f)
                {
                    pending.RemoveAt(i);
                }
                else
                {
                    pending[i] = p;
                }

                // 一点都存不进去，说明频道满了或没电，这一轮不用再试别的条目。
                if (stored <= 0f)
                {
                    break;
                }
            }
        }
    }
}
