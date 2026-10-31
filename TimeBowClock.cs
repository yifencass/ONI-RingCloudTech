using System;
using System.IO;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 时间之弓表盘：让左中那张机械钟的三根指针真正转起来。
    ///
    /// 美术分层在 `_assets/time_bow_layers/`：机身（body）+ 时针 + 分针 + 秒针，
    /// 四张图共用同一块 1024×768 画布，指针枢轴在 (232, 274)（从左上角量）。
    /// 贴图与建筑占位一一对应：1024×768 = 4×3 格，即 256 px / 格。
    ///
    /// 运行时把三张指针 PNG 读成 Sprite（pivot 设在枢轴处、PPU = 256），
    /// 各自挂一个子对象，每帧按时间角换算 Z 轴旋转。这样指针在任意角度都是
    /// 矢量般的干净边缘，不需要做逐帧 kanim。
    /// </summary>
    public class TimeBowClock : KMonoBehaviour
    {
        // ── 与美术对齐的常量（改动美术时同步改这里）──
        private const float ART_W = 1024f;
        private const float ART_H = 768f;
        private const float PIVOT_X = 232f;
        private const float PIVOT_Y = 274f;      // 从画布顶边向下量
        private const float PX_PER_CELL = 256f;  // 1024 / 4 格

        private const string ART_DIR = "timebow_clock";
        private const string HAND_HOUR = "hand_hour.png";
        private const string HAND_MINUTE = "hand_minute.png";
        private const string HAND_SECOND = "hand_second.png";

        /// <summary>子对象相对机身的 Z 偏移：往摄像机方向挪一点，保证盖在机身贴图上。</summary>
        private const float Z_OFFSET = -0.05f;

        /// <summary>待机时按真实时间走（秒针 60 秒一圈）。</summary>
        private const float IDLE_TIME_SCALE = 1f;

        /// <summary>
        /// 工作（真正在发电）时的「时间流速」倍率。
        /// 60 = 一秒当一分钟：秒针每秒 1 圈、分针每 60 秒 1 圈、时针每 12 分钟 1 圈，
        /// 视觉上能明显看出「时间被加速了」，又不会糊成一片。
        /// </summary>
        private const float WORK_TIME_SCALE = 60f;

        /// <summary>倍率过渡时间常数（秒），避免开关瞬间速度突变。</summary>
        private const float SPEED_SMOOTHING = 2f;

        private Transform hourHand;
        private Transform minuteHand;
        private Transform secondHand;

        /// <summary>三根针各自的累计角度（度，顺时针为正）。</summary>
        private float secondAngle;
        private float minuteAngle;
        private float hourAngle;

        /// <summary>当前时间倍率（待机 1，工作时 60，平滑过渡）。</summary>
        private float timeScale = IDLE_TIME_SCALE;

        private Generator generator;
        private bool built;

        protected override void OnSpawn()
        {
            base.OnSpawn();

            // 只给「已建成」的建筑挂指针：建造中/蓝图用的是同一份模板，
            // 拿不到 BuildingComplete 就说明还没造好。
            if (GetComponent<BuildingComplete>() == null)
            {
                return;
            }

            BuildHands();
        }

        private void BuildHands()
        {
            try
            {
                string modDir = Path.GetDirectoryName(typeof(TimeBowClock).Assembly.Location);
                string artDir = Path.Combine(modDir ?? string.Empty, ART_DIR);

                Sprite hourSprite = LoadSprite(Path.Combine(artDir, HAND_HOUR));
                Sprite minuteSprite = LoadSprite(Path.Combine(artDir, HAND_MINUTE));
                Sprite secondSprite = LoadSprite(Path.Combine(artDir, HAND_SECOND));

                if (hourSprite == null || minuteSprite == null || secondSprite == null)
                {
                    Debug.LogWarning("[RingCloudTech] 时间之弓表盘：指针贴图缺失，表盘保持静态");
                    return;
                }

                Vector3 center = GetClockWorldPosition();
                hourHand = MakeHand("TimeBow_hand_hour", hourSprite, center);
                minuteHand = MakeHand("TimeBow_hand_minute", minuteSprite, center);
                secondHand = MakeHand("TimeBow_hand_second", secondSprite, center);

                Debug.Log("[RingCloudTech] 时间之弓表盘：装好三根指针，中心=" + center +
                          "，建筑格=" + Grid.PosToCell(this) +
                          "，transform=" + transform.GetPosition());

                // 初始角度：读档时按当前游戏时钟摆出正确位置，不是每次都从 12 点开始
                float gameTime = GameClock.Instance != null ? GameClock.Instance.GetTime() : 0f;
                hourAngle = gameTime % 43200f / 43200f * 360f;
                minuteAngle = gameTime % 3600f / 3600f * 360f;
                secondAngle = gameTime % 60f / 60f * 360f;
                ApplyAngles();

                built = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 时间之弓表盘构建失败: " + e);
            }
        }

        /// <summary>
        /// 表盘中心的 world 坐标。
        ///
        /// 坐标系依据（都从反编译代码核过）：
        ///   · 建筑 transform 在 Grid.PosToCell(building) 这一格的**底边中点**
        ///     （BuildingComplete 用 Grid.CellToPosCBC 摆位，y 只抬高 0.01）；
        ///   · 4×3 建筑的 PlacementOffsets 是 x=-1..2 / y=0..2，即 transform 所在格是底排第 2 格，
        ///     占位左边界 = transform 格 x - 1，占位底边 = transform 格 y；
        ///   · kanim 贴图 1024×768 正好铺满 4×3 格（200 kanim 单位/格），
        ///     偶宽建筑的贴图会整体右移半格（BuildingComplete 把 KBAC.Offset 设成 Def.GetVisualizerOffset()）。
        ///
        /// 所以：
        ///   贴图左边 x = 格 x + 0.5 + (偶宽 ? 0.5 : 0) - 宽/2
        ///   贴图顶边 y = 格 y + 高
        /// 表盘枢轴再按美术像素往里推。
        /// </summary>
        private Vector3 GetClockWorldPosition()
        {
            float width = 4f;
            float height = 3f;

            Building building = GetComponent<Building>();
            if (building != null && building.Def != null)
            {
                width = building.Def.WidthInCells;
                height = building.Def.HeightInCells;
            }

            int cell = Grid.PosToCell(this);
            int cellX = 0;
            int cellY = 0;
            Grid.CellToXY(cell, out cellX, out cellY);

            float halfCellIfEvenWidth = (Mathf.RoundToInt(width) % 2 == 0) ? 0.5f : 0f;
            float artLeft = cellX + 0.5f + halfCellIfEvenWidth - width * 0.5f;
            float artTop = cellY + height;

            return new Vector3(
                artLeft + PIVOT_X / PX_PER_CELL,
                artTop - PIVOT_Y / PX_PER_CELL,
                transform.GetPosition().z + Z_OFFSET);
        }

        private Sprite LoadSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[RingCloudTech] 时间之弓表盘：找不到 " + path);
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Debug.LogWarning("[RingCloudTech] 时间之弓表盘：贴图解码失败 " + path);
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            // 部署的是以枢轴为中心裁好的 256×256 小图（只留下指针附近），
            // 所以 pivot 正好是图心，PPU=256 时 1 px = 1/256 格，与美术比例一致。
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), PX_PER_CELL);
        }

        private Transform MakeHand(string name, Sprite sprite, Vector3 position)
        {
            GameObject hand = new GameObject(name);
            hand.transform.SetParent(transform, false);
            hand.transform.position = position;
            hand.transform.localScale = Vector3.one;

            SpriteRenderer renderer = hand.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 100;
            return hand.transform;
        }

        /// <summary>只在指针需要动的时候写 transform，避免每帧三次无意义赋值。</summary>
        private void Update()
        {
            if (!built)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            // 工作时时间流速加快：齿轮转起来了，钟自然走得更快。
            float target = IsWorking() ? WORK_TIME_SCALE : IDLE_TIME_SCALE;
            timeScale = Mathf.Lerp(timeScale, target, 1f - Mathf.Exp(-SPEED_SMOOTHING * dt));

            float scaled = dt * timeScale;
            secondAngle = Mathf.Repeat(secondAngle + scaled / 60f * 360f, 360f);
            minuteAngle = Mathf.Repeat(minuteAngle + scaled / 3600f * 360f, 360f);
            hourAngle = Mathf.Repeat(hourAngle + scaled / 43200f * 360f, 360f);
            ApplyAngles();
        }

        /// <summary>「正在工作」= 发电机确实在往外送电（有氦气、已通电、未掩埋）。</summary>
        private bool IsWorking()
        {
            if (generator == null)
            {
                generator = GetComponent<Generator>();
            }

            try
            {
                return generator != null && generator.IsProducingPower();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ApplyAngles()
        {
            // Unity 里正角度是逆时针，钟表是顺时针 → 取负号
            if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0f, 0f, -hourAngle);
            if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0f, 0f, -minuteAngle);
            if (secondHand != null) secondHand.localRotation = Quaternion.Euler(0f, 0f, -secondAngle);
        }
    }
}
