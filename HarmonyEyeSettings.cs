using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼的外部配置文件（`harmonyeye.cfg`，放在 mod 根目录、与 dll 同级）。
    ///
    /// 为什么要文件：配方时间、产物入网上限这类数值调起来频繁，
    /// 改一次代码要重新编译部署，改成文本文件后改完重启游戏就行。
    ///
    /// 文件不存在时会自动写一份带注释的默认配置出来。
    ///
    /// 支持的行格式：
    ///     recipe_time = 60        # 配方耗时（秒），两种配方共用
    ///     output_rate = 0         # 单条产物单次入网上限（kg），0 = 不限
    /// `#` / `;` / `//` 开头的行与空行会被忽略；键名不区分大小写。
    /// </summary>
    internal static class HarmonyEyeSettings
    {
        /// <summary>配置文件放 mod 根目录，和 dll 同级。</summary>
        private const string FileName = "harmonyeye.cfg";

        /// <summary>没有配置文件时的配方耗时（原版设计值：3 个周期）。</summary>
        public const float DefaultRecipeTime = 1800f;

        /// <summary>没有配置文件时的单条产物入网上限；0 表示不限（由网络剩余容量决定）。</summary>
        public const float DefaultOutputRate = 0f;

        private const float MinValue = 0.1f;
        private const float MaxValue = 1000000f;

        private static bool loaded;
        private static float recipeTime = DefaultRecipeTime;
        private static float outputRate = DefaultOutputRate;

        /// <summary>配方耗时（秒）。两种配方共用同一个值。</summary>
        public static float RecipeTime
        {
            get
            {
                EnsureLoaded();
                return recipeTime;
            }
        }

        /// <summary>单条产物单次入网的上限（kg）；0 = 不限。</summary>
        public static float OutputRate
        {
            get
            {
                EnsureLoaded();
                return outputRate;
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            try
            {
                Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 读取 harmonyeye.cfg 失败，使用默认值: " + e.Message);
            }
        }

        private static void Load()
        {
            string path = ConfigPath();
            if (path == null)
            {
                Debug.LogWarning("[RingCloudTech] 找不到 mod 目录，鸿蒙之眼使用默认配置" +
                                 "（配方 " + DefaultRecipeTime + " s）");
                return;
            }

            if (!File.Exists(path))
            {
                WriteTemplate(path);
                Debug.Log("[RingCloudTech] 已生成默认配置 " + path +
                          "（配方 " + recipeTime + " s / 出网 " + outputRate + " kg/s）");
                return;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }

                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                int split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, split).Trim().ToLowerInvariant();
                string value = line.Substring(split + 1).Trim();

                // 允许行尾注释：recipe_time = 60   # 调试用
                int comment = value.IndexOf('#');
                if (comment >= 0)
                {
                    value = value.Substring(0, comment).Trim();
                }

                float parsed;
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    Debug.LogWarning("[RingCloudTech] harmonyeye.cfg 中 " + key + " 的值无法解析: " + value);
                    continue;
                }

                if (key == "recipe_time")
                {
                    recipeTime = Mathf.Clamp(parsed, MinValue, MaxValue);
                }
                else if (key == "output_rate")
                {
                    // 0 是合法值：表示不限量，由 ALN 分类剩余容量决定每条产物能进多少
                    outputRate = Mathf.Max(0f, parsed);
                }
            }

            Debug.Log("[RingCloudTech] 鸿蒙之眼配置已加载：" + path +
                      " → 配方 " + recipeTime + " s / 出网 " + outputRate + " kg/s");
        }

        private static string ConfigPath()
        {
            try
            {
                string dll = typeof(HarmonyEyeSettings).Assembly.Location;
                if (string.IsNullOrEmpty(dll))
                {
                    return null;
                }

                string dir = Path.GetDirectoryName(dll);
                return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, FileName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteTemplate(string path)
        {
            try
            {
                string text =
                    "# 鸿蒙之眼（Harmony Eye）配置\r\n" +
                    "# 改完保存，重启游戏生效。\r\n" +
                    "\r\n" +
                    "# 配方耗时（秒）。两种配方共用。默认 1800（3 个周期），调试时可用 60。\r\n" +
                    "recipe_time = 60\r\n" +
                    "\r\n" +
                    "# 单条产物的单次入网上限（kg）。0 = 不限：能一次全进就全进，\r\n" +
                    "# 装不下就按 ALN 该品类的剩余容量装一部分。\r\n" +
                    "output_rate = 0\r\n";

                File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                recipeTime = 60f;
                outputRate = DefaultOutputRate;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 写入 harmonyeye.cfg 模板失败: " + e.Message);
            }
        }
    }
}
