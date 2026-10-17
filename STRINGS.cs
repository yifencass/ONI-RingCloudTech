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
                        "Blends ingredients into assorted beverages.";
                }

                public static class SUPERCAPACITOR
                {
                    public static LocString NAME = "Super Capacitor Bank";
                    public static LocString DESC =
                        "A rack of high-density supercapacitors. It holds far more charge than an ordinary battery and keeps a grid running long after the generators stop.";
                    public static LocString EFFECT =
                        "Stores up to <b>800 kJ</b> of Power.\n\nLeaks 50 J per second to internal resistance and emits 50 DTU/s of Heat.\n\nSends a Green Signal when its charge rises above the activation threshold.";
                    public static LocString LOGIC_PORT = "Battery Charge";
                    public static LocString LOGIC_PORT_ACTIVE =
                        "Sends a <b><style=\"logic_on\">Green Signal</style></b> when the charge rises above the activation threshold";
                    public static LocString LOGIC_PORT_INACTIVE =
                        "Sends a <b><style=\"logic_off\">Red Signal</style></b> when the charge falls below the deactivation threshold";
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
                    "Water, Carbon Dioxide, Sucrose and Pincha Peppernut, fused under pressure into a glass of Cola.";
            }
        }
    }
}
