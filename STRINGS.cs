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
                    "A caffeine-rich carbonated drink. Loaded with sucrose, so you'll need to eat less.";
            }

            public static class BLACKCOFFEE
            {
                public static LocString NAME = "黑咖啡";
                public static LocString DESC =
                    "通过咖啡豆冲泡的黑咖啡，与浓缩咖啡不同，他的味道绝对算不上好的\n但是复制人饮用后就不用睡觉了，如果能处理后黑咖啡带来的压力，他将是个不错的消遣液体";
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
                        "A mysterious device of unknown make. While it is supplied with power, a <b>simulated universe</b> is born at its core.\nThe <b>simulated universe</b> feeds on the neutronium produced by the Time Bow and, given vast amounts of hydrogen, yields matter far in excess of its input mass.";
                    public static LocString EFFECT =
                        "A monument to something that watched this place before the colony ever existed.";
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

                public static class NEUTRONIUMLIQUIDCONDUIT
                {
                    public static LocString NAME = "Neutronium Liquid Pipe";
                    public static LocString DESC =
                        "A pipe drawn from neutronium. Nothing else survives the pressure it is built to carry.";
                    public static LocString EFFECT =
                        "Carries <b>Liquid</b> between buildings.\n\nHolds up to <b>5 t</b> per segment.";
                }

                public static class NEUTRONIUMGASCONDUIT
                {
                    public static LocString NAME = "Neutronium Gas Pipe";
                    public static LocString DESC =
                        "A pipe drawn from neutronium. Its walls shrug off the pressure that would burst any other pipe.";
                    public static LocString EFFECT =
                        "Carries <b>Gas</b> between buildings.\n\nHolds up to <b>1 t</b> per segment.";
                }

                public static class NEUTRONIUMSOLIDCONDUIT
                {
                    public static LocString NAME = "Neutronium Conveyor Rail";
                    public static LocString DESC =
                        "A rail drawn from neutronium. It moves loads that would shear an ordinary conveyor apart.";
                    public static LocString EFFECT =
                        "Carries <b>Solid</b> materials between buildings.\n\nMoves objects of up to <b>10 t</b> each.";
                }

                public static class TIMEBOW
                {
                    public static LocString NAME = "Time Bow";
                    public static LocString DESC =
                        "Give time to matter, and it yields immense clean power. Because of an improper operation our \"homeland\" was blasted apart; for safety, it refuses to work unless helium is fed in, and while working it produces a special substance - neutronium.";
                    public static LocString EFFECT =
                        "Generates <b>800 W</b> of Power.\n\nConsumes <b>30 kg</b> of <b>Helium</b> per cycle.\n\nProduces <b>500 kg</b> of <b>Neutronium</b> per cycle from its right edge.\n\nEmits no heat.";
                }

                public static class SMARTCHILLER
                {
                    public static LocString NAME = "Smart Liquid Chiller";
                    public static LocString DESC =
                        "A chiller that reads a number and obeys it. Set the temperature you want coming out, and it holds your coolant there - chilling whatever arrives too warm, and waving through whatever is already cold enough.";
                    public static LocString EFFECT =
                        "Cools <b>Liquid</b> down to the <b>target temperature</b> shown on its display.\n\nConsumes <b>1800 W</b> of Power while chilling.\n\nLiquid that is already at or below the target passes through untouched, using no power.\n\nHeat removed from the liquid is dumped into the building itself.";
                }
            }
        }

        public static class RESEARCH
        {
            public static class TECHS
            {
                public static class PASSIVEENERGY
                {
                    public static LocString NAME = "Passive Energy";
                    public static LocString DESC =
                        "Draw power out of processes that were never meant to be harnessed. Nothing burns, nothing decays - the current simply arrives.";
                }

                public static class POCKETUNIVERSE
                {
                    public static LocString NAME = "Pocket Universe";
                    public static LocString DESC =
                        "Fold a universe small enough to fit inside a machine. Feeding it matter is optional; feeding it neutronium is not.";
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

                    public static class COFFEETREE
                    {
                        public static LocString NAME = "Coffee Tree Seed";
                        public static LocString DESC =
                            "The seed of a Coffee Tree. Plant it in bright light to harvest Coffee Beans.";
                    }
                }

                // ── 咖啡树（CoffeeTree）──────────────────────────
                public static class COFFEETREE
                {
                    public static LocString NAME = "Coffee Tree";
                    public static LocString DESC =
                        "A close relative of the Pincha Peppernut Vine. Its fruit has lost all of the peppernut's heat, but carries a far higher concentration of caffeine.\n\nIt spends most of its life slowly laying down a woody stem, making it a painfully slow crop to grow, but it asks for nothing except abundant light.";
                    public static LocString DOMESTICATEDDESC =
                        "This plant has been domesticated. It does not need fertiliser, but demands strong light to grow.";
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

                public static class COFFEEBEAN
                {
                    public static LocString NAME = "Coffee Bean";
                    public static LocString DESC =
                        "The roasted seed of a Coffee Tree. It has no flavour worth eating on its own, but brewed properly it will wake a Duplicant right up.";
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
