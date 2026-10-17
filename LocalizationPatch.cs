using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 注册 STRINGS 树并应用 translations/&lt;locale&gt;.po。
    ///
    /// 不用 Localization.OverloadStrings 的原因：
    ///   它的入口是 path = &lt;assemblyKey&gt; + "." + &lt;TypeName&gt;，
    ///   只能覆盖游戏自带 LocString 树里的字段（比如 ELEMENTS.WATER.NAME）。
    ///   我们的 BEVERAGEMIXER / COLA 是新增的嵌套类，游戏的 ELEMENTS、
    ///   BUILDINGS.PREFABS 里并不存在，遍历时够不着，.po 条目永远不会生效。
    ///
    /// 所以这里自己做两件事：
    ///   1. 用 CreateLocStringKeys 把 STRINGS 注册进字符串表（拿默认文本）
    ///   2. 读 .po，既覆盖 LocString 字段本身，也覆盖字符串表
    ///      （游戏 UI 走 Strings.Get，代码里可能直接引用 LocString）
    /// </summary>
    [HarmonyPatch(typeof(global::Localization), nameof(global::Localization.Initialize))]
    public static class Localization_Initialize_Patch
    {
        private const string RootKey = "STRINGS";

        private static void Postfix()
        {
            try
            {
                // 关键：父路径传 null，内部会拼成 "" + "STRINGS" + "."
                // 得到 STRINGS.ELEMENTS.COLA.NAME 这样的正确路径
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
                    Debug.LogWarning("[RingCloudTech] 缺少 translations 目录，使用默认文本");
                    return;
                }

                foreach (string code in BuildCandidates(localeCode))
                {
                    string poFile = Path.Combine(transDir, code + ".po");
                    if (!File.Exists(poFile))
                    {
                        continue;
                    }

                    var entries = global::Localization.LoadStringsFile(poFile, false);
                    int applied = ApplyTranslations(typeof(STRINGS), RootKey, entries);
                    Debug.Log("[RingCloudTech] 已加载 " + code + ".po，应用 " + applied + " 条");
                    return;
                }

                Debug.LogWarning("[RingCloudTech] 没找到匹配的 po 文件（尝试过: "
                                 + string.Join(", ", BuildCandidates(localeCode)) + "）");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 加载翻译失败: " + e);
            }
        }

        /// <summary>
        /// 递归遍历 STRINGS 的嵌套类型，按 key 覆盖 LocString 字段并写回字符串表。
        /// 返回实际替换的条目数。
        /// </summary>
        private static int ApplyTranslations(Type type, string path, Dictionary<string, string> entries)
        {
            int count = 0;

            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field.FieldType != typeof(LocString))
                {
                    continue;
                }

                string key = path + "." + field.Name;
                string value;
                if (!entries.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                // 替换 LocString 字段（代码里直接引用时拿到的就是译文）
                field.SetValue(null, new LocString(value, key));
                // 同时写进字符串表（游戏 UI 通过 Strings.Get 取值）
                Strings.Add(key, value);
                count++;
            }

            foreach (Type nested in type.GetNestedTypes(
                         BindingFlags.Public | BindingFlags.NonPublic))
            {
                count += ApplyTranslations(nested, path + "." + nested.Name, entries);
            }

            return count;
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
