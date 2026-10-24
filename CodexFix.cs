using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 数据库（Codex）修补。
    ///
    /// 原版把「名字」当成条目 ID 用：CodexConversionPanel 与 DetailsScreen 都是
    ///   ManagementMenu.Instance.codexScreen.ChangeArticle(UI.ExtractLinkID(prefab.GetProperName()));
    /// 而条目的真实键是 CodexCache.FormatLinkID(prefabID)（大写、去下划线）。英文里两者接近
    /// （"Mealwood" → "MEALWOOD"），中文名（"咖啡树"/"鸿蒙之眼"）和预制体 ID 对不上，
    /// 于是点过去就是「数据库未构建」，「查看完整条目」按钮也一直是灰的。
    ///
    /// 注意：不能把别名塞进 CodexCache.entries —— CodexScreen.CategorizeEntries 会遍历该字典
    /// 并把 CodexEntry 对象当 key 塞进 Dictionary&lt;CodexEntry, GameObject&gt;，同一个对象出现两次就
    /// 抛 ArgumentException: An item with the same key has already been added. Key: CodexEntry。
    /// 所以改成在 ChangeArticle / CodexEntryButton_GetCodexId 两处入口做名字→真实 ID 的解析。
    /// </summary>
    [HarmonyPatch(typeof(CodexCache), "CodexCacheInit")]
    public static class RingCloudTechCodex
    {
        /// <summary>自制预制体：建筑 / 植物 / 种子 / 产物。</summary>
        private static readonly string[] OwnedPrefabs =
        {
            BeverageMixerConfig.ID,        // BeverageMixer
            SuperCapacitorConfig.ID,       // SuperCapacitor
            HarmonyEyeConfig.ID,           // HarmonyEye
            CoffeeTreeRegistrar.PLANT_ID,  // CoffeeTree
            CoffeeTreeRegistrar.SEED_ID,   // CoffeeTreeSeed
            CoffeeTreeRegistrar.BEAN_ID,   // CoffeeBean
            SweetCaneRegistrar.PLANT_ID,   // SweetCanePlant
            SweetCaneRegistrar.SEED_ID,    // SweetCaneSeed
            SweetCaneRegistrar.FRUIT_ID,   // SweetCane
            RingCloudTechMod.COLA_TAG,     // Cola
        };

        /// <summary>名字（FormatLinkID 之后）→ 真实条目 ID。</summary>
        internal static readonly Dictionary<string, string> AliasMap = new Dictionary<string, string>();

        public static void Postfix()
        {
            try
            {
                EnsureBuildingEntry(BeverageMixerConfig.ID,
                    STRINGS.BUILDINGS.PREFABS.BEVERAGEMIXER.NAME,
                    STRINGS.BUILDINGS.PREFABS.BEVERAGEMIXER.DESC,
                    STRINGS.BUILDINGS.PREFABS.BEVERAGEMIXER.EFFECT);

                EnsureBuildingEntry(SuperCapacitorConfig.ID,
                    STRINGS.BUILDINGS.PREFABS.SUPERCAPACITOR.NAME,
                    STRINGS.BUILDINGS.PREFABS.SUPERCAPACITOR.DESC,
                    STRINGS.BUILDINGS.PREFABS.SUPERCAPACITOR.EFFECT);

                EnsureBuildingEntry(HarmonyEyeConfig.ID,
                    STRINGS.BUILDINGS.PREFABS.HARMONYEYE.NAME,
                    STRINGS.BUILDINGS.PREFABS.HARMONYEYE.DESC,
                    STRINGS.BUILDINGS.PREFABS.HARMONYEYE.EFFECT);

                BuildAliasMap();
                AddPlantProducts(CoffeeTreeRegistrar.PLANT_ID, CoffeeTreeRegistrar.BEAN_ID);
                AddPlantProducts(SweetCaneRegistrar.PLANT_ID, SweetCaneRegistrar.FRUIT_ID);

                Debug.Log("[RingCloudTech] 数据库修补完成：共 " + CodexCache.entries.Count
                          + " 条，别名 " + AliasMap.Count + " 个");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 数据库修补失败: " + e);
            }
        }

        /// <summary>建筑条目；已由原版生成过就直接跳过。</summary>
        private static void EnsureBuildingEntry(string prefabId, LocString name, LocString desc, LocString effect)
        {
            string key = CodexCache.FormatLinkID(prefabId);
            if (CodexCache.entries.ContainsKey(key))
            {
                return;
            }

            List<ICodexWidget> widgets = new List<ICodexWidget>
            {
                new CodexText(name, CodexTextStyle.Title),
                new CodexDividerLine(),
                new CodexText(desc, CodexTextStyle.Body),
            };
            if (effect != null)
            {
                widgets.Add(new CodexText(effect, CodexTextStyle.Body));
            }

            CodexEntry entry = new CodexEntry("BUILDINGS", new List<ContentContainer>
            {
                new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical),
            }, Strings.Get(name))
            {
                iconPrefabID = prefabId,
            };

            CodexCache.AddEntry(prefabId, entry);
            Debug.Log("[RingCloudTech] 已补建筑数据库条目 " + key);
        }

        /// <summary>登记「显示名 → 条目 ID」的别名表（只进本类的字典，不动 CodexCache.entries）。</summary>
        private static void BuildAliasMap()
        {
            AliasMap.Clear();
            foreach (string prefabId in OwnedPrefabs)
            {
                string key = CodexCache.FormatLinkID(prefabId);
                if (!CodexCache.entries.TryGetValue(key, out CodexEntry entry) || entry == null)
                {
                    Debug.LogWarning("[RingCloudTech] 没有数据库条目: " + prefabId);
                    continue;
                }

                string shown = entry.name;
                if (string.IsNullOrEmpty(shown))
                {
                    GameObject prefab = Assets.GetPrefab(prefabId);
                    shown = prefab != null ? prefab.GetProperName() : null;
                }
                if (string.IsNullOrEmpty(shown))
                {
                    continue;
                }

                // 两种写法都登记：原样，以及 FormatLinkID 之后
                AliasMap[shown] = key;
                string formatted = CodexCache.FormatLinkID(shown);
                if (!string.IsNullOrEmpty(formatted))
                {
                    AliasMap[formatted] = key;
                }
            }
            Debug.Log("[RingCloudTech] 数据库别名表就绪");
        }

        /// <summary>去掉名字里的富文本标签（原版 UI.StripLinkFormatting 在 firstpass 里，不引用它）。</summary>
        internal static string StripTags(string s)
        {
            return string.IsNullOrEmpty(s) ? s : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "");
        }

        /// <summary>把「显示名」解析成真实条目 ID；解析不出就原样返回。</summary>
        internal static string Resolve(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }
            if (CodexCache.entries.ContainsKey(id) || CodexCache.subEntries.ContainsKey(id))
            {
                return id;
            }
            if (AliasMap.TryGetValue(id, out string real) && !string.IsNullOrEmpty(real))
            {
                return real;
            }
            string formatted = CodexCache.FormatLinkID(id);
            if (!string.IsNullOrEmpty(formatted) && AliasMap.TryGetValue(formatted, out real))
            {
                return real;
            }
            return id;
        }

        /// <summary>已经在哪些植物条目里补过产出（CodexCacheInit 可能被多次调用）。</summary>
        private static readonly HashSet<string> ProductPatched = new HashSet<string>();

        /// <summary>在植物条目里补一块「产出」面板。</summary>
        private static void AddPlantProducts(string plantPrefabId, string productId)
        {
            string key = CodexCache.FormatLinkID(plantPrefabId);
            if (!ProductPatched.Add(key))
            {
                return;
            }
            if (!CodexCache.entries.TryGetValue(key, out CodexEntry entry) || entry?.contentContainers == null)
            {
                return;
            }

            GameObject plant = Assets.GetPrefab(plantPrefabId);
            Crop crop = plant?.GetComponent<Crop>();
            if (crop == null || crop.cropVal.cropDuration <= 0f)
            {
                return;
            }

            Tag product = new Tag(productId);
            if (Assets.GetPrefab(product) == null)
            {
                return;
            }

            float perCycle = crop.cropVal.numProduced / crop.cropVal.cropDuration * 600f;
            List<ICodexWidget> widgets = new List<ICodexWidget>
            {
                new CodexLargeSpacer(),
                new CodexText(product.ProperName(), CodexTextStyle.Subtitle),
                new CodexDividerLine(),
                new CodexConversionPanel(
                    product.ProperName(),
                    new ElementUsage[0],
                    new ElementUsage[] { new ElementUsage(product, perCycle, true) },
                    plant),
            };

            entry.contentContainers.Add(new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical));
            Debug.Log("[RingCloudTech] 已给 " + plantPrefabId + " 补产出 " + productId);
        }
    }

    /// <summary>点击链接时把中文名换成真实条目 ID，否则 CodexScreen 找不到条目就显示「数据库未构建」。</summary>
    [HarmonyPatch(typeof(CodexScreen), "ChangeArticle")]
    public static class CodexScreen_ChangeArticle_Patch
    {
        public static void Prefix(ref string id)
        {
            id = RingCloudTechCodex.Resolve(id);
        }
    }

    /// <summary>「查看数据库中的完整条目」按钮：原版按名字拼 ID，对不上就把按钮置灰。</summary>
    [HarmonyPatch(typeof(DetailsScreen), "CodexEntryButton_GetCodexId")]
    public static class DetailsScreen_CodexEntryButton_Patch
    {
        public static void Postfix(DetailsScreen __instance, ref string __result)
        {
            try
            {
                if (!string.IsNullOrEmpty(__result)
                    && (CodexCache.entries.ContainsKey(__result)
                        || CodexCache.subEntries.ContainsKey(__result)))
                {
                    return;
                }

                GameObject target = __instance != null ? __instance.target : null;
                if (target == null)
                {
                    return;
                }

                string guess = CodexCache.FormatLinkID(RingCloudTechCodex.StripTags(target.GetProperName()));
                string real = RingCloudTechCodex.Resolve(guess);
                if (real != guess && CodexCache.entries.ContainsKey(real))
                {
                    __result = real;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 数据库按钮解析失败: " + e);
            }
        }
    }
}
