using System;
using System.Collections.Generic;
using HarmonyLib;
using KMod;
using Klei.AI;
using STRINGS;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>模组入口。</summary>
    public class RingCloudTechMod : UserMod2
    {
        public const string EFFECT_ID = "DuplicantDrankCola";
        public const string COLA_TAG = "Cola";

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            try
            {
                ModUtil.AddBuildingToPlanScreen("Food", BeverageMixerConfig.ID);
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 注册建造菜单失败: " + e);
            }
            Debug.Log("[RingCloudTech] 环云科技已加载");
        }
    }

    /// <summary>
    /// 给可乐补 Substance：渲染材质 + 液滴动画。
    ///
    /// 时序：Assets.CreatePrefabs 里先跑 SubstanceListHookup，再跑 CreatePrefabs
    /// （后者会调 LegacyModMain.Load → LoadOre → GeneratedOre.LoadGeneratedOre）。
    /// 自定义元素没有 SubstanceTable 条目，substance 是兜底 new Substance()，
    /// material 和 anim 都为空，导致液体渲染异常、也造不出液滴 prefab。
    /// </summary>
    [HarmonyPatch(typeof(Assets), "SubstanceListHookup")]
    public static class Assets_SubstanceListHookup_Patch
    {
        public static void Postfix()
        {
            try
            {
                Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
                if (cola == null || cola.substance == null)
                {
                    Debug.LogWarning("[RingCloudTech] 未找到 Cola 元素");
                    return;
                }

                Substance source = Assets.instance?.substanceTable?.GetSubstance(SimHashes.Water);
                if (source == null)
                {
                    Debug.LogError("[RingCloudTech] 找不到水的 Substance 作为模板");
                    return;
                }

                if (source.material != null)
                {
                    cola.substance.material = new Material(source.material) { name = "matCola" };
                }
                cola.substance.anim = source.anim;

                Debug.Log("[RingCloudTech] 已配置可乐 Substance");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 配置 Substance 失败: " + e);
            }
        }
    }

    /// <summary>
    /// 后备：若 SubstanceListHookup 没能补上 anim，这里再补一次，
    /// 保证 LoadGeneratedOre 能给可乐生成液滴 prefab。
    /// </summary>
    [HarmonyPatch(typeof(GeneratedOre), nameof(GeneratedOre.LoadGeneratedOre))]
    public static class GeneratedOre_LoadGeneratedOre_Patch
    {
        public static void Prefix()
        {
            try
            {
                Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
                if (cola?.substance == null || cola.substance.anim != null)
                {
                    return;
                }

                Element water = ElementLoader.FindElementByHash(SimHashes.Water);
                if (water?.substance?.anim != null)
                {
                    cola.substance.anim = water.substance.anim;
                    Debug.Log("[RingCloudTech] 后备补齐可乐液滴动画");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 后备补动画失败: " + e);
            }
        }
    }

    /// <summary>
    /// Db.Initialize 之后：注册效果、修正元素文本、解锁科技。
    /// 此时元素已加载完毕（LoadOre 已跑过）。
    /// </summary>
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class Db_Initialize_Patch
    {
        public static void Postfix()
        {
            try { RegisterColaEffect(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 注册效果失败: " + e); }

            try { CustomizeColaElement(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 修正元素失败: " + e); }

            try { UnlockBeverageMixer(); }
            catch (Exception e) { Debug.LogError("[RingCloudTech] 解锁建筑失败: " + e); }
        }

        private static void CustomizeColaElement()
        {
            Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
            if (cola == null)
            {
                Debug.LogWarning("[RingCloudTech] 未找到 Cola 元素");
                return;
            }

            // .po 覆盖的是 LocString 树，元素自身的 name/description
            // 是加载 YAML 时按 localizationID 取的，这里显式赋值。
            cola.name = STRINGS.ELEMENTS.COLA.NAME;
            cola.description = STRINGS.ELEMENTS.COLA.DESC;

            if (cola.substance != null)
            {
                Color32 deep = new Color32(38, 20, 10, 255);
                cola.substance.colour = deep;
                cola.substance.uiColour = deep;
                cola.substance.conduitColour = new Color32(70, 40, 20, 255);
            }
        }

        private static void RegisterColaEffect()
        {
            // 用 Exists 而非 Get：Get 查不到会 LogError，其堆栈会让
            // SearchForModsInStackTrace 把所有 patch 了 Db.Initialize 的 mod 标记为可疑。
            if (Db.Get().effects.Exists(RingCloudTechMod.EFFECT_ID))
            {
                return;
            }

            Effect effect = new Effect(
                id: RingCloudTechMod.EFFECT_ID,
                name: STRINGS.DUPLICANTS.MODIFIERS.DUPLICANTDRANKCOLA.NAME,
                description: STRINGS.DUPLICANTS.MODIFIERS.DUPLICANTDRANKCOLA.TOOLTIP,
                duration: 600f,                 // 1 个周期
                show_in_ui: true,
                trigger_floating_text: true,
                is_bad: false);

            // deltaAttribute 单位是「每秒」，1 周期 = 600 秒。
            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Stress.deltaAttribute.Id, -20f / 600f, "可乐：压力"));
            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Stamina.deltaAttribute.Id, 20f / 600f, "可乐：体力"));
            effect.Add(new AttributeModifier(
                Db.Get().Amounts.Calories.deltaAttribute.Id, 500f * 1000f / 600f, "可乐：卡路里"));

            // 即时属性：士气 / 运动
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.QualityOfLife.Id, 3f, "可乐：士气"));
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.Athletics.Id, 4f, "可乐：运动"));

            Db.Get().effects.Add(effect);
            Debug.Log("[RingCloudTech] 已注册效果 " + RingCloudTechMod.EFFECT_ID);
        }

        private static void UnlockBeverageMixer()
        {
            Tech tech = Db.Get().Techs.Get("FoodRepurposing");
            if (tech == null)
            {
                Debug.LogWarning("[RingCloudTech] 找不到 FoodRepurposing 科技");
                return;
            }
            if (!tech.unlockedItemIDs.Contains(BeverageMixerConfig.ID))
            {
                tech.AddUnlockedItemIDs(BeverageMixerConfig.ID);
            }
        }
    }

    /// <summary>
    /// 让可乐的 Tag 显示成「可乐」而非「Cola」。
    /// TagManager.ProperNames 只收录原版 Tag，FillMissingProperNames 也不会新增 key，
    /// 所以自定义元素查询时会回退到 tag.Name。
    /// </summary>
    [HarmonyPatch(typeof(TagManager), nameof(TagManager.GetProperName))]
    public static class TagManager_GetProperName_Patch
    {
        public static bool Prefix(Tag tag, bool stripLink, ref string __result)
        {
            if (tag.Name == RingCloudTechMod.COLA_TAG)
            {
                __result = STRINGS.ELEMENTS.COLA.NAME;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// ComplexRecipe.GetUIIcon 里 Assets.GetPrefab(tag).GetComponent 不判空，
    /// 元素产物没有 prefab 就会 NRE。改走 Def.GetUISprite 的元素分支。
    /// </summary>
    [HarmonyPatch(typeof(ComplexRecipe), nameof(ComplexRecipe.GetUIIcon))]
    public static class ComplexRecipe_GetUIIcon_Patch
    {
        public static bool Prefix(ComplexRecipe __instance, ref Sprite __result)
        {
            try
            {
                if (__instance.results == null || __instance.results.Length == 0)
                {
                    __result = null;
                    return false;
                }

                Tag tag = (__instance.nameDisplay == ComplexRecipe.RecipeNameDisplay.Ingredient
                           && __instance.ingredients != null
                           && __instance.ingredients.Length > 0)
                    ? __instance.ingredients[0].material
                    : __instance.results[0].material;

                if (__instance.nameDisplay == ComplexRecipe.RecipeNameDisplay.Custom
                    && !string.IsNullOrEmpty(__instance.customSpritePrefabID))
                {
                    tag = __instance.customSpritePrefabID;
                }

                if (Assets.GetPrefab(tag) != null)
                {
                    return true;
                }

                __result = Def.GetUISprite((object)tag, "ui", false).first;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] GetUIIcon 保护失败: " + e);
                __result = null;
                return false;
            }
        }
    }

    /// <summary>
    /// SelectedRecipeQueueScreen.GetIngredientDescription 里
    /// prefab.GetProperName() 不判空，元素原料会 NRE。
    /// 同时原版只统计世界库存，材料搬进机器后就显示缺料 —— 这里合并机器内部库存。
    /// </summary>
    [HarmonyPatch(typeof(SelectedRecipeQueueScreen), "GetIngredientDescription")]
    public static class SelectedRecipeQueueScreen_GetIngredientDescription_Patch
    {
        public static bool Prefix(ComplexRecipe.RecipeElement ingredient,
                                  out bool hasEnoughMaterial,
                                  ref string __result,
                                  SelectedRecipeQueueScreen __instance)
        {
            hasEnoughMaterial = false;
            try
            {
                string amountText = GameUtil.GetFormattedByTag(ingredient.material, ingredient.amount);

                float available = 0f;
                ComplexFabricator target = Traverse.Create(__instance)
                    .Field("target").GetValue<ComplexFabricator>();
                if (target != null)
                {
                    if (target.GetMyWorld() != null)
                    {
                        available = target.GetMyWorld().worldInventory
                            .GetAmount(ingredient.material, includeRelatedWorlds: true);
                    }
                    if (target.inStorage != null)
                    {
                        available += target.inStorage.GetAmountAvailable(ingredient.material);
                    }
                }

                hasEnoughMaterial = available >= ingredient.amount;
                string availText = GameUtil.GetFormattedByTag(ingredient.material, available);

                string text = GameUtil.SafeStringFormat(
                    UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_REQUIREMENT,
                    ingredient.material.ProperName(),
                    amountText) + "\n";

                string avail = GameUtil.SafeStringFormat(
                    UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_AVAILABLE, availText);

                __result = hasEnoughMaterial
                    ? text + "<size=12>" + avail + "</size>"
                    : text + "<size=12><color=#E68280>" + avail + "</color></size>";

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] GetIngredientDescription 保护失败: " + e);
                __result = ingredient.material.ProperName();
                return false;
            }
        }
    }

    /// <summary>
    /// Codex 的 CodexRecipePanel.ConfigureComplexRecipe 对元素原料会 NRE
    /// （多处 Assets.GetPrefab(...).GetComponent 不判空）。
    /// 检测到非 prefab 的 tag 就跳过图标渲染，只写标题。
    /// </summary>
    [HarmonyPatch(typeof(CodexRecipePanel), "ConfigureComplexRecipe")]
    public static class CodexRecipePanel_ConfigureComplexRecipe_Patch
    {
        public static bool Prefix(CodexRecipePanel __instance)
        {
            try
            {
                ComplexRecipe recipe = Traverse.Create(__instance)
                    .Field("complexRecipe").GetValue<ComplexRecipe>();
                if (recipe == null)
                {
                    return true;
                }

                bool hasNonPrefab = false;
                if (recipe.ingredients != null)
                {
                    foreach (ComplexRecipe.RecipeElement ing in recipe.ingredients)
                    {
                        if (Assets.GetPrefab(ing.material) == null) { hasNonPrefab = true; break; }
                    }
                }
                if (!hasNonPrefab && recipe.results != null)
                {
                    foreach (ComplexRecipe.RecipeElement res in recipe.results)
                    {
                        if (Assets.GetPrefab(res.material) == null) { hasNonPrefab = true; break; }
                    }
                }

                if (!hasNonPrefab)
                {
                    return true;
                }

                object titleObj = Traverse.Create(__instance).Field("title").GetValue();
                if (titleObj != null && recipe.results != null && recipe.results.Length > 0)
                {
                    var textProp = titleObj.GetType().GetProperty("text");
                    textProp?.SetValue(titleObj, recipe.results[0].material.ProperName());
                }

                Debug.Log("[RingCloudTech] 跳过元素配方在 Codex 里的图标渲染");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] Codex 配方保护失败: " + e);
                return false;
            }
        }
    }

    /// <summary>
    /// Def.GetUISprite(Tag, string) 无判空：Assets.GetPrefab(prefabID).GetComponent
    /// 对元素 Tag 直接 NRE。prefab 不存在时改走 object 重载（会先查 ElementLoader）。
    /// </summary>
    [HarmonyPatch(typeof(Def), nameof(Def.GetUISprite), new Type[] { typeof(Tag), typeof(string) })]
    public static class Def_GetUISprite_Patch
    {
        public static bool Prefix(Tag prefabID, string facadeID, ref Tuple<Sprite, Color> __result)
        {
            if (Assets.GetPrefab(prefabID) != null)
            {
                return true;
            }

            __result = Def.GetUISprite((object)prefabID, "ui", false);
            return false;
        }
    }

    /// <summary>把可乐加进饮水机的可选饮品。</summary>
    [HarmonyPatch(typeof(WaterCoolerConfig), "CreateBuildingDef")]
    public static class WaterCoolerConfig_CreateBuildingDef_Patch
    {
        public static void Postfix()
        {
            try
            {
                Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
                if (cola == null)
                {
                    Debug.LogWarning("[RingCloudTech] 未找到 Cola，饮水机不添加");
                    return;
                }

                List<Tuple<Tag, string>> options =
                    new List<Tuple<Tag, string>>(WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS);

                foreach (var o in options)
                {
                    if (o.first == cola.tag)
                    {
                        return;   // 已存在
                    }
                }

                options.Add(new Tuple<Tag, string>(cola.tag, RingCloudTechMod.EFFECT_ID));
                WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS = options.ToArray();

                Debug.Log("[RingCloudTech] 饮水机已支持可乐");
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 扩展饮水机失败: " + e);
            }
        }
    }

    /// <summary>
    /// 饮水机选项的 tooltip：原版取 OPTION_TOOLTIPS.&lt;TAG&gt;，自定义液体没有这个 key。
    /// 这里重写可乐那一条，首行用 UI.FormatAsLink 包出标题色。
    /// </summary>
    [HarmonyPatch(typeof(WaterCooler), "GetOptions")]
    public static class WaterCooler_GetOptions_Patch
    {
        public static void Postfix(ref FewOptionSideScreen.IFewOptionSideScreen.Option[] __result)
        {
            try
            {
                Element cola = ElementLoader.FindElementByName(RingCloudTechMod.COLA_TAG);
                if (cola == null || __result == null)
                {
                    return;
                }

                for (int i = 0; i < __result.Length; i++)
                {
                    if (__result[i].tag != cola.tag)
                    {
                        continue;
                    }

                    __result[i].labelText =
                        string.IsNullOrEmpty(cola.name)
                            ? STRINGS.ELEMENTS.COLA.NAME
                            : cola.name;

                    string tip = UI.FormatAsLink(__result[i].labelText, "COLA")
                               + "\n" + STRINGS.ELEMENTS.COLA.DESC;

                    Effect effect = Db.Get().effects.Get(RingCloudTechMod.EFFECT_ID);
                    if (effect != null)
                    {
                        tip += "\n\n" + Effect.CreateTooltip(effect, showDuration: true);
                    }
                    __result[i].tooltipText = tip;
                    break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 修正饮水机文本失败: " + e);
            }
        }
    }
}
