using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼的分层渲染。
    ///
    /// ONI 的所有 KBAC 共用一个批次，渲染队列是批次级的 —— 没法把「十字」
    /// 单独抬到某个 quad 之上。所以第 3-6 层全部改用 MeshRenderer + 手动队列：
    ///
    ///     层 1 环     建筑自带贴图（KBAC，约 3000）
    ///     层 2 水波   3600   程序化纹理，每帧重算
    ///     层 3 黑圆   3605
    ///     层 4 彩球   3606   7 个独立 quad，运行时着色
    ///     层 5 十字   3610
    ///     层 6 矩阵   3620   4 帧循环
    ///
    /// 贴图直接从已部署的 harmonyeye.png 图集里裁区域生成，不额外放文件。
    /// </summary>
    public class HarmonyEyeVisual : KMonoBehaviour
    {
        // 图集区域（与 make_eye_kanim.py 保持一致）
        private const int ATLAS = 2048;   // 仅供参考，图集本身已拆成 raw
        private const int CROSS_X = 1024, CROSS_Y = 0, CROSS_S = 896;
        private const int MATRIX_X = 0, MATRIX_Y = 1024, MATRIX_S = 256, MATRIX_N = 4;
        /// <summary>层 2-6 的整体缩放（实测偏小，先放大 2 倍看效果）。</summary>
        private const float LAYER_SCALE = 2f;
        private const float MATRIX_FPS = 1.2f;
        private const int SPHERE_X = 0, SPHERE_Y = 1280, SPHERE_S = 640;
        private const int BALL_X = 704, BALL_Y = 1280, BALL_S = 64;

        private const int TEX_SIZE = 160;
        private const float RING_RADIUS_PX = 54f;

        // 世界尺寸（anim 单位 / 200）
        private const float U = 1f / 200f;
        private const float SPHERE_W = SPHERE_S * U * LAYER_SCALE;
        private const float CROSS_W  = CROSS_S * U * LAYER_SCALE;
        private const float MATRIX_W = MATRIX_S * U * LAYER_SCALE;
        private const float BALL_W   = BALL_S * U * LAYER_SCALE;

        private const int BALL_COUNT = 7;

        private static readonly Color32[] PALETTE =
        {
            new Color32(255, 96, 96, 255),
            new Color32(255, 176, 64, 255),
            new Color32(255, 236, 96, 255),
            new Color32(128, 236, 128, 255),
            new Color32(96, 220, 220, 255),
            new Color32(120, 168, 255, 255),
            new Color32(196, 140, 255, 255),
        };

        private class Ball
        {
            public Transform tr;
            public float orbitR, orbitW, phase, wobR, wobW, wobPhase;
        }

        private Operational operational;
        private EnergyConsumer consumer;
        private Vector3 center;
        private float halfHeight = 4.5f;
        private float maxBallR = 2.18f;
        private bool active;
        private bool lastPowered = true;
        private bool lastWorking = true;

        private Shader shader;
        private Color32[] atlasPixels;

        private readonly List<Ball> balls = new List<Ball>();
        private GameObject sphereGo, crossGo, matrixGo, distGo;
        private MeshRenderer sphereMr, crossMr, matrixMr, distMr;
        private Material matrixMat;
        private Texture2D[] matrixTex;
        private Texture2D distTex;
        private Color32[] distPixels;
        private float clock;
        private int matrixFrame = -1;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            operational = GetComponent<Operational>();
            consumer = GetComponent<EnergyConsumer>();

            Building building = GetComponent<Building>();
            if (building != null && building.Def != null)
            {
                halfHeight = building.Def.HeightInCells * 0.5f;
            }
            Vector3 p = transform.GetPosition();
            center = new Vector3(p.x, p.y + halfHeight, p.z);

            shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Klei/Unlit Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                Debug.LogWarning("[HarmonyEye] 找不到透明着色器，分层渲染跳过");
                return;
            }

            if (!ResolveAssetDir())
            {
                Debug.LogWarning("[HarmonyEye] 图集加载失败，分层渲染跳过");
                return;
            }

            try
            {
                MeshRenderer mr;
                sphereGo = MakeQuad("sphere", SPHERE_W, LoadRaw("sphere", out _), 3605, out mr);
                sphereMr = mr;

                crossGo = MakeQuad("cross", CROSS_W, LoadRaw("cross", out _), 3610, out mr);
                crossMr = mr;
                crossMr.enabled = true;      // 十字常驻

                matrixTex = new Texture2D[MATRIX_N];
                for (int i = 0; i < MATRIX_N; i++)
                {
                    matrixTex[i] = LoadRaw("matrix" + i, out _);
                }
                matrixGo = MakeQuad("matrix", MATRIX_W, matrixTex[0], 3620, out mr);
                matrixMr = mr;
                matrixMat = mr.material;
                matrixMr.enabled = true;     // 矩阵常驻

                SpawnBalls();
                SpawnDistortion();

                SetPowered(false);
                SetWorking(false);

                // 关键：把「上一次状态」也归到 false。
                // 两个字段的初值是 true（原意是「第一帧强制翻转一次」），
                // 但这里已经把层关掉了；如果不同步归零，第一帧 Update 会判定
                // powered(true) == lastPowered(true) 而不调用 SetPowered，
                // 黑球与空间扭曲就会永久停在关闭状态。
                lastPowered = false;
                lastWorking = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HarmonyEye] 分层渲染创建失败: " + e);
            }
        }

        // ────────────────────── 图集 ──────────────────────

        private string assetDir;

        private bool ResolveAssetDir()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            assetDir = Path.Combine(docs, "Klei", "OxygenNotIncluded", "mods", "Local", "RingCloudTech",
                                    "anim", "assets", "harmonyeye");
            if (Directory.Exists(assetDir)) return true;
            Debug.LogWarning("[HarmonyEye] 找不到贴图目录: " + assetDir);
            return false;
        }

        /// <summary>读取裸 RGBA 文件（前 8 字节是宽高）。</summary>
        private Texture2D LoadRaw(string name, out bool ok)
        {
            ok = false;
            try
            {
                string path = Path.Combine(assetDir, name + ".raw");
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[HarmonyEye] 缺贴图: " + path);
                    return null;
                }
                byte[] bytes = File.ReadAllBytes(path);
                int w = BitConverter.ToInt32(bytes, 0);
                int h = BitConverter.ToInt32(bytes, 4);
                if (w <= 0 || h <= 0 || bytes.Length < 8 + w * h * 4)
                {
                    Debug.LogWarning("[HarmonyEye] 贴图尺寸异常: " + name);
                    return null;
                }
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.filterMode = FilterMode.Bilinear;
                t.wrapMode = TextureWrapMode.Clamp;
                Color32[] px = new Color32[w * h];
                for (int i = 0; i < px.Length; i++)
                {
                    int o = 8 + i * 4;
                    px[i] = new Color32(bytes[o], bytes[o + 1], bytes[o + 2], bytes[o + 3]);
                }
                t.SetPixels32(px);
                t.Apply(false);
                ok = true;
                return t;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HarmonyEye] 读取 " + name + " 失败: " + e.Message);
                return null;
            }
        }
        // ────────────────────── quad ──────────────────────

        private GameObject MakeQuad(string name, float size, Texture2D tex, int queue, out MeshRenderer mr)
        {
            GameObject go = new GameObject("HarmonyEye_" + name);
            go.transform.SetParent(transform, false);
            go.transform.SetPosition(center);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.mesh = BuildQuad(size);

            mr = go.AddComponent<MeshRenderer>();
            Material mat = new Material(shader)
            {
                name = "HarmonyEye_" + name,
                mainTexture = tex,
                renderQueue = queue,
            };
            mr.material = mat;
            mr.enabled = false;
            return go;
        }

        private static Mesh BuildQuad(float size)
        {
            float h = size * 0.5f;
            Mesh mesh = new Mesh { name = "HarmonyEyeQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-h, -h, 0f), new Vector3(-h, h, 0f),
                new Vector3(h, h, 0f), new Vector3(h, -h, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ────────────────────── 彩球 ──────────────────────

        private void SpawnBalls()
        {
            // 球体可见半径直接从贴图推：sphere.raw 里黑盘直径占画布的 96.6%，
            // 所以半径 = quad 宽度 × 0.483。彩球活动范围按它的比例收。
            float discR = SPHERE_W * 0.483f;
            float radius = discR * 0.80f;
            maxBallR = discR * 0.90f - BALL_W * 0.5f;
            Texture2D ballTex = LoadRaw("ball", out _);

            for (int i = 0; i < BALL_COUNT; i++)
            {
                MeshRenderer mr;
                GameObject go = MakeQuad("ball" + i, BALL_W, ballTex, 3606, out mr);
                mr.material.color = PALETTE[i % PALETTE.Length];

                balls.Add(new Ball
                {
                    tr = go.transform,
                    orbitR = UnityEngine.Random.Range(radius * 0.45f, radius * 0.72f),
                    orbitW = UnityEngine.Random.Range(0.28f, 0.62f)
                             * (UnityEngine.Random.value < 0.5f ? -1f : 1f),
                    phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    wobR = UnityEngine.Random.Range(0.08f, 0.20f) * radius,
                    wobW = UnityEngine.Random.Range(0.7f, 1.9f),
                    wobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                });
            }
        }

        // ────────────────────── 水波透镜 ──────────────────────

        private void SpawnDistortion()
        {
            distTex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            distTex.filterMode = FilterMode.Bilinear;
            distTex.wrapMode = TextureWrapMode.Clamp;
            distPixels = new Color32[TEX_SIZE * TEX_SIZE];

            MeshRenderer mr;
            distGo = MakeQuad("distortion", SPHERE_W * 1.15f, distTex, 3600, out mr);
            distMr = mr;
        }

        private static float SmoothStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float WaveSlope(float u, float a, float t)
        {
            float s = 0f;
            s += Mathf.Cos(u * 0.62f - t * 1.05f + Mathf.Sin(a * 3f + t * 0.35f) * 2.2f) * 0.62f * 1.00f;
            s += Mathf.Cos(u * 1.45f - t * 1.75f + Mathf.Sin(a * 5f - t * 0.55f) * 1.3f) * 1.45f * 0.55f;
            s += Mathf.Cos(u * 2.70f - t * 2.60f + Mathf.Sin(a * 2f + t * 0.20f) * 0.80f) * 2.70f * 0.28f;
            return s;
        }

        private void RebuildDistortionTexture(float t)
        {
            int n = TEX_SIZE;
            float c = (n - 1) * 0.5f;
            float half = c;
            float r2 = RING_RADIUS_PX;

            for (int y = 0; y < n; y++)
            {
                float dy = y - c;
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= r2) continue;

                    float a = Mathf.Atan2(dy, dx);
                    float rn = 0.42f + (d - r2) / Mathf.Max(1f, half - r2) * 0.58f;
                    float bump = SmoothStep(0.42f, 0.78f, rn) * (1f - SmoothStep(0.88f, 1f, rn));
                    if (bump <= 0.004f) continue;

                    float u = d - r2;
                    float slope = WaveSlope(u, a, t);
                    float refr = slope / (1f + Mathf.Abs(slope));
                    float rim = Mathf.Exp(-u * u * 0.020f) * 0.55f;

                    float v = bump * (0.46f + 0.42f * refr) + rim;
                    v *= Mathf.Exp(-u * 0.045f);
                    if (v <= 0.010f) continue;
                    v = Mathf.Clamp01(v);

                    float disp = Mathf.Clamp01(1f - u * 0.05f);
                    distPixels[y * n + x] = new Color32(
                        (byte)(Mathf.Clamp01(v * (0.34f + 0.30f * disp)) * 255f),
                        (byte)(Mathf.Clamp01(v * 0.76f) * 255f),
                        (byte)(Mathf.Clamp01(v) * 255f),
                        (byte)(Mathf.Clamp01(v * 1.10f) * 255f));
                }
            }

            distTex.SetPixels32(distPixels);
            distTex.Apply(false);
        }

        // ────────────────────── 帧驱动 ──────────────────────

        private void SetPowered(bool on)
        {
            if (sphereMr != null) sphereMr.enabled = on;
            if (distMr != null) distMr.enabled = on;
        }

        private void SetWorking(bool on)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i].tr == null) continue;
                MeshRenderer mr = balls[i].tr.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = on;
            }
        }

        private void Update()
        {
            // 层 2/3（黑球 + 空间扭曲）看是否接电；层 4（彩球）看是否在执行配方
            bool powered = consumer != null && consumer.IsConnected;
            bool working = operational != null && operational.IsActive;

            if (powered != lastPowered)
            {
                lastPowered = powered;
                SetPowered(powered);
            }
            if (working != lastWorking)
            {
                lastWorking = working;
                SetWorking(working);
            }
            clock += Time.deltaTime;

            // 矩阵层常驻：不接电、不工作也照常显示并换帧
            if (matrixMat != null && matrixTex != null)
            {
                int f = Mathf.FloorToInt(clock * MATRIX_FPS) % MATRIX_N;
                if (f != matrixFrame)
                {
                    matrixFrame = f;
                    matrixMat.mainTexture = matrixTex[f];
                }
            }

            if (!powered && !working) return;

            for (int i = 0; working && i < balls.Count; i++)
            {
                Ball b = balls[i];
                if (b.tr == null) continue;
                float ang = clock * b.orbitW + b.phase;
                float x = Mathf.Cos(ang) * b.orbitR + Mathf.Cos(clock * b.wobW + b.wobPhase) * b.wobR;
                float y = Mathf.Sin(ang) * b.orbitR + Mathf.Sin(clock * b.wobW * 1.37f + b.wobPhase) * b.wobR;

                float len = Mathf.Sqrt(x * x + y * y);
                if (len > maxBallR)
                {
                    float k = maxBallR / len;
                    x *= k;
                    y *= k;
                }
                b.tr.SetPosition(center + new Vector3(x, y, 0f));
            }

            if (powered && distMr != null && distTex != null)
            {
                try
                {
                    RebuildDistortionTexture(clock);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[HarmonyEye] 水波更新失败，停用: " + e);
                    distMr.enabled = false;
                }
            }
        }

        protected override void OnCleanUp()
        {
            if (sphereGo != null) Destroy(sphereGo);
            if (crossGo != null) Destroy(crossGo);
            if (matrixGo != null) Destroy(matrixGo);
            if (distGo != null) Destroy(distGo);
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i].tr != null) Destroy(balls[i].tr.gameObject);
            }
            balls.Clear();
            if (distTex != null) Destroy(distTex);
            base.OnCleanUp();
        }
    }
}
