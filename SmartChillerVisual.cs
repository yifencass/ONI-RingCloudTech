using System;
using System.IO;
using System.Globalization;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 智能液冷机的三个「动画目标」+ 屏幕动态温度文本。
    ///
    /// 美术是 1024×768（4×3 格，1 格 256 px），kanim 只有静态机身，
    /// 会动的部分全部用运行时 SpriteRenderer 叠在机身上：
    ///   · 涡轮 —— 有液体通过时旋转
    ///   · 机器运行指针 —— 通电时顺时针旋转
    ///   · 冷却进行指针 —— 冷却时 0° → -45° → 0° → +45° → 0° 快速往复
    ///   · 屏幕温度文本 —— 用字形条按目标温度实时拼出来
    ///
    /// 分层 PNG 放在 mod 目录的 smartchiller_layers\ 下（与 timebow_clock 同一套做法）。
    /// </summary>
    public class SmartChillerVisual : KMonoBehaviour
    {
        // ───── 贴图↔世界 换算 ─────
        /// <summary>贴图宽（px）。</summary>
        private const float ART_W = 1024f;
        /// <summary>贴图高（px）。</summary>
        private const float ART_H = 768f;
        /// <summary>每格像素数：1024 px / 4 格。</summary>
        private const float PX_PER_CELL = 256f;
        /// <summary>叠在机身前方一点点（和 TimeBowClock 一致）。</summary>
        private const float Z_OFFSET = -0.05f;

        // ───── 各动态元素在贴图里的枢轴（用户精修版坐标） ─────
        private const float TURBINE_X = 378f;
        private const float TURBINE_Y = 404f;
        private const float NEEDLE_RUN_X = 700f;
        private const float NEEDLE_RUN_Y = 450f;
        private const float NEEDLE_COOL_X = 872f;
        private const float NEEDLE_COOL_Y = 450f;

        // ───── 屏幕温度文本排版 ─────
        /// <summary>文本区右边界（贴图 px）。</summary>
        private const float TEXT_RIGHT = 908f;
        /// <summary>字形底边（贴图 px，自上而下）：基线 220 + 下降部 18。</summary>
        private const float TEXT_BOTTOM = 238f;
        /// <summary>字形单元格宽（px），字形条按 34 px 一格切。</summary>
        private const float GLYPH_W = 34f;
        /// <summary>字形单元格高（px）。</summary>
        private const float GLYPH_H = 80f;
        /// <summary>数字与单位之间的间隙（px）。</summary>
        private const float UNIT_GAP = 6f;
        /// <summary>文本可用宽度上限（px），超了就整体缩小。</summary>
        private const float TEXT_MAX_WIDTH = 252f;
        /// <summary>字形条里字符的顺序，必须与 glyphs.png 的格子一一对应。</summary>
        private const string GLYPHS = "0123456789-.°CFK";
        /// <summary>最多几个字符（"-100.0°C" 8 个，留一点余量）。</summary>
        private const int MAX_GLYPH_SPRITES = 10;

        // ───── 动画速度 ─────
        private const float TURBINE_DEG_PER_SEC = 300f;
        private const float RUN_DEG_PER_SEC = 60f;
        private const float COOL_CYCLES_PER_SEC = 1.6f;
        private const float COOL_AMPLITUDE = 45f;
        private const float SPEED_SMOOTHING = 5f;

        private const string ART_DIR = "smartchiller_layers";

        private SmartChiller chiller;
        private Transform turbine;
        private Transform needleRun;
        private Transform needleCool;

        private Sprite[] glyphSprites;
        private SpriteRenderer[] glyphRenderers;
        private string drawnText = string.Empty;

        private float turbineAngle;
        private float turbineSpeed;
        private float runAngle;
        private float runSpeed;
        private float coolPhase;
        private float coolAmplitude;

        private bool built;
        private bool flipped;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (GetComponent<BuildingComplete>() == null)
            {
                return;
            }

            try
            {
                Build();
                built = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 智能液冷机动画层构建失败: " + e);
            }
        }

        private void Build()
        {
            chiller = GetComponent<SmartChiller>();
            flipped = IsFlipped();

            string modDir = Path.GetDirectoryName(typeof(SmartChillerVisual).Assembly.Location);
            string artDir = Path.Combine(modDir ?? string.Empty, ART_DIR);

            Sprite turbineSprite = LoadSprite(Path.Combine(artDir, "turbine.png"));
            Sprite runSprite = LoadSprite(Path.Combine(artDir, "needle_run.png"));
            Sprite coolSprite = LoadSprite(Path.Combine(artDir, "needle_cool.png"));

            turbine = MakeLayer("sc_turbine", turbineSprite, WorldPos(TURBINE_X, TURBINE_Y));
            needleRun = MakeLayer("sc_needle_run", runSprite, WorldPos(NEEDLE_RUN_X, NEEDLE_RUN_Y));
            needleCool = MakeLayer("sc_needle_cool", coolSprite, WorldPos(NEEDLE_COOL_X, NEEDLE_COOL_Y));

            // 指针贴图默认朝「左下」；翻转时镜像贴图，方向才对得上
            foreach (Transform t in new[] { needleRun, needleCool })
            {
                SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.flipX = flipped;
                }
            }

            BuildGlyphs(Path.Combine(artDir, "glyphs.png"));

            Debug.Log("[RingCloudTech] 智能液冷机动画层：涡轮 + 两根指针 + 温度文本已装好");
        }

        private void BuildGlyphs(string glyphsPath)
        {
            Texture2D texture = LoadTexture(glyphsPath);
            if (texture == null)
            {
                return;
            }

            int cell = Mathf.RoundToInt(texture.width / (float)GLYPHS.Length);
            int cellHeight = texture.height;
            glyphSprites = new Sprite[GLYPHS.Length];
            for (int i = 0; i < GLYPHS.Length; i++)
            {
                Rect rect = new Rect(i * cell, 0f, cell, cellHeight);
                // 左下角做枢轴，方便按行排版
                glyphSprites[i] = Sprite.Create(
                    texture, rect, new Vector2(0f, 0f), PX_PER_CELL);
                glyphSprites[i].name = "sc_glyph_" + GLYPHS[i];
            }

            glyphRenderers = new SpriteRenderer[MAX_GLYPH_SPRITES];
            for (int i = 0; i < glyphRenderers.Length; i++)
            {
                GameObject go = new GameObject("sc_glyph_" + i);
                go.transform.SetParent(transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 101;
                sr.enabled = false;
                glyphRenderers[i] = sr;
            }

            // 字形高度 = texture.height px（80 px），排版以格子高度为准
            _glyphCellWidth = cell;
            _glyphCellHeight = cellHeight;
        }

        private float _glyphCellWidth = 34f;
        private float _glyphCellHeight = 80f;

        private bool IsFlipped()
        {
            Rotatable rotatable = GetComponent<Rotatable>();
            return rotatable != null && rotatable.GetVisualizerFlipX();
        }

        /// <summary>贴图坐标（左上角原点，y 向下）→ 世界坐标。widthPx 用于左边缘对齐的字形格。</summary>
        private Vector3 WorldPos(float artX, float artY, float widthPx = 0f)
        {
            int cell = Grid.PosToCell(this);
            Grid.CellToXY(cell, out int cellX, out int cellY);

            int width = 4;
            int height = 3;
            Building building = GetComponent<Building>();
            if (building != null && building.Def != null)
            {
                width = building.Def.WidthInCells;
                height = building.Def.HeightInCells;
            }

            if (flipped)
            {
                artX = ART_W - artX - widthPx;
            }

            float halfCellIfEvenWidth = (Mathf.RoundToInt(width) % 2 == 0) ? 0.5f : 0f;
            float artLeft = cellX + 0.5f + halfCellIfEvenWidth - width * 0.5f;
            float artTop = cellY + height;

            return new Vector3(
                artLeft + artX / PX_PER_CELL,
                artTop - artY / PX_PER_CELL,
                transform.GetPosition().z + Z_OFFSET);
        }

        private Transform MakeLayer(string name, Sprite sprite, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 100;
            return go.transform;
        }

        private static Texture2D LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Debug.LogWarning("[RingCloudTech] 贴图解码失败: " + path);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static Sprite LoadSprite(string path)
        {
            Texture2D texture = LoadTexture(path);
            if (texture == null)
            {
                return null;
            }
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), PX_PER_CELL);
        }

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

            bool nowFlipped = IsFlipped();
            if (nowFlipped != flipped)
            {
                flipped = nowFlipped;
                RepositionAll();
            }

            bool hasFlow = chiller != null && chiller.HasLiquidFlow;
            bool powered = chiller != null && chiller.IsPowered;
            bool cooling = chiller != null && chiller.IsCooling;

            // ── 涡轮：有液体通过就转，流量没了就缓停 ──
            turbineSpeed = Mathf.Lerp(turbineSpeed,
                hasFlow ? TURBINE_DEG_PER_SEC : 0f,
                1f - Mathf.Exp(-SPEED_SMOOTHING * dt));
            turbineAngle = Mathf.Repeat(turbineAngle + turbineSpeed * dt, 360f);
            ApplyRotation(turbine, turbineAngle);

            // ── 运行指针：通电时顺时针转 ──
            runSpeed = Mathf.Lerp(runSpeed,
                powered ? RUN_DEG_PER_SEC : 0f,
                1f - Mathf.Exp(-SPEED_SMOOTHING * dt));
            runAngle = Mathf.Repeat(runAngle + runSpeed * dt, 360f);
            ApplyRotation(needleRun, runAngle);

            // ── 冷却指针：0 → -45 → 0 → +45 → 0 往复 ──
            coolAmplitude = Mathf.Lerp(coolAmplitude,
                cooling ? COOL_AMPLITUDE : 0f,
                1f - Mathf.Exp(-SPEED_SMOOTHING * 1.5f * dt));
            if (cooling)
            {
                coolPhase = Mathf.Repeat(coolPhase + dt * COOL_CYCLES_PER_SEC, 1f);
            }
            ApplyRotation(needleCool, CycleAngle(coolPhase) * coolAmplitude);

            // ── 屏幕温度文本 ──
            RefreshText();
        }

        /// <summary>0..1 相位 → 0 → -1 → 0 → +1 → 0 的三角波。</summary>
        private static float CycleAngle(float phase)
        {
            float p = Mathf.Repeat(phase, 1f);
            if (p < 0.25f)
            {
                return Mathf.Lerp(0f, -1f, p / 0.25f);
            }
            if (p < 0.5f)
            {
                return Mathf.Lerp(-1f, 0f, (p - 0.25f) / 0.25f);
            }
            if (p < 0.75f)
            {
                return Mathf.Lerp(0f, 1f, (p - 0.5f) / 0.25f);
            }
            return Mathf.Lerp(1f, 0f, (p - 0.75f) / 0.25f);
        }

        /// <summary>正数 = 画面上顺时针（Unity 的 z 正角是逆时针，故取负）。</summary>
        private static void ApplyRotation(Transform target, float clockwiseDegrees)
        {
            if (target == null)
            {
                return;
            }
            target.localRotation = Quaternion.Euler(0f, 0f, -clockwiseDegrees);
        }

        private void RepositionAll()
        {
            if (turbine != null)
            {
                turbine.position = WorldPos(TURBINE_X, TURBINE_Y);
            }
            if (needleRun != null)
            {
                needleRun.position = WorldPos(NEEDLE_RUN_X, NEEDLE_RUN_Y);
            }
            if (needleCool != null)
            {
                needleCool.position = WorldPos(NEEDLE_COOL_X, NEEDLE_COOL_Y);
            }
            if (glyphRenderers != null)
            {
                foreach (SpriteRenderer sr in glyphRenderers)
                {
                    if (sr != null)
                    {
                        sr.enabled = false;
                    }
                }
            }
            drawnText = string.Empty;
        }

        private void RefreshText()
        {
            if (glyphRenderers == null || glyphSprites == null)
            {
                return;
            }

            float kelvin = chiller != null
                ? chiller.TargetTemperature
                : SmartChiller.DEFAULT_TARGET_TEMPERATURE;

            string number = GameUtil.GetConvertedTemperature(kelvin)
                .ToString("0.0", CultureInfo.InvariantCulture);
            string unit;
            switch (GameUtil.temperatureUnit)
            {
                case GameUtil.TemperatureUnit.Fahrenheit:
                    unit = "°F";
                    break;
                case GameUtil.TemperatureUnit.Kelvin:
                    unit = "K";
                    break;
                default:
                    unit = "°C";
                    break;
            }

            string text = number + unit;
            if (text == drawnText)
            {
                return;
            }
            drawnText = text;

            int glyphCount = Mathf.Min(text.Length, glyphRenderers.Length);
            float cellWidth = _glyphCellWidth;
            float totalWidth = glyphCount * cellWidth + UNIT_GAP;
            float scale = totalWidth > TEXT_MAX_WIDTH ? TEXT_MAX_WIDTH / totalWidth : 1f;
            float advance = cellWidth * scale;
            float gap = UNIT_GAP * scale;
            float renderedWidth = glyphCount * advance + gap;
            float startX = TEXT_RIGHT - renderedWidth;

            for (int i = 0; i < glyphRenderers.Length; i++)
            {
                SpriteRenderer sr = glyphRenderers[i];
                if (sr == null)
                {
                    continue;
                }

                if (i >= glyphCount)
                {
                    sr.enabled = false;
                    continue;
                }

                int index = GLYPHS.IndexOf(text[i]);
                if (index < 0)
                {
                    sr.enabled = false;
                    continue;
                }

                float xArt = startX + i * advance;
                if (i >= number.Length)
                {
                    // 单位从数字之后再加一个间隙开始
                    xArt = startX + number.Length * advance + gap + (i - number.Length) * advance;
                }

                sr.sprite = glyphSprites[index];
                sr.enabled = true;
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                sr.transform.position = WorldPos(xArt, TEXT_BOTTOM, advance);
            }
        }
    }
}
