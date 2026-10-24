using System.Collections.Generic;
using HarmonyLib;
using Klei;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 中子质管道：容量登记表。
    /// ONI 的 ConduitFlow 每种类型只有一个 MaxMass（液体 10 kg、气体 1 kg），
    /// 这里用「格 → 容量」的字典把它逐格化，只有中子质管道所在格放大。
    /// </summary>
    public static class NeutroniumConduitRegistry
    {
        public const float LIQUID_CAPACITY = 5000f;   // 5 吨
        public const float GAS_CAPACITY = 1000f;      // 1 吨
        public const float SOLID_CAPACITY = 10000f;   // 10 吨

        private static readonly Dictionary<int, float> Liquid = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> Gas = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> Solid = new Dictionary<int, float>();

        public static float CapacityOf(ConduitType type)
        {
            switch (type)
            {
                case ConduitType.Gas:
                    return GAS_CAPACITY;
                case ConduitType.Liquid:
                    return LIQUID_CAPACITY;
                case ConduitType.Solid:
                    return SOLID_CAPACITY;
                default:
                    return 0f;
            }
        }

        private static Dictionary<int, float> Bucket(ConduitType type)
        {
            switch (type)
            {
                case ConduitType.Gas:
                    return Gas;
                case ConduitType.Liquid:
                    return Liquid;
                case ConduitType.Solid:
                    return Solid;
                default:
                    return null;
            }
        }

        public static void Register(int cell, ConduitType type)
        {
            Dictionary<int, float> bucket = Bucket(type);
            if (bucket != null && cell >= 0)
            {
                bucket[cell] = CapacityOf(type);
            }
        }

        public static void Unregister(int cell, ConduitType type)
        {
            Dictionary<int, float> bucket = Bucket(type);
            if (bucket != null)
            {
                bucket.Remove(cell);
            }
        }

        public static bool TryGet(int cell, ConduitType type, out float capacity)
        {
            Dictionary<int, float> bucket = Bucket(type);
            if (bucket != null && bucket.TryGetValue(cell, out capacity))
            {
                return true;
            }

            capacity = 0f;
            return false;
        }

        /// <summary>已登记的管格总数。</summary>
        public static int Count
        {
            get
            {
                return Gas.Count + Liquid.Count + Solid.Count;
            }
        }

        /// <summary>取某一类型的所有已登记管格。</summary>
        public static List<int> CellsOf(ConduitType type)
        {
            List<int> result = new List<int>();
            foreach (KeyValuePair<int, float> pair in Bucket(type))
            {
                result.Add(pair.Key);
            }

            return result;
        }

        public static float MaxCapacity(ConduitType type)
        {
            return CapacityOf(type);
        }

        public static bool Any(ConduitType type)
        {
            Dictionary<int, float> bucket = Bucket(type);
            return bucket != null && bucket.Count > 0;
        }
    }

    /// <summary>调试开关：把管道注册情况打到 Player.log。</summary>
    public static class RingCloudTechDebug
    {
        public const bool LogConduits = true;
    }

    /// <summary>挂在中子质管道上，把所在格登记进容量表。</summary>
    public class NeutroniumConduitHandler : KMonoBehaviour
    {
        public ConduitType conduitType = ConduitType.Liquid;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            int cell = Grid.PosToCell(gameObject);
            NeutroniumConduitRegistry.Register(cell, conduitType);

            if (RingCloudTechDebug.LogConduits)
            {
                bool solidHas = Game.Instance != null && Game.Instance.solidConduitFlow != null
                    && Game.Instance.solidConduitFlow.HasConduit(cell);
                bool liquidHas = Game.Instance != null && Game.Instance.liquidConduitFlow != null
                    && Game.Instance.liquidConduitFlow.HasConduit(cell);
                bool gasHas = Game.Instance != null && Game.Instance.gasConduitFlow != null
                    && Game.Instance.gasConduitFlow.HasConduit(cell);
                Debug.Log("[RingCloudTech] 中子质管道生成 type=" + conduitType + " cell=" + cell
                    + " solid=" + solidHas + " liquid=" + liquidHas + " gas=" + gasHas);
            }
        }

        protected override void OnCleanUp()
        {
            NeutroniumConduitRegistry.Unregister(Grid.PosToCell(gameObject), conduitType);
            base.OnCleanUp();
        }
    }

    /// <summary>三种中子质管道的公共 Def 设置。</summary>
    internal static class NeutroniumConduitDefs
    {
        public static BuildingDef Make(string id, string anim, HashedString viewMode,
            ObjectLayer objectLayer, ObjectLayer tileLayer, ObjectLayer replacementLayer,
            Grid.SceneLayer sceneLayer, float massPerSegment)
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                id, 1, 1, anim, 10, 3f,
                new float[] { massPerSegment },
                new string[] { "Unobtanium" },
                1600f,
                BuildLocationRule.Anywhere,
                BUILDINGS.DECOR.NONE,
                NOISE_POLLUTION.NONE);

            def.Floodable = false;
            def.Overheatable = false;
            def.Entombable = false;
            def.ViewMode = viewMode;
            def.ObjectLayer = objectLayer;
            def.TileLayer = tileLayer;
            def.ReplacementLayer = replacementLayer;
            def.AudioCategory = "Metal";
            def.AudioSize = "small";
            def.BaseTimeUntilRepair = -1f;
            def.UtilityInputOffset = new CellOffset(0, 0);
            def.UtilityOutputOffset = new CellOffset(0, 0);
            def.SceneLayer = sceneLayer;
            def.isKAnimTile = true;
            def.isUtility = true;
            def.DragBuild = true;
            def.MaterialCategory = new string[] { "Unobtanium" };
            def.Mass = new float[] { massPerSegment };
            return def;
        }
    }

    /// <summary>中子质液体管道：单格 5 吨。</summary>
    public class NeutroniumLiquidConduitConfig : IBuildingConfig
    {
        public const string ID = "NeutroniumLiquidConduit";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = NeutroniumConduitDefs.Make(ID, "utilities_liquid_kanim",
                OverlayModes.LiquidConduits.ID,
                ObjectLayer.LiquidConduit,
                ObjectLayer.LiquidConduitTile,
                ObjectLayer.ReplacementLiquidConduit,
                Grid.SceneLayer.LiquidConduits,
                500f);
            def.ReplacementTags = new List<Tag> { GameTags.Pipes };
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.LiquidVentIDs, ID);
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            GeneratedBuildings.MakeBuildingAlwaysOperational(go);
            BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
            go.AddOrGet<Conduit>().type = ConduitType.Liquid;
            go.AddOrGet<NeutroniumConduitHandler>().conduitType = ConduitType.Liquid;
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.GetComponent<Building>().Def.BuildingUnderConstruction.GetComponent<Constructable>().isDiggingRequired = false;
            go.AddComponent<EmptyConduitWorkable>();
            KAnimGraphTileVisualizer vis = go.AddComponent<KAnimGraphTileVisualizer>();
            vis.connectionSource = KAnimGraphTileVisualizer.ConnectionSource.Liquid;
            vis.isPhysicalBuilding = true;
            go.GetComponent<KPrefabID>().AddTag(GameTags.Pipes);
            GeneratedBuildings.RemoveLoopingSounds(go);
        }

        public override void DoPostConfigureUnderConstruction(GameObject go)
        {
            KAnimGraphTileVisualizer vis = go.AddComponent<KAnimGraphTileVisualizer>();
            vis.connectionSource = KAnimGraphTileVisualizer.ConnectionSource.Liquid;
            vis.isPhysicalBuilding = false;
        }
    }

    /// <summary>中子质气体管道：单格 1 吨。</summary>
    public class NeutroniumGasConduitConfig : IBuildingConfig
    {
        public const string ID = "NeutroniumGasConduit";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = NeutroniumConduitDefs.Make(ID, "utilities_gas_kanim",
                OverlayModes.GasConduits.ID,
                ObjectLayer.GasConduit,
                ObjectLayer.GasConduitTile,
                ObjectLayer.ReplacementGasConduit,
                Grid.SceneLayer.GasConduits,
                300f);
            def.ReplacementTags = new List<Tag> { GameTags.Pipes };
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.GasVentIDs, ID);
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            GeneratedBuildings.MakeBuildingAlwaysOperational(go);
            BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
            go.AddOrGet<Conduit>().type = ConduitType.Gas;
            go.AddOrGet<NeutroniumConduitHandler>().conduitType = ConduitType.Gas;
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.GetComponent<Building>().Def.BuildingUnderConstruction.GetComponent<Constructable>().isDiggingRequired = false;
            go.AddComponent<EmptyConduitWorkable>();
            KAnimGraphTileVisualizer vis = go.AddComponent<KAnimGraphTileVisualizer>();
            vis.connectionSource = KAnimGraphTileVisualizer.ConnectionSource.Gas;
            vis.isPhysicalBuilding = true;
            go.GetComponent<KPrefabID>().AddTag(GameTags.Pipes);
            GeneratedBuildings.RemoveLoopingSounds(go);
        }

        public override void DoPostConfigureUnderConstruction(GameObject go)
        {
            KAnimGraphTileVisualizer vis = go.AddComponent<KAnimGraphTileVisualizer>();
            vis.connectionSource = KAnimGraphTileVisualizer.ConnectionSource.Gas;
            vis.isPhysicalBuilding = false;
        }
    }

    /// <summary>中子质运输轨道：单件 10 吨。</summary>
    public class NeutroniumSolidConduitConfig : IBuildingConfig
    {
        public const string ID = "NeutroniumSolidConduit";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = NeutroniumConduitDefs.Make(ID, "utilities_conveyor_kanim",
                OverlayModes.SolidConveyor.ID,
                ObjectLayer.SolidConduit,
                ObjectLayer.SolidConduitTile,
                ObjectLayer.ReplacementSolidConduit,
                Grid.SceneLayer.SolidConduits,
                500f);
            def.BaseTimeUntilRepair = 0f;
            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.SolidConveyorIDs, ID);
            def.ReplacementTags = null;
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            GeneratedBuildings.MakeBuildingAlwaysOperational(go);
            BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
            go.AddOrGet<SolidConduit>();
            go.AddOrGet<NeutroniumConduitHandler>().conduitType = ConduitType.Solid;
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.GetComponent<Building>().Def.BuildingUnderConstruction.GetComponent<Constructable>().isDiggingRequired = false;
            KAnimGraphTileVisualizer vis = go.AddComponent<KAnimGraphTileVisualizer>();
            vis.connectionSource = KAnimGraphTileVisualizer.ConnectionSource.Solid;
            vis.isPhysicalBuilding = true;
            GeneratedBuildings.RemoveLoopingSounds(go);
            go.AddComponent<EmptySolidConduitWorkable>();
        }

        public override void DoPostConfigureUnderConstruction(GameObject go)
        {
            KAnimGraphTileVisualizer vis = go.AddComponent<KAnimGraphTileVisualizer>();
            vis.connectionSource = KAnimGraphTileVisualizer.ConnectionSource.Solid;
            vis.isPhysicalBuilding = false;
            go.GetComponent<Constructable>().requiredSkillPerk = Db.Get().SkillPerks.ConveyorBuild.Id;
        }
    }

    /// <summary>逐格容量：AddElement 时按所在格临时把 MaxMass 抬到该格容量。</summary>
        // 中子质管道整体禁用：[HarmonyPatch(typeof(ConduitFlow), "AddElement")]
    public static class ConduitFlow_AddElement_Patch
    {
        private static readonly AccessTools.FieldRef<ConduitFlow, float> MaxMass =
            AccessTools.FieldRefAccess<ConduitFlow, float>("MaxMass");

        private static void Prefix(ConduitFlow __instance, int cell_idx, out float __state)
        {
            __state = MaxMass(__instance);
            float capacity;
            if (NeutroniumConduitRegistry.TryGet(cell_idx, __instance.conduitType, out capacity) && capacity > __state)
            {
                MaxMass(__instance) = capacity;
            }
        }

        private static void Postfix(ConduitFlow __instance, float __state)
        {
            MaxMass(__instance) = __state;
        }
    }

    /// <summary>模拟期放开上限，让大包能被整格推进（没人注入就不会凭空产生大包）。</summary>
        // 中子质管道整体禁用：[HarmonyPatch(typeof(ConduitFlow), "Sim200ms")]
    public static class ConduitFlow_Sim200ms_Patch
    {
        private static readonly AccessTools.FieldRef<ConduitFlow, float> MaxMass =
            AccessTools.FieldRefAccess<ConduitFlow, float>("MaxMass");

        private static void Prefix(ConduitFlow __instance, out float __state)
        {
            __state = MaxMass(__instance);
            float capacity = NeutroniumConduitRegistry.MaxCapacity(__instance.conduitType);
            if (NeutroniumConduitRegistry.Any(__instance.conduitType) && capacity > __state)
            {
                MaxMass(__instance) = capacity;
            }
        }

        private static void Postfix(ConduitFlow __instance, float __state)
        {
            MaxMass(__instance) = __state;
        }
    }

}
