using KSerialization;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 时间之弓的固体产物生成器。
    ///
    /// 语义：
    ///   本建筑每周期（600 s）产出 500 kg 中子质，折合 0.8333 kg/s。
    ///   只有在本建筑「真正发电/工作」时才累积产量 —— 判据是 Operational.IsActive，
    ///   因为 EnergyGenerator.EnergySim200ms 只在扣掉氦气并发电的那一帧调用
    ///   operational.SetActive(true)；没接电线（wireConnectedFlag 未满足）、管道没氦气、
    ///   被掩埋（Structure 会清掉 not_entombed 标志）时 IsOperational/IsActive 都会是 false。
    ///
    /// 产出位置：
    ///   BuildingDef.GenerateOffsets() 生成的 4 宽建筑 PlacementOffsets 是 x = -1..2、y = 0..2，
    ///   即贴图左下角原点 = Grid.PosToCell(建筑) + (-1, 0)。贴图 O 格（右边缘中排）
    ///   = 原点 + (3, 1) = 建筑单元格 + (2, 1)。
    ///
    /// 生成方式：
    ///   Substance.SpawnResource 没有 int cell 重载，第一个参数是世界坐标 Vector3，
    ///   所以用 Grid.CellToPosCCC 把目标格转成坐标再生成；病菌参数按原版 Emit 的固体分支
    ///   传 byte.MaxValue / 0（无病菌）。
    /// </summary>
    public class TimeBowSolidSpawner : KMonoBehaviour, ISim1000ms
    {
        /// <summary>产出速率：每周期 500 kg ÷ 600 s。</summary>
        public const float MASS_PER_SECOND = TimeBowConfig.NEUTRONIUM_KG_PER_SECOND;

        /// <summary>单块质量（kg）。攒够一块才生成，避免每秒刷一个 1 kg 的小物件。</summary>
        public const float CHUNK_MASS = 10f;

        /// <summary>
        /// 产出格（贴图 O、右边缘中排）相对「建筑单元格」的偏移。
        /// 见类注释：贴图原点 + (3,1) = 建筑单元格 + (2,1)。
        /// </summary>
        private static readonly CellOffset OUTPUT_OFFSET = new CellOffset(2, 1);

        /// <summary>已累积但还没凑够一块的中子质质量（kg）。随存档保存，读档不丢产出。</summary>
        [Serialize]
        private float bufferedMass;

        private Operational operational;
        private PrimaryElement primaryElement;
        private bool errorLogged;

        protected override void OnSpawn()
        {
            base.OnSpawn();

            operational = GetComponent<Operational>();
            primaryElement = GetComponent<PrimaryElement>();

            if (bufferedMass < 0f)
            {
                bufferedMass = 0f;
            }
        }

        public void Sim1000ms(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            // 只在真正发电/工作时产出
            if (operational == null || !operational.IsOperational || !operational.IsActive)
            {
                return;
            }

            bufferedMass += MASS_PER_SECOND * dt;
            while (bufferedMass >= CHUNK_MASS)
            {
                bufferedMass -= CHUNK_MASS;
                SpawnChunk(CHUNK_MASS);
            }
        }

        private void SpawnChunk(float mass)
        {
            Element element = ElementLoader.FindElementByHash(SimHashes.Unobtanium);
            if (element == null || element.substance == null)
            {
                LogErrorOnce("找不到 Unobtanium（中子质）元素或它的 Substance，产物停止生成");
                return;
            }

            // 原版中子质只作为地图边界/遗迹砖存在，不一定有可落地的 ore prefab。
            // 缺 prefab 时 SpawnResource 会在 KInstantiate(null) 处抛异常，这里提前拦下来给出可读日志。
            if (Assets.TryGetPrefab(element.tag) == null)
            {
                LogErrorOnce("Unobtanium 没有可生成的固体 prefab（Assets.TryGetPrefab 为空），产物无法生成");
                return;
            }

            int buildingCell = Grid.PosToCell(gameObject);
            if (!Grid.IsValidCell(buildingCell))
            {
                return;
            }

            int targetCell = Grid.OffsetCell(buildingCell, OUTPUT_OFFSET);
            if (!Grid.IsValidCell(targetCell))
            {
                return;
            }

            Vector3 position = Grid.CellToPosCCC(targetCell, Grid.SceneLayer.Front);

            // 产物温度取建筑自身温度；建筑 PrimaryElement 缺失时用 293 K（中子质元素默认温度是 1 K，不能直接用）。
            float temperature = primaryElement != null && primaryElement.Temperature > 0f
                ? primaryElement.Temperature
                : 293.15f;

            try
            {
                // 参数：(世界坐标, 质量, 温度, 病菌索引, 病菌数量, prevent_merge, forceTemperature, manual_activation)
                // prevent_merge 用默认 false：同格已有的中子质块会自动合并，避免一周期 50 个物件堆积。
                GameObject spawned = element.substance.SpawnResource(position, mass, temperature, byte.MaxValue, 0);
                Debug.Log(string.Format(
                    "[RingCloudTech] 时间之弓产出中子质 {0:0.###} kg：格={1} 温度={2:0.##} K 物件={3}",
                    mass, targetCell, temperature, spawned != null ? spawned.name : "<null>"));
            }
            catch (System.Exception e)
            {
                LogErrorOnce("生成中子质失败: " + e.Message);
            }
        }

        private void LogErrorOnce(string message)
        {
            if (errorLogged)
            {
                return;
            }

            errorLogged = true;
            Debug.LogWarning("[RingCloudTech] 时间之弓：" + message);
        }
    }
}
