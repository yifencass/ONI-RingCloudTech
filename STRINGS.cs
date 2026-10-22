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

                public static class HARMONYEYE
                {
                    public static LocString NAME = "Eye of Harmony";
                    public static LocString DESC =
                        "A ring of unknown alloy suspended around a sphere of absolute darkness. The sphere only exists while the device is running.";
                    public static LocString EFFECT =
                        "A monument to something that watched the colony before it existed.\n\nOnly the machine's frame is built. Whatever sits at its centre is not built, merely <b>allowed</b>.";
                }

                public static class SUPERCAPACITOR
                {
                    public static LocString NAME = "Super Capacitor Bank";
                    public static LocString DESC =
                        "A rack of high-density supercapacitors. It holds far more charge than an ordinary battery and keeps a grid running long after the generators stop.";
                    public static LocString EFFECT =
                        "Stores up to <b>800 kJ</b> of Power.\n\nLeaks 60 J per cycle to internal resistance and emits 50 DTU/s of Heat.\n\nSends a Green Signal when its charge rises above the activation threshold.";
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
            public static class HARMONYEYE
            {
                public static LocString RECIPE_NAME = "Molten Iron";
                public static LocString RECIPE_DESC =
                    "One kilogram of Hydrogen, unmade and remade as one hundred kilograms of molten iron.";
            }

            public static class BEVERAGEMIXER
            {
                public static LocString RECIPE_NAME = "Cola";
                public static LocString RECIPE_DESC =
                    "Water, Carbon Dioxide, Sucrose and Pincha Peppernut, fused under pressure into a glass of Cola.";
            }

            public static class SWEETCANE_CRUSH
            {
                public static LocString RECIPE_NAME = "Sucrose and Plant Husk";
                public static LocString RECIPE_DESC =
                    "Sweet Cane run through the crusher, split into its sugar and its husk.";
            }
        }

        // ── 甜蔗（SweetCane）────────────────────────────────────────

        public static class CREATURES
        {
            public static class SPECIES
            {
                public static class SWEETCANE
                {
                    public static LocString NAME = "Sweet Cane";
                    public static LocString DESC =
                        "A variant of Salty Sticks, with sucrose crystals growing along its stalk. Duplicants adore eating it, but the osmotic pressure from all that sugar leaves them thirsty.";
                    public static LocString DOMESTICATEDDESC =
                        "This plant has been domesticated and needs irrigation to grow.";
                }

                public static class SEEDS
                {
                    public static class SWEETCANE
                    {
                        public static LocString NAME = "Sweet Cane Seed";
                        public static LocString DESC =
                            "The seed of a Sweet Cane. Plant it to harvest Sweet Cane.";
                    }
                }
            }
        }

        public static class ITEMS
        {
            public static class FOOD
            {
                public static class SWEETCANE
                {
                    public static LocString NAME = "Sweet Cane";
                    public static LocString DESC =
                        "A variant of Salty Sticks, its stalk studded with crystallised sucrose. Duplicants love it, but it leaves them thirsty.";
                }
            }
        }

        public static class CODEX
        {
            public static class SWEETCANE
            {
                public static LocString TITLE = "Sweet Cane";
                public static LocString SUBTITLE = "A variant of Salty Sticks";

                public static class BODY
                {
                    public static LocString CONTAINER1 =
                        "Sweet Cane is a variant of Salty Sticks. Its stalk bears crystallised sucrose, formed naturally as sugar accumulates between the fibres.\n\nDuplicants are very fond of eating Sweet Cane.\n\nBut Sweet Cane contains far too much sugar, and the osmotic pressure it brings leaves whoever eats it thirsty.";
                }
            }
        }
    }
}
