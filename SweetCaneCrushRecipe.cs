using System;
using System.Collections.Generic;
using HarmonyLib;
using TUNING;

namespace RingCloudTech
{
    /// <summary>
    /// 碎石机配方：甜蔗 → 蔗糖 + 植物纤维。
    ///
    /// 这条配方把 mod 内部的内容串成一条链：
    ///     甜蔗（作物） → 蔗糖（碎石机） → 可乐（饮料调配机）
    ///
    /// 为什么挂在 RockCrusherConfig.ConfigureBuildingTemplate 的 Postfix：
    ///   原版就是在这个方法里 new ComplexRecipe 的，此时
    ///   ComplexRecipeManager 已经可用，时间点最稳。
    ///   放在 Db.Initialize 里则可能早于 ComplexRecipeManager 初始化。
    ///
    /// 产物温度处理：
    ///   蔗糖是固体元素，原版碎石机输出 Sand 时也不指定 TemperatureOperation，
    ///   走默认即可。只有纯液体/气体元素才需要 Melted
    ///   （AverageTemperature / Heated 分支会去找 prefab，元素没有 prefab）。
    /// </summary>
    [HarmonyPatch(typeof(RockCrusherConfig), "ConfigureBuildingTemplate")]
    internal static class SweetCaneCrushRecipePatch
    {
        private const string FABRICATOR_ID = "RockCrusher";

        /// <summary>每次投入的甜蔗质量，单位 kg。</summary>
        private const float INPUT_KG = 10f;

        /// <summary>产出蔗糖质量。</summary>
        private const float SUCROSE_KG = 6f;

        /// <summary>产出植物纤维质量。</summary>
        private const float FIBER_KG = 4f;

        /// <summary>加工耗时，秒。原版碎石机配方是 40 秒。</summary>
        private const float RECIPE_TIME = 40f;

        [HarmonyPostfix]
        internal static void Postfix()
        {
            try
            {
                AddRecipe();
            }
            catch (Exception e)
            {
                // 配方失败不该拖垮整个 mod 的加载
                Debug.LogError("[RingCloudTech] 甜蔗碎石配方注册失败: " + e);
            }
        }

        private static void AddRecipe()
        {
            Tag sucroseTag = SimHashes.Sucrose.CreateTag();
            Tag fiberTag = new Tag("PlantFiber");

            // 输入：甜蔗。用 FoodInfo 重载，直接吃食物定义
            ComplexRecipe.RecipeElement[] inputs =
            {
                new ComplexRecipe.RecipeElement(SweetCaneRegistrar.FruitInfo, INPUT_KG),
            };

            // 输出：固体元素 + 带 prefab 的工业产物，两个都不需要指定温度处理
            ComplexRecipe.RecipeElement[] outputs =
            {
                new ComplexRecipe.RecipeElement(sucroseTag, SUCROSE_KG),
                new ComplexRecipe.RecipeElement(fiberTag, FIBER_KG),
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID(FABRICATOR_ID, inputs, outputs);

            // 幂等：可能因为场景重载导致 Postfix 跑多次
            if (ComplexRecipeManager.Get().GetRecipe(recipeId) != null)
            {
                return;
            }

            new ComplexRecipe(recipeId, inputs, outputs)
            {
                time = RECIPE_TIME,
                description = STRINGS.UI.SWEETCANE_CRUSH.RECIPE_DESC,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Custom,
                customName = STRINGS.UI.SWEETCANE_CRUSH.RECIPE_NAME,
                fabricators = new List<Tag> { TagManager.Create(FABRICATOR_ID) },
                sortOrder = 100,
            };

            Debug.Log("[RingCloudTech] 已注册碎石机配方: 甜蔗 "
                + INPUT_KG + "kg -> 蔗糖 " + SUCROSE_KG + "kg + 植物纤维 " + FIBER_KG + "kg");
        }
    }
}
