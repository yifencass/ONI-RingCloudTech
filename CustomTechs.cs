using System;
using System.Collections.Generic;
using System.Reflection;
using Database;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 自建科技节点。
    ///
    /// 原版科技树由 Techs.Init() 建 Tech、再由 Techs.Load(tree 文本) 把节点和前置关系读进来，
    /// 最后 Techs.PostProcess() 把 unlockedItemIDs 映射成 TechItem。
    /// 我们在 Db.Initialize 之后（Load 已跑完、PostProcess 还没跑）插入自己的 Tech：
    ///   - Tech 构造时 base(id, techs, name) 会 parent.Add(this) 自动进资源集，不用手动 Add；
    ///   - 节点（ResourceTreeNode）原版没有公开构造入口，这里直接 new 一个空节点再填字段；
    ///   - 前置关系就是 requiredTech / unlockedTech 两个双向列表；
    ///   - 位置按「前置科技下一排、往右找空位」自动算，不写死坐标。
    /// </summary>
    internal static class CustomTechs
    {
        public const string PassiveEnergyId = "PassiveEnergy";
        public const string PocketUniverseId = "PocketUniverse";

        /// <summary>节点之间的最小行距/列距（原版节点坐标是任意单位，这里只求不重叠）。</summary>
        private const float NodeGap = 40f;

        /// <summary>相对前置科技向右的错开倍数：1 = 正好一整列，2 = 两列（用户要求 2）。</summary>
        private const float X_OFFSET_SCALE = 2f;

        /// <summary>前置科技：多路传输（Multiplexing）与低温燃料推进（CryofuelPropulsion）。</summary>
        private const string PrereqMultiplexing = "MULTIPLEXING";
        private const string PrereqCryofuelPropulsion = "CRYOFUELPROPULSION";

        private static readonly FieldInfo NodeField =
            typeof(Tech).GetField("node", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            installed = true;

            Database.Techs techs = Db.Get()?.Techs;
            if (techs == null)
            {
                Debug.LogWarning("[RingCloudTech] Techs 为空，跳过自建科技");
                return;
            }

            bool so = DlcManager.IsExpansion1Active();

            Tech passive = CreateTech(techs, PassiveEnergyId, so
                ? new Dictionary<string, float>
                {
                    { "basic", 70f }, { "advanced", 100f }, { "orbital", 250f }, { "nuclear", 370f }
                }
                : new Dictionary<string, float> { { "basic", 70f }, { "advanced", 100f } });

            Tech pocket = CreateTech(techs, PocketUniverseId, so
                ? new Dictionary<string, float>
                {
                    { "basic", 200f }, { "advanced", 260f }, { "orbital", 800f }, { "nuclear", 900f }
                }
                : new Dictionary<string, float> { { "basic", 200f }, { "advanced", 260f } });

            // 时间之弓挂在「无源能源」上
            AddItem(passive, TimeBowConfig.ID);

            // 鸿蒙之眼挂在「口袋宇宙」上（没装 ALN 时这个建筑根本不注册）
            if (HarmonyEyeAln.Installed)
            {
                AddItem(pocket, HarmonyEyeConfig.ID);
            }

            Tech multiplexing = FindTech(techs, PrereqMultiplexing);
            Tech cryofuel = FindTech(techs, PrereqCryofuelPropulsion);

            // 无源能源：前置 = 多路传输
            if (multiplexing != null)
            {
                Link(passive, multiplexing);
                BuildNode(passive, multiplexing, techs);
            }
            else
            {
                Debug.LogWarning("[RingCloudTech] 找不到前置科技 " + PrereqMultiplexing + "，无源能源不挂前置");
                BuildNode(passive, null, techs);
            }

            // 口袋宇宙：前置 = 无源能源 + 低温燃料推进
            Link(pocket, passive);
            BuildNode(pocket, passive.FoundNode ? passive : null, techs);

            List<Tech> pocketParents = new List<Tech>();
            if (passive.FoundNode) pocketParents.Add(passive);
            if (cryofuel != null)
            {
                Link(pocket, cryofuel);
                pocketParents.Add(cryofuel);
            }
            else
            {
                Debug.LogWarning("[RingCloudTech] 找不到前置科技 " + PrereqCryofuelPropulsion);
            }

            List<Tech> passiveParents = multiplexing != null ? new List<Tech> { multiplexing } : new List<Tech>();
            AttachEdge(pocket, pocketParents);
            AttachEdge(passive, passiveParents);

            passive.tier = Database.Techs.GetTier(passive);
            pocket.tier = Database.Techs.GetTier(pocket);

            Debug.Log("[RingCloudTech] 自建科技完成：" + PassiveEnergyId + " tier=" + passive.tier
                      + "，" + PocketUniverseId + " tier=" + pocket.tier);
        }

        private static Tech CreateTech(Database.Techs techs, string id, Dictionary<string, float> costs)
        {
            Tech existing = FindByIdOrNull(techs, id);
            if (existing != null) return existing;

            // Tech 构造会从 STRINGS.RESEARCH.TECHS.<ID> 里取名字和描述，
            // 所以 id 必须和 STRINGS.cs / translations/*.po 里的键一致。
            Tech tech = new Tech(id, new List<string>(), techs);

            tech.costsByResearchTypeID.Clear();
            foreach (KeyValuePair<string, float> kv in costs)
            {
                tech.costsByResearchTypeID[kv.Key] = kv.Value;
            }

            return tech;
        }

        private static Tech FindTech(Database.Techs techs, string id)
        {
            return FindByIdOrNull(techs, id);
        }

        /// <summary>
        /// 按 id 找科技。这里刻意不用 techs.Get(id)：ResourceSet.Get 查不到时会直接
        /// Debug.LogError（玩家日志里就是一条红色 "Could not find Tech: xxx"），
        /// 而我们只是想知道「有没有」，需要安静地返回 null。
        /// </summary>
        private static Tech FindByIdOrNull(Database.Techs techs, string id)
        {
            if (techs == null || string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < techs.Count; i++)
            {
                Tech candidate = techs.GetResource(i) as Tech;
                if (candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static void AddItem(Tech tech, string itemId)
        {
            if (tech == null || string.IsNullOrEmpty(itemId)) return;
            if (!tech.unlockedItemIDs.Contains(itemId))
            {
                tech.AddUnlockedItemIDs(itemId);
            }
        }

        private static void Link(Tech child, Tech parent)
        {
            if (child == null || parent == null) return;
            if (!child.requiredTech.Contains(parent)) child.requiredTech.Add(parent);
            if (!parent.unlockedTech.Contains(child)) parent.unlockedTech.Add(child);
        }

        /// <summary>
        /// 给 tech 建一个节点。位置规则：放在前置科技的「下一排、右一列」（Y 下移、X 右移一整列，
        /// 避免和前置卡片共用同一列）；该位置若已有别的节点，就按「节点宽 + 间距」继续向右平移，直到找到空位。
        /// </summary>
        private static void BuildNode(Tech tech, Tech above, Database.Techs techs)
        {
            if (tech == null || tech.FoundNode) return;

            float width = above != null && above.FoundNode ? above.width : 240f;
            float height = above != null && above.FoundNode ? above.height : 100f;

            float centerX;
            float centerY;
            if (above != null && above.FoundNode)
            {
                // 原版科技树的 y 越大越靠上，所以「下一排」= 前置节点中心往下让出两半个高度 + 间距。
                // x 方向再向右错开：基础量是「前置半宽 + 间距 + 自己半宽」（正好一整列），
                // 现在按用户要求放大到两倍（X_OFFSET_SCALE），避免自建节点和前置卡片共用同一列。
                centerY = above.center.y - (height * 0.5f + above.height * 0.5f + NodeGap);
                centerX = above.center.x + X_OFFSET_SCALE * (above.width * 0.5f + NodeGap + width * 0.5f);
            }
            else
            {
                GetTreeBounds(techs, out float minX, out float maxX, out float minY, out float maxY);
                centerY = minY - (height * 0.5f + NodeGap);
                centerX = minX + width * 0.5f;
            }

            for (int i = 0; i < 64; i++)
            {
                if (!OverlapsAny(techs, tech, centerX, centerY, width, height, NodeGap * 0.5f))
                {
                    break;
                }
                centerX += width + NodeGap;
            }

            ResourceTreeNode node = new ResourceTreeNode
            {
                Id = tech.Id,
                width = width,
                height = height,
                nodeX = centerX - width * 0.5f,
                nodeY = centerY + height * 0.5f
            };

            tech.SetNode(node, above != null ? above.category : "");
        }

        private static void AttachEdge(Tech child, List<Tech> parents)
        {
            ResourceTreeNode childNode = GetNode(child);
            if (childNode == null || parents == null) return;

            foreach (Tech parent in parents)
            {
                ResourceTreeNode parentNode = GetNode(parent);
                if (parentNode == null) continue;

                bool exists = false;
                for (int i = 0; i < childNode.edges.Count; i++)
                {
                    ResourceTreeNode.Edge e = childNode.edges[i];
                    if (e != null && e.source == parentNode && e.target == childNode) { exists = true; break; }
                }
                if (exists) continue;

                childNode.edges.Add(new ResourceTreeNode.Edge(
                    parentNode, childNode, ResourceTreeNode.Edge.EdgeType.PolyLineEdge));
            }
        }

        private static ResourceTreeNode GetNode(Tech tech)
        {
            if (tech == null || !tech.FoundNode) return null;
            return NodeField?.GetValue(tech) as ResourceTreeNode;
        }

        private static void GetTreeBounds(Database.Techs techs, out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = float.MaxValue; maxX = float.MinValue;
            minY = float.MaxValue; maxY = float.MinValue;

            for (int i = 0; i < techs.Count; i++)
            {
                Tech t = techs.GetResource(i) as Tech;
                if (t == null || !t.FoundNode) continue;

                Vector2 c = t.center;
                float halfW = t.width * 0.5f, halfH = t.height * 0.5f;
                minX = Mathf.Min(minX, c.x - halfW); maxX = Mathf.Max(maxX, c.x + halfW);
                minY = Mathf.Min(minY, c.y - halfH); maxY = Mathf.Max(maxY, c.y + halfH);
            }

            if (minX > maxX) { minX = 0f; maxX = 1000f; minY = 0f; maxY = 1000f; }
        }

        private static bool OverlapsAny(Database.Techs techs, Tech self, float cx, float cy, float w, float h, float margin)
        {
            for (int i = 0; i < techs.Count; i++)
            {
                Tech t = techs.GetResource(i) as Tech;
                if (t == null || t == self || !t.FoundNode) continue;

                Vector2 c = t.center;
                float halfW = t.width * 0.5f, halfH = t.height * 0.5f;

                if (Mathf.Abs(cx - c.x) < halfW + w * 0.5f + margin &&
                    Mathf.Abs(cy - c.y) < halfH + h * 0.5f + margin)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
