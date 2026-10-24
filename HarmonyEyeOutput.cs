using System.Collections.Generic;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼的产物输出。
    ///
    /// 原版 ComplexFabricator 的 Melted 分支调用 Storage.AddLiquid，而
    /// Storage.AddLiquid 会走 LiquidSourceManager.CreateChunk → GeneratedOre.CreateChunk，
    /// 后者用 Assets.GetPrefab(element.tag) 取液滴 prefab。熔融金属/岩浆这类元素
    /// 从来没有液滴 prefab，于是 GetPrefab 返回 null，KInstantiate 抛
    /// NullReferenceException —— 这就是熔融产物崩溃的根因。
    ///
    /// 因此液体/气体产物不进箱子：液体直接写进液体输出管道
    /// （ConduitFlow.AddElement 不需要任何 prefab），管道没接时浇到世界格上；
    /// 气体没有输出管道，直接放到建筑上方那格。固体仍然走箱子。
    /// </summary>
    public class HarmonyEyeOutput : KMonoBehaviour, ISim200ms
    {
        /// <summary>每 200ms 每个条目最多排出的质量，避免一格瞬间超压。</summary>
        private const float MAX_PER_TICK = 5000f;

        private struct Pending
        {
            public SimHashes hash;
            public float mass;
            public float temp;
            public bool isGas;
            public bool isSolid;
        }

        private readonly List<Pending> pending = new List<Pending>();

        public void Enqueue(Element element, float mass, float temp)
        {
            if (element == null || mass <= 0f)
            {
                return;
            }

            pending.Add(new Pending
            {
                hash = element.id,
                mass = mass,
                temp = temp,
                isGas = element.IsGas,
                isSolid = element.IsSolid,
            });
        }

        public void Sim200ms(float dt)
        {
            if (pending.Count == 0)
            {
                return;
            }

            int baseCell = Grid.PosToCell(gameObject);
            if (!Grid.IsValidCell(baseCell))
            {
                return;
            }

            int liquidCell = Grid.OffsetCell(baseCell, HarmonyEyeConfig.LIQUID_OUT_PORT);
            int gasCell = Grid.OffsetCell(baseCell, HarmonyEyeConfig.GAS_OUT_PORT);
            int solidCell = Grid.OffsetCell(baseCell, HarmonyEyeConfig.SOLID_OUT_PORT);
            ConduitFlow liquids = Game.Instance != null ? Game.Instance.liquidConduitFlow : null;
            ConduitFlow gases = Game.Instance != null ? Game.Instance.gasConduitFlow : null;
            bool hasPipe = liquids != null && Grid.IsValidCell(liquidCell) && liquids.HasConduit(liquidCell);
            bool hasGasPipe = gases != null && Grid.IsValidCell(gasCell) && gases.HasConduit(gasCell);

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending p = pending[i];
                float left = p.mass;

                if (p.isGas)
                {
                    // 气体：只从「O 气体出」那格的气体管道走
                    if (!hasGasPipe)
                    {
                        continue;
                    }
                    float chunk = Mathf.Min(left, MAX_PER_TICK);
                    left -= gases.AddElement(gasCell, p.hash, chunk, p.temp, byte.MaxValue, 0);
                }
                else if (p.isSolid)
                {
                    // 没有实体块 prefab 的固体：堆到「I 物品出」那格
                    float chunk = Mathf.Min(left, MAX_PER_TICK);
                    SimMessages.AddRemoveSubstance(
                        Grid.IsValidCell(solidCell) ? solidCell : baseCell, p.hash,
                        CellEventLogger.Instance.FabricatorProduceMelted,
                        chunk, p.temp, byte.MaxValue, 0);
                    left -= chunk;
                }
                else
                {
                    // 液体：沿「L 液体出」那格所在的管道网络找格子。
                    // 一格管道只能装一种元素，13 种熔融产物挤同一格时，
                    // 后面 12 种永远进不去 —— 必须分散到网络里的不同格。
                    if (!hasPipe)
                    {
                        continue;
                    }
                    float chunk = Mathf.Min(left, MAX_PER_TICK);
                    left -= PushLiquid(liquids, liquidCell, p.hash, chunk, p.temp);
                }

                if (left > 0.01f)
                {
                    p.mass = left;
                    pending[i] = p;
                }
                else
                {
                    pending.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 从出口格开始沿管道网络 BFS，把质量分摊到「空着」或「装的正是这种元素」的格子上。
        /// 返回实际写进去的质量。
        /// </summary>
        private static float PushLiquid(ConduitFlow flow, int start, SimHashes hash, float mass, float temp)
        {
            if (mass <= 0f || !Grid.IsValidCell(start) || !flow.HasConduit(start))
            {
                return 0f;
            }

            float left = mass;
            int visitedLimit = 24;

            Queue<int> queue = new Queue<int>();
            HashSet<int> seen = new HashSet<int>();
            queue.Enqueue(start);
            seen.Add(start);

            int guard = 0;
            while (queue.Count > 0 && left > 0.01f && guard < visitedLimit)
            {
                int cell = queue.Dequeue();
                guard++;

                ConduitFlow.ConduitContents contents = flow.GetContents(cell);
                bool same = contents.element == hash;
                bool empty = contents.element == SimHashes.Vacuum;
                if (same || empty)
                {
                    left -= flow.AddElement(cell, hash, left, temp, byte.MaxValue, 0);
                }

                // 继续沿相邻的管道格扩散
                int[] neighbours =
                {
                    Grid.CellLeft(cell), Grid.CellRight(cell),
                    Grid.CellAbove(cell), Grid.CellBelow(cell),
                };
                for (int i = 0; i < neighbours.Length; i++)
                {
                    int n = neighbours[i];
                    if (Grid.IsValidCell(n) && !seen.Contains(n) && flow.HasConduit(n))
                    {
                        seen.Add(n);
                        queue.Enqueue(n);
                    }
                }
            }

            return mass - left;
        }
    }
}
