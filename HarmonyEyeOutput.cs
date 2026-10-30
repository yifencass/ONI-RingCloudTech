using KSerialization;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼的产物出口 —— 先进机器内部缓冲，再按速率灌进 ALN 频道。
    ///
    /// 为什么不能走原版：Melted 分支调 Storage.AddLiquid →
    /// LiquidSourceManager.CreateChunk → GeneratedOre.CreateChunk →
    /// Assets.GetPrefab(element.tag)，熔融金属/岩浆没有液滴 prefab，
    /// GetPrefab 返回 null 后 KInstantiate 抛 NullReferenceException。
    /// 频道存的是「Tag + 质量」，不需要 prefab，正好绕开这个坑。
    ///
    /// 为什么不再「一次全推」：一次配方 13~23 种 × 50 吨 = 650~1150 吨，
    /// 远超网络容量。现在的顺序是：
    ///   1. 产物入队（机器内部缓冲，随存档保存）；
    ///   2. 每秒检查频道是否已满；
    ///   3. 没满就按 output_rate 一点点推进去，推不动就留到下秒。
    /// </summary>
    public class HarmonyEyeOutput : KMonoBehaviour, ISim1000ms
    {
        /// <summary>队列里还剩多少 kg（UI/日志用）。</summary>
        public float PendingMass { get; private set; }

        /// <summary>产物队列：用三个平行数组，KSerialization 对它最稳。</summary>
        [Serialize] private string[] queueTags = new string[0];
        [Serialize] private float[] queueMasses = new float[0];
        [Serialize] private float[] queueTemps = new float[0];

        private HarmonyEyeAlnBridge bridge;
        private bool loggedNoChannel;
        private float lastFullLogTime;
        private bool loggedFull;

        /// <summary>产物入队，由 SpawnOrderProduct 补丁调用。</summary>
        public void Enqueue(Element element, float mass, float temp)
        {
            if (element == null || mass <= 0f)
            {
                return;
            }

            string tagName = element.tag.ToString();
            int count = queueTags.Length;

            for (int i = 0; i < count; i++)
            {
                if (queueTags[i] == tagName)
                {
                    queueMasses[i] += mass;
                    queueTemps[i] = temp;
                    PendingMass += mass;
                    return;
                }
            }

            string[] tags = new string[count + 1];
            float[] masses = new float[count + 1];
            float[] temps = new float[count + 1];
            System.Array.Copy(queueTags, tags, count);
            System.Array.Copy(queueMasses, masses, count);
            System.Array.Copy(queueTemps, temps, count);
            tags[count] = tagName;
            masses[count] = mass;
            temps[count] = temp;
            queueTags = tags;
            queueMasses = masses;
            queueTemps = temps;
            PendingMass += mass;
        }

        public void Sim1000ms(float dt)
        {
            if (queueTags.Length == 0)
            {
                return;
            }

            if (bridge == null)
            {
                bridge = GetComponent<HarmonyEyeAlnBridge>();
            }

            if (bridge == null || !bridge.HasChannel || !HarmonyEyeAln.Available)
            {
                if (!loggedNoChannel)
                {
                    loggedNoChannel = true;
                    Debug.Log("[RingCloudTech] 鸿蒙之眼产物暂存于机器内（尚未接入 ALN 频道），累计 " +
                              Mathf.RoundToInt(PendingMass) + " kg");
                }

                return;
            }

            loggedNoChannel = false;

            // 网络满了就等下一轮，不做无谓的 Deposit 调用
            if (HarmonyEyeAln.IsChannelFull(bridge.Channel))
            {
                if (!loggedFull || Time.unscaledTime - lastFullLogTime > 60f)
                {
                    loggedFull = true;
                    lastFullLogTime = Time.unscaledTime;
                    Debug.Log("[RingCloudTech] ALN 频道 " + bridge.Channel + " 已满，" +
                              "鸿蒙之眼滞留 " + Mathf.RoundToInt(PendingMass) + " kg 产物等待空位");
                }

                return;
            }

            loggedFull = false;

            int cell = Grid.PosToCell(gameObject);
            float budget = HarmonyEyeSettings.OutputRate;

            for (int i = 0; i < queueTags.Length && budget > 0.01f; i++)
            {
                Tag tag = TagManager.Create(queueTags[i]);
                float want = Mathf.Min(queueMasses[i], budget);
                float stored = bridge.Deposit(tag, want, cell, queueTemps[i]);

                if (stored > 0f)
                {
                    queueMasses[i] -= stored;
                    PendingMass -= stored;
                    budget -= stored;
                }

                // 这一次灌不进去，说明网络刚满/没电，本轮到此为止
                if (stored <= 0f)
                {
                    break;
                }

                if (queueMasses[i] <= 0.01f)
                {
                    RemoveAt(i);
                    i--;
                }
            }
        }

        private void RemoveAt(int index)
        {
            int count = queueTags.Length;
            if (count <= 1)
            {
                queueTags = new string[0];
                queueMasses = new float[0];
                queueTemps = new float[0];
                PendingMass = 0f;
                return;
            }

            string[] tags = new string[count - 1];
            float[] masses = new float[count - 1];
            float[] temps = new float[count - 1];
            for (int i = 0, j = 0; i < count; i++)
            {
                if (i == index)
                {
                    continue;
                }

                tags[j] = queueTags[i];
                masses[j] = queueMasses[i];
                temps[j] = queueTemps[i];
                j++;
            }

            queueTags = tags;
            queueMasses = masses;
            queueTemps = temps;
        }
    }
}
