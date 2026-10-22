using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 与 BetterFoodEffect（Steam 3796760626，staticID `BetterFoodEffect`）的软联动。
    ///
    /// 对方做的事：把「食材 Tag」映射到营养分类，食物按所用食材组成获得 1 周期效果。
    /// 分类有 Vegetable / Protein / Fruit / Carb / Oil / Seafood / Spicy 七种。
    ///
    /// 我们做的事：把 SweetCane 登记为 Fruit（甜美水果）。
    ///
    /// 为什么要反射：
    ///   对方的数据库是
    ///       public static class IngredientTagDatabase
    ///       {
    ///           public const string Signature = "bfe-v1.3.5-...";
    ///           private static readonly Dictionary<string, NutritionCategory[]> Tags;
    ///           public static bool TryGet(...)              // 只读
    ///           public static string[] GetIngredientIdsForCategory(...)  // 只读
    ///       }
    ///   只有读接口，没有写接口，所以只能反射改私有字典。
    ///
    /// 软依赖原则：
    ///   - 没装 BetterFoodEffect：TypeByName 返回 null，直接返回，不影响本 mod
    ///   - 对方版本变了：只警告，不中断
    ///   - 任何异常都被吞掉并记录，绝不拖垮加载
    /// </summary>
    internal static class BetterFoodEffectIntegration
    {
        /// <summary>对方的食材数据库类型全名。</summary>
        private const string DatabaseTypeName = "FoodNutritionBuff.Nutrition.IngredientTagDatabase";

        /// <summary>对方的营养分类枚举类型全名。</summary>
        private const string CategoryTypeName = "FoodNutritionBuff.Nutrition.NutritionCategory";

        /// <summary>
        /// 我们验证过的对方版本签名。对方更新后这个常量会变，
        /// 届时字段名/结构可能也有变动，需要重新确认。
        /// </summary>
        private const string VerifiedSignature = "bfe-v1.3.5-tags-all-spice-sources-tallow-20260918";

        /// <summary>登记的目标分类。</summary>
        private const string TargetCategory = "Fruit";

        private static bool _done;

        internal static void TryRegister()
        {
            if (_done)
            {
                return;
            }
            _done = true;

            try
            {
                Type dbType = AccessTools.TypeByName(DatabaseTypeName);
                if (dbType == null)
                {
                    // 没装 BetterFoodEffect —— 正常情况，静默返回
                    return;
                }

                Type catType = AccessTools.TypeByName(CategoryTypeName);
                if (catType == null)
                {
                    Debug.LogWarning("[RingCloudTech] 找到 BetterFoodEffect 但缺 NutritionCategory 类型，联动跳过");
                    return;
                }

                // 版本校验：Signature 变了说明对方改过数据结构，警告但仍尝试
                FieldInfo sigField = AccessTools.Field(dbType, "Signature");
                string signature = sigField?.GetValue(null) as string;
                if (!string.IsNullOrEmpty(signature) && signature != VerifiedSignature)
                {
                    Debug.LogWarning("[RingCloudTech] BetterFoodEffect 版本与验证时不同（"
                        + signature + "），联动可能失效；如异常请反馈");
                }

                FieldInfo tagsField = AccessTools.Field(dbType, "Tags");
                if (tagsField == null)
                {
                    Debug.LogWarning("[RingCloudTech] BetterFoodEffect 的 IngredientTagDatabase.Tags 字段找不到，联动跳过");
                    return;
                }

                IDictionary tags = tagsField.GetValue(null) as IDictionary;
                if (tags == null)
                {
                    Debug.LogWarning("[RingCloudTech] BetterFoodEffect 的 Tags 不是 IDictionary，联动跳过");
                    return;
                }

                string ingredientId = SweetCaneRegistrar.FRUIT_ID;

                // 幂等：对方后续版本可能自己就加上了
                if (tags.Contains(ingredientId))
                {
                    Debug.Log("[RingCloudTech] BetterFoodEffect 已包含 " + ingredientId + "，无需重复登记");
                    return;
                }

                object category = Enum.Parse(catType, TargetCategory);
                Array categories = Array.CreateInstance(catType, 1);
                categories.SetValue(category, 0);

                tags[ingredientId] = categories;

                Debug.Log("[RingCloudTech] 已与 BetterFoodEffect 联动："
                    + ingredientId + " 登记为 " + TargetCategory + "（甜美水果）食材");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] BetterFoodEffect 联动失败（不影响本 mod 其他功能）: " + e);
            }
        }
    }

    /// <summary>
    /// 在 Db.Initialize 之后注册联动。
    ///
    /// 时机选择：
    ///   必须早于香料选择界面构建（NutritionSpiceCatalog 会读取分类里的食材列表），
    ///   而 Db.Initialize 在 UI 之前、且此时 Mod 的静态类型都已可访问。
    /// </summary>
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class BetterFoodEffectIntegrationPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            BetterFoodEffectIntegration.TryRegister();
        }
    }
}
