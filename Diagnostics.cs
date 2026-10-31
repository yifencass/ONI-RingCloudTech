using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 临时诊断（定位完就删）：
    ///   1. 研究界面里自建科技卡片为什么没有标题栏/图标 —— 把卡片子节点的结构、尺寸、激活状态打出来；
    ///   2. 「ASSERT: Bad temperature」到底是谁注册的 chunk —— 在 SimTemperatureTransfer 注册回调里把温度
    ///      非法（&lt;= 0）的物件信息打出来，并回填 293 K 避免 KCrashReporter 弹崩框。
    /// </summary>
    internal static class Diagnostics
    {
        private static int badTemperatureCount;

        private static string RectInfo(Transform t)
        {
            if (t == null)
            {
                return "无";
            }

            RectTransform rt = t as RectTransform;
            if (rt == null)
            {
                return "?";
            }

            return string.Format(
                "{0:0.#}x{1:0.#}@({2:0.#},{3:0.#}){4}",
                rt.sizeDelta.x, rt.sizeDelta.y, rt.anchoredPosition.x, rt.anchoredPosition.y,
                t.gameObject.activeSelf ? "" : "(隐藏)");
        }

        private static void DumpNode(Transform t, int depth, StringBuilder sb)
        {
            if (t == null || depth > 3)
            {
                return;
            }

            sb.Append('\n').Append(new string(' ', depth * 2)).Append("- ").Append(t.name).Append(' ').Append(RectInfo(t));
            for (int i = 0; i < t.childCount; i++)
            {
                DumpNode(t.GetChild(i), depth + 1, sb);
            }
        }

        internal static void DumpResearchEntries()
        {
            try
            {
                ResearchEntry[] entries = UnityEngine.Object.FindObjectsOfType<ResearchEntry>();
                StringBuilder head = new StringBuilder();
                head.Append("[RingCloudTech] 研究卡片诊断：共 ").Append(entries.Length).Append(" 张");
                Debug.Log(head.ToString());

                foreach (ResearchEntry entry in entries)
                {
                    Tech tech = Traverse.Create(entry).Field("targetTech").GetValue<Tech>();
                    string id = tech != null ? tech.Id : "<null>";
                    bool mine = id == CustomTechs.PassiveEnergyId || id == CustomTechs.PocketUniverseId;
                    bool reference = string.Equals(id, "Multiplexing", StringComparison.OrdinalIgnoreCase);
                    if (!mine && !reference)
                    {
                        continue;
                    }

                    LocText label = Traverse.Create(entry).Field("researchName").GetValue<LocText>();
                    RectTransform rt = entry.transform as RectTransform;
                    StringBuilder sb = new StringBuilder();
                    sb.Append("[RingCloudTech] 研究卡 ").Append(id)
                      .Append(" 名='").Append(label != null ? label.text : "<无>").Append('\'')
                      .Append(" 节点=").Append(tech != null ? tech.width.ToString("0.#") + "x" + tech.height.ToString("0.#") : "?")
                      .Append(" 卡片=").Append(RectInfo(entry.transform));
                    DumpNode(entry.transform, 0, sb);
                    Debug.Log(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RingCloudTech] 研究卡诊断失败: " + ex);
            }
        }

        /// <summary>在 STT 注册回调用 simulate 数据里的温度检查一遍，非法就修掉并记录是谁。</summary>
        internal static unsafe void CheckChunkTemperature(SimTemperatureTransfer stt, int handle)
        {
            try
            {
                if (stt == null || !Sim.IsValidHandle(handle))
                {
                    return;
                }

                int index = Sim.GetHandleIndex(handle);
                float temperature = Game.Instance.simData.elementChunks[index].temperature;
                if (temperature > 0f)
                {
                    return;
                }

                PrimaryElement pe = stt.GetComponent<PrimaryElement>();
                string elementId = pe != null ? pe.ElementID.ToString() : "<无>";
                float mass = pe != null ? pe.Mass : 0f;
                float internalTemperature = pe != null ? pe.InternalTemperature : 0f;
                int cell = Grid.PosToCell(stt.transform.GetPosition());

                Debug.LogWarning(string.Format(
                    "[RingCloudTech] 非法温度 {0:0.###}：物件='{1}' 元素={2} 质量={3:0.###} 内部温度={4:0.###} 格={5} 次数={6}；已回填 293 K 以免弹崩框",
                    temperature, stt.gameObject.name, elementId, mass, internalTemperature, cell, badTemperatureCount + 1));

                Game.Instance.simData.elementChunks[index].temperature = 293f;
                badTemperatureCount++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RingCloudTech] 温度诊断失败: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(SimTemperatureTransfer), "OnSimRegistered")]
    public static class SimTemperatureTransfer_OnSimRegistered_Patch
    {
        public static void Prefix(SimTemperatureTransfer __instance, int handle)
        {
            Diagnostics.CheckChunkTemperature(__instance, handle);
        }
    }

    [HarmonyPatch(typeof(ResearchScreen), "OnShow")]
    public static class ResearchScreen_OnShow_Patch
    {
        public static void Postfix(bool show)
        {
            if (show)
            {
                Diagnostics.DumpResearchEntries();
            }
        }
    }
}
