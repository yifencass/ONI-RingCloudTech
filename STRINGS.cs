namespace RingCloudTech
{
    /// <summary>
    /// 所有可翻译文本。默认值为英文，中文由 translations/zh.po 覆盖。
    /// 嵌套路径直接对应游戏查询的 key，例如
    ///     STRINGS.ELEMENTS.COLA.NAME
    ///     STRINGS.BUILDINGS.PREFABS.BEVERAGEMIXER.NAME
    /// 由 Localization 补丁里的 LocString.CreateLocStringKeys 自动注册。
    /// </summary>
    public static class STRINGS
    {
        public static class ELEMENTS
        {
            public static class COLA
            {
                public static LocString NAME = "Cola";
                public static LocString DESC =
                    "A sweet-and-spicy carbonated drink. Pincha Peppernut and sucrose, in perfect proportion, warm you right through.";
            }
        }

        public static class BUILDINGS
        {
            public static class PREFABS
            {
                public static class BEVERAGEMIXER
                {
                    public static LocString NAME = "Beverage Mixer";
                    public static LocString DESC =
                        "A machine that blends liquids, gases and spices in exact proportions.";
                    public static LocString EFFECT =
                        "Combines Water, Carbon Dioxide and Pincha Peppernut into Cola.";
                }
            }
        }

        public static class DUPLICANTS
        {
            public static class MODIFIERS
            {
                public static class DUPLICANTDRANKCOLA
                {
                    public static LocString NAME = "Drank Cola";
                    public static LocString TOOLTIP =
                        "A chilled glass of Cola. Stress melts away, spirits lift.";
                }
            }
        }

        public static class UI
        {
            public static class BEVERAGEMIXER
            {
                public static LocString RECIPE_NAME = "Cola";
                public static LocString RECIPE_DESC =
                    "Water, Carbon Dioxide and Pincha Peppernut, fused under pressure into a glass of Cola.";
            }
        }
    }
}
