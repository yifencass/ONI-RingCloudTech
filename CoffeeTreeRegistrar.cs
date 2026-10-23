using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 集中注册咖啡树的「食物定义」与「作物定义」。
    ///
    /// 和 SweetCaneRegistrar 同样的道理：必须早于植物 prefab 生成。
    /// EntityTemplates.ExtendEntityToBasicPlant 会执行
    ///     CROPS.CROP_TYPES.Find(m => m.cropId == crop_id)
    /// 并直接取 .numProduced / .cropDuration（EntityTemplates.cs:274-279），
    /// 找不到就抛 NullReferenceException。
    ///
    /// 数值设计（玩家指定）：
    ///   定位       火椒树的近亲，产物失去辛辣口感，咖啡因浓度远高于火椒粒
    ///   形态       1×2 直立，用虫果芽「正常版（super_*）」美术
    ///   培育生长   18 周期（木质茎，生长期漫长）
    ///   光照需求   900 勒克斯（毛刺花是 200）
    ///   温度       -10℃ ~ 70℃
    ///   施肥       不需要（刻意不调用 ExtendPlantToFertilizable）
    ///   产物       咖啡豆，热量 0 → 不可直接食用（同火椒粒）
    /// </summary>
    internal static class CoffeeTreeRegistrar
    {
        public const string PLANT_ID = "CoffeeTree";
        public const string SEED_ID = "CoffeeTreeSeed";
        public const string BEAN_ID = "CoffeeBean";
        public const string PREVIEW_ID = "CoffeeTree_preview";

        // ── 动画 ──────────────────────────────────────────────
        /// <summary>
        /// 植株本体：自建的彩色 kanim 副本。
        ///
        /// 内容是原版虫果芽 wormwood 图集的完整拷贝，只把内部名改成 coffee_tree。
        /// 为什么不直接引用原版 `wormwood_kanim`：
        ///   1) 它属于太空 DLC 资产，DLC 未激活或资产未加载时 Assets.GetAnim 会返回
        ///      null，植株就没贴图，只剩预览白描（正是之前的症状）
        ///   2) 自带一份可以让本 mod 的植株/预览完全自洽，不受原版资源变动影响
        ///
        /// 动画剪辑里同时含 basic_*（贫瘠）与 super_*（正常），
        /// 我们在 CoffeeTreePlantConfig.ANIMS 里取 super_*。
        /// </summary>
        public const string PLANT_ANIM = "coffee_tree_kanim";

        /// <summary>预览：把同一张图集整图转成白色线稿后的副本，播 super_idle_full。</summary>
        public const string PREVIEW_ANIM = "coffee_tree_preview_kanim";

        public const string SEED_ANIM = "coffee_tree_seed_kanim";
        public const string BEAN_ANIM = "coffee_bean_kanim";

        /// <summary>1 周期 = 600 秒。</summary>
        public const float SECONDS_PER_CYCLE = 600f;

        /// <summary>培育生长期 18 周期。</summary>
        public const float GROWTH_CYCLES = 18f;

        /// <summary>
        /// 作物时长（秒）。EntityTemplates 会把 Maturity.max 设成
        /// cropDuration / 600，也就是「周期数」；配合 GROWTH_RATE = 1/600 每秒，
        /// 实际成熟时间正好是 18 × 600 秒。
        /// </summary>
        public const float CROP_DURATION = GROWTH_CYCLES * SECONDS_PER_CYCLE;

        /// <summary>
        /// 野生生长速率。留空即用原版默认 1/2400（养殖的 1/4），
        /// 也就是野生 18×4 = 72 周期。这里显式写出来便于日后调整。
        /// </summary>
        public const float WILD_GROWTH_RATE = 0.00041666668f;

        /// <summary>生长所需最低光照：900 勒克斯。</summary>
        public const float MIN_LIGHT_LUX = 900f;

        // ── 体温（开尔文）─────────────────────────────────────
        /// <summary>-10℃ —— 玩家指定的存活下限。</summary>
        public const float TEMP_WARNING_LOW = 263.15f;

        /// <summary>70℃ —— 玩家指定的存活上限。</summary>
        public const float TEMP_WARNING_HIGH = 343.15f;

        /// <summary>-50℃，超出即死亡。</summary>
        public const float TEMP_LETHAL_LOW = 223.15f;

        /// <summary>120℃，超出即死亡。</summary>
        public const float TEMP_LETHAL_HIGH = 393.15f;

        // ── 资源 ──────────────────────────────────────────────
        /// <summary>灌溉：污染水 7/120 kg/s（沿用火椒藤）。</summary>
        public const float WATER_RATE = 7f / 120f;

        /// <summary>每次收获份数。</summary>
        public const int YIELD_UNITS = 2;

        /// <summary>拔除植物时掉落的植物外壳数量（火椒藤是 16）。</summary>
        public const float FIBER_ON_UPROOT = 16f;

        public static EdiblesManager.FoodInfo BeanInfo { get; private set; }

        private static bool _done;

        public static void EnsureRegistered()
        {
            if (_done)
            {
                return;
            }
            _done = true;

            // 热量 0 —— 和原版火椒粒 SpiceNut 完全一样的「不可直接食用」处理。
            // TUNING.FOOD.cs:156
            //     SPICENUT = new FoodInfo(SpiceNutConfig.ID, 0f, 0, ...)
            // 热量为 0 时 Edible 不会提供进食选项，但物品本身仍可作为食材参与配方。
            BeanInfo = new EdiblesManager.FoodInfo(
                BEAN_ID,
                0f,          // ← 0 千卡：不可直接食用
                0,           // 品质
                255.15f,
                277.15f,
                4800f,
                can_rot: true);

            // 直接取 LocString 的显示文本，不要传键名
            BeanInfo.Name = STRINGS.ITEMS.FOOD.COFFEEBEAN.NAME;
            BeanInfo.Description = STRINGS.ITEMS.FOOD.COFFEEBEAN.DESC;

            // 注册作物。CropVal 是 struct，用 Exists 而不是 Find(...) == null
            if (!CROPS.CROP_TYPES.Exists(m => m.cropId == BEAN_ID))
            {
                CROPS.CROP_TYPES.Add(new Crop.CropVal(BEAN_ID, CROP_DURATION, YIELD_UNITS));
            }

            Debug.Log("[RingCloudTech] registered crop '" + BEAN_ID + "' ("
                + GROWTH_CYCLES + " cycles domesticated / "
                + (GROWTH_CYCLES * 4f) + " wild, " + YIELD_UNITS + " units, "
                + "0 kcal so not edible, needs " + MIN_LIGHT_LUX + " lux)");
        }
    }
}
