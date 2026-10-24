using System.Collections.Generic;
using HarmonyLib;

namespace RingCloudTech
{
    /// <summary>
    /// 把咖啡树种子加进打印舱的补给包列表，否则游戏里没有途径获得种子。
    ///
    /// 结构与 SweetCaneCarePackage 完全一致：
    /// Immigration.ConfigureCarePackages 是私有方法，它构建的 carePackages
    /// 也是私有字段，只能靠 FieldRefAccess 访问。
    /// </summary>
    [HarmonyPatch(typeof(Immigration), "ConfigureCarePackages")]
    internal static class CoffeeTreeCarePackagePatch
    {
        private const float SEED_AMOUNT = 3f;

        [HarmonyPostfix]
        internal static void Postfix(Immigration __instance)
        {
            List<CarePackageInfo> packages = AccessTools
                .FieldRefAccess<Immigration, List<CarePackageInfo>>("carePackages")(__instance);

            if (packages == null)
            {
                return;
            }

            // 幂等保护
            if (packages.Exists(p => p.id == CoffeeTreeRegistrar.SEED_ID))
            {
                return;
            }

            packages.Add(new CarePackageInfo(
                CoffeeTreeRegistrar.SEED_ID,
                SEED_AMOUNT,
                null));
        }
    }
}
