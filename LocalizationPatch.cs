using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 注册 STRINGS 树并加载 translations/&lt;locale&gt;.po。
    ///
    /// 必须在 Localization.Initialize 之后执行，因为需要 LocString 树已经建好。
    /// CreateLocStringKeys 会递归嵌套类，把字段注册成
    ///     STRINGS.BUILDINGS.PREFABS.BEVERAGEMIXER.NAME
    /// 这类路径，正好对上游戏内部的查找方式。
    /// </summary>
    [HarmonyPatch(typeof(global::Localization), nameof(global::Localization.Initialize))]
    public static class Localization_Initialize_Patch
    {
        private static void Postfix()
        {
            try
            {
                global::Localization.RegisterForTranslation(typeof(STRINGS));
                LocString.CreateLocStringKeys(typeof(STRINGS), null);
                Debug.Log("[RingCloudTech] STRINGS 已注册");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] STRINGS 注册失败: " + e);
                return;
            }

            try
            {
                string modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string transDir = Path.Combine(modDir, "translations");

                string localeCode = null;
                try { localeCode = global::Localization.GetLocale()?.Code; } catch { }
                Debug.Log("[RingCloudTech] 当前语言代码: " + (localeCode ?? "(null)"));

                if (!Directory.Exists(transDir))
                {
                    Debug.LogWarning("[RingCloudTech] 缺少 translations 目录，使用默认英文文本");
                    return;
                }

                // 依次尝试：当前语言 → 语言主码（zh-CN → zh）→ 兜底
                string[] candidates = BuildCandidates(localeCode);
                foreach (string code in candidates)
                {
                    string poFile = Path.Combine(transDir, code + ".po");
                    if (!File.Exists(poFile))
                    {
                        continue;
                    }

                    global::Localization.OverloadStrings(
                        global::Localization.LoadStringsFile(poFile, false));
                    Debug.Log("[RingCloudTech] 已加载翻译 " + code + ".po");
                    return;
                }

                Debug.LogWarning("[RingCloudTech] 没找到匹配的 po 文件（尝试过: "
                                 + string.Join(", ", candidates) + "）");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 加载翻译失败: " + e);
            }
        }

        private static string[] BuildCandidates(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode))
            {
                return new[] { "en" };
            }

            string normalized = localeCode.Replace('_', '-');
            string primary = normalized.Split('-')[0];

            if (normalized == primary)
            {
                return new[] { normalized, "en" };
            }
            return new[] { normalized, primary, "en" };
        }
    }
}
