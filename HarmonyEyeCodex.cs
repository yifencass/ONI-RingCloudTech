using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 给鸿蒙之眼补一条数据库（Codex）条目。
    ///
    /// 原版 GenerateBuildingEntries() 只遍历 TUNING.BUILDINGS.PLANORDER，
    /// 且在 mod 注册建造菜单之前就生成完毕，所以自制建筑默认没有条目。
    /// 这里在 CodexCacheInit 之后手动塞进去。
    /// </summary>
    [HarmonyPatch(typeof(CodexCache), "CodexCacheInit")]
    public static class HarmonyEyeCodex
    {
        public const string ENTRY_ID = "HARMONYEYE";

        public static void Postfix()
        {
            try
            {
                AddEntry();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 数据库条目写入失败: " + e);
            }
        }

        private static void AddEntry()
        {
            if (CodexCache.entries == null || CodexCache.entries.ContainsKey(ENTRY_ID))
            {
                return;
            }

            List<ICodexWidget> widgets = new List<ICodexWidget>
            {
                new CodexText(STRINGS.BUILDINGS.PREFABS.HARMONYEYE.NAME, CodexTextStyle.Title),
                new CodexText(STRINGS.BUILDINGS.PREFABS.HARMONYEYE.DESC, CodexTextStyle.Body),
                new CodexText(STRINGS.BUILDINGS.PREFABS.HARMONYEYE.EFFECT, CodexTextStyle.Body),
            };

            CodexEntry entry = new CodexEntry
            {
                id = ENTRY_ID,
                parentId = "BUILDINGS",
                category = "BUILDINGS",
                title = STRINGS.BUILDINGS.PREFABS.HARMONYEYE.NAME,
                name = STRINGS.BUILDINGS.PREFABS.HARMONYEYE.NAME,
                iconPrefabID = HarmonyEyeConfig.ID,
            };
            // contentContainers 是只读属性，只能往里塞
            if (entry.contentContainers == null)
            {
                Debug.LogWarning("[RingCloudTech] CodexEntry.contentContainers 为 null，放弃写入");
                return;
            }
            entry.contentContainers.Add(new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical));

            CodexCache.entries[ENTRY_ID] = entry;

            CategoryEntry category = CodexCache.categoriesForPostYAMLPopulation
                .FirstOrDefault(c => c.id == "BUILDINGS");
            if (category != null && category.entriesInCategory != null)
            {
                if (!category.entriesInCategory.Any(e => e != null && e.id == ENTRY_ID))
                {
                    category.entriesInCategory.Add(entry);
                }
            }

            Debug.Log("[RingCloudTech] 已写入数据库条目 " + ENTRY_ID);
        }
    }
}