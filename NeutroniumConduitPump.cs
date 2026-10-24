using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 中子质液体/气体管道的「旁路搬运」。
    ///
    /// 原版 ConduitFlow 的每格容量是单个全局字段 MaxMass（液体 10 kg、气体 1 kg），
    /// 流量模拟每 tick 都用它算有效容量，所以单纯在 AddElement 时抬 MaxMass 只能灌进去、运不动。
    ///
    /// 这里不复刻那套模拟，而是：把中子质管道本身当成一层「水库网络」，
    /// 每 tick 按「离出口的距离」做一次梯度搬运，出口就是把质量塞回与它相邻的原版管道格。
    /// 原版只管它自己那 10 kg 的边界，中转全部由我们自己的容量（5 t / 1 t）承担。
    /// </summary>
        // 中子质管道整体禁用：[HarmonyPatch(typeof(ConduitFlow), "Sim200ms")]
    public static class NeutroniumConduitPump
    {
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        private static void Postfix(ConduitFlow __instance, float dt)
        {
            try
            {
                if (NeutroniumConduitRegistry.Count == 0)
                {
                    return;
                }

                ConduitType type;
                if (__instance == Game.Instance.liquidConduitFlow)
                {
                    type = ConduitType.Liquid;
                }
                else if (__instance == Game.Instance.gasConduitFlow)
                {
                    type = ConduitType.Gas;
                }
                else
                {
                    return;
                }

                List<int> cells = NeutroniumConduitRegistry.CellsOf(type);
                if (cells == null || cells.Count == 0)
                {
                    return;
                }

                float capacity = NeutroniumConduitRegistry.MaxCapacity(type);

                // 1. 出口格：自己的管格紧挨着原版同类型管道
                Dictionary<int, int> distance = new Dictionary<int, int>();
                Queue<int> queue = new Queue<int>();
                foreach (int cell in cells)
                {
                    if (HasVanillaNeighbour(__instance, cell))
                    {
                        distance[cell] = 0;
                        queue.Enqueue(cell);
                    }
                }

                if (queue.Count == 0)
                {
                    return;
                }

                // 2. 在自己的管格图上 BFS 出到出口的步数
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue();
                    int next = distance[cell] + 1;
                    for (int i = 0; i < 4; i++)
                    {
                        int n = Grid.OffsetCell(cell, Dx[i], Dy[i]);
                        if (!NeutroniumConduitRegistry.TryGet(n, type, out float _) || distance.ContainsKey(n))
                        {
                            continue;
                        }

                        distance[n] = next;
                        queue.Enqueue(n);
                    }
                }

                // 3. 由远及近搬：每格往「距离 -1」的邻居倒
                List<int> ordered = new List<int>(distance.Keys);
                ordered.Sort((a, b) => distance[b].CompareTo(distance[a]));

                foreach (int cell in ordered)
                {
                    int d = distance[cell];
                    if (d == 0)
                    {
                        continue;
                    }

                    var from = __instance.GetContents(cell);
                    if (from.mass <= 0.01f)
                    {
                        continue;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        int n = Grid.OffsetCell(cell, Dx[i], Dy[i]);
                        if (!distance.TryGetValue(n, out int nd) || nd != d - 1)
                        {
                            continue;
                        }

                        var to = __instance.GetContents(n);
                        if (to.mass > 0.01f && to.element != from.element)
                        {
                            continue;
                        }

                        float room = capacity - to.mass;
                        if (room <= 0.01f)
                        {
                            continue;
                        }

                        float moved = Mathf.Min(from.mass, room);
                        float left = __instance.AddElement(n, from.element, moved, from.temperature, byte.MaxValue, 0);
                        if (left < moved)
                        {
                            float done = moved - left;
                            __instance.SetContents(cell, new ConduitFlow.ConduitContents(from.element, from.mass - done, from.temperature, byte.MaxValue, 0));
                            if (from.mass <= 0.01f)
                            {
                                break;
                            }
                        }
                    }
                }

                // 4. 出口格往外倒给原版管道（原版自己管 10 kg 上限）
                foreach (int cell in cells)
                {
                    if (!distance.TryGetValue(cell, out int d) || d != 0)
                    {
                        continue;
                    }

                    var contents = __instance.GetContents(cell);
                    if (contents.mass <= 0.01f)
                    {
                        continue;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        int n = Grid.OffsetCell(cell, Dx[i], Dy[i]);
                        if (NeutroniumConduitRegistry.TryGet(n, type, out float _) || !__instance.HasConduit(n))
                        {
                            continue;
                        }

                        // 只喂到原版管道「不超压」的量：液体 5 kg、气体 0.5 kg 每次
                        float pushLimit = type == ConduitType.Liquid ? 5f : 0.5f;
                        var target = __instance.GetContents(n);
                        float push = Mathf.Min(contents.mass, Mathf.Max(0f, pushLimit - target.mass));
                        if (push <= 0.01f)
                        {
                            continue;
                        }

                        float left = __instance.AddElement(n, contents.element, push, contents.temperature, byte.MaxValue, 0);
                        float done = push - left;
                        if (done > 0f)
                        {
                            __instance.SetContents(cell, new ConduitFlow.ConduitContents(contents.element, contents.mass - done, contents.temperature, byte.MaxValue, 0));
                        }

                        if (contents.mass <= 0.01f)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RingCloudTech] 中子质管道搬运异常: " + ex.Message);
            }
        }

        private static bool HasVanillaNeighbour(ConduitFlow flow, int cell)
        {
            for (int i = 0; i < 4; i++)
            {
                int n = Grid.OffsetCell(cell, Dx[i], Dy[i]);
                if (flow.HasConduit(n) && !NeutroniumConduitRegistry.TryGet(n, flow.conduitType, out float _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
