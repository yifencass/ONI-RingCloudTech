using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 集中注册甜蔗的「食物定义」与「作物定义」。
    ///
    /// 必须早于植物 prefab 生成：
    ///   EntityTemplates.ExtendEntityToBasicPlant 会执行
    ///       CROPS.CROP_TYPES.Find(m => m.cropId == crop_id)
    ///   并直接取 .numProduced / .cropDuration（EntityTemplates.cs:274-279），
    ///   找不到就会抛 NullReferenceException。
    ///
    /// 字符串由 LocalizationPatch 统一注册（Localization.Initialize 的 Postfix），
    /// 那个 Postfix 早于 prefab 生成，所以这里拿到的 LocString 已经是当前语言了。
    ///
    /// 数值设计：
    ///   生长     6 周期（养殖）= 3600 秒；野生 12 周期
    ///   产量     每次收获 1 份，6000 千卡
    ///   灌溉     10 kg/周期 → 10/600 kg/s
    ///   副作用   食用后口渴
    ///   品质     1（士气 +1，因为士气加成直接等于 Quality 值）
    /// </summary>
    internal static class SweetCaneRegistrar
    {
        public const string PLANT_ID = "SweetCanePlant";
        public const string SEED_ID = "SweetCaneSeed";
        public const string FRUIT_ID = "SweetCane";

        /// <summary>1 周期 = 600 秒。</summary>
        public const float SECONDS_PER_CYCLE = 600f;

        /// <summary>养殖 6 周期成熟。</summary>
        public const float GROWTH_CYCLES = 6f;

        public const float CROP_DURATION = GROWTH_CYCLES * SECONDS_PER_CYCLE;

        /// <summary>
        /// 养殖生长速率 = 1/600，野生默认是它的 1/4（会变成 24 周期）。
        /// 这里取一半，让野生正好是养殖的 2 倍 = 12 周期。
        /// Growing.GROWTH_RATE / WILD_GROWTH_RATE 都是 public 字段，可按植物覆盖。
        /// </summary>
        public const float WILD_GROWTH_RATE = 0.0016666667f * 0.5f;

        /// <summary>每次收获份数。</summary>
        public const int YIELD_UNITS = 1;

        /// <summary>
        /// 每份热量（内部单位，1 千卡 = 1000）。6000000 = 6000 千卡。
        /// 数值偏高：甜蔗定位是「高糖」作物，6000 千卡对应它能榨出 8 kg 蔗糖。
        /// </summary>
        public const float KCAL_PER_UNIT = 6000000f;

        /// <summary>食物品质。士气加成 = 该值，所以 1 表示 +1 士气。</summary>
        public const int FOOD_QUALITY = 1;

        /// <summary>灌溉消耗：10 kg/周期。</summary>
        public const float WATER_RATE = 10f / SECONDS_PER_CYCLE;

        /// <summary>
        /// 施肥消耗：10 kg 泥土 / 周期（1/60 kg/s，和米虱木同速）。
        /// 甜蔗 6 周期成熟，也就是每轮消耗 60 kg 泥土。
        /// </summary>
        public const float FERTILIZER_RATE = 10f / SECONDS_PER_CYCLE;

        /// <summary>拔除植物时掉落的植物外壳数量（原版咸蔗也是 2）。</summary>
        public const float FIBER_ON_UPROOT = 2f;

        public static EdiblesManager.FoodInfo FruitInfo { get; private set; }

        private static bool _done;

        public static void EnsureRegistered()
        {
            if (_done)
            {
                return;
            }
            _done = true;

            // FoodInfo 构造函数会自我注册进 EdiblesManager（EdiblesManager.cs:82-83）
            FruitInfo = new EdiblesManager.FoodInfo(
                FRUIT_ID,
                KCAL_PER_UNIT,
                FOOD_QUALITY,
                255.15f,
                277.15f,
                4800f,
                can_rot: true);

            // 食用后口渴（同原版咸蔗 SaltySticksFood 的处理）
            FruitInfo.AddEffects(new List<string> { "Thirsty" });

            // 直接取 LocString 的显示文本（隐式转换走 _text）。不要传键名：
            // KSelectable.SetName / InfoDescription 都是原样存储、原样显示，不做查表。
            FruitInfo.Name = STRINGS.ITEMS.FOOD.SWEETCANE.NAME;
            FruitInfo.Description = STRINGS.ITEMS.FOOD.SWEETCANE.DESC;

            // 注册作物。CropVal 是 struct，要用 Exists 而不是 Find(...) == null
            if (!CROPS.CROP_TYPES.Exists(m => m.cropId == FRUIT_ID))
            {
                CROPS.CROP_TYPES.Add(new Crop.CropVal(FRUIT_ID, CROP_DURATION, YIELD_UNITS));
            }

            Debug.Log("[RingCloudTech] registered crop '" + FRUIT_ID + "' ("
                + GROWTH_CYCLES + " cycles domesticated / "
                + (GROWTH_CYCLES * 2f) + " wild, " + YIELD_UNITS + " unit, "
                + (KCAL_PER_UNIT / 1000f) + " kcal, quality " + FOOD_QUALITY + ")");
        }
    }
}
