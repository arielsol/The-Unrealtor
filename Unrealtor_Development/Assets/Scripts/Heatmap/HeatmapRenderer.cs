using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在 3D 场景中绘制热力图：将世界空间中的位置采样到网格，用颜色强度表示密度。
/// 适用于：玩家停留、点击位置、事件密度等。
/// </summary>
public class HeatmapRenderer : MonoBehaviour
{
    [Header("热力图范围 (XZ 平面)")]
    [Tooltip("可选：拖入第一关的 Cube（带 BoxCollider 或 MeshRenderer），将自动用其 XZ 范围作为热力图范围；不填则用下面手动值")]
    public Transform boundsSource;
    [Tooltip("热力图中心（世界坐标）；若已填 Bounds Source 则运行时会被覆盖")]
    public Vector3 boundsCenter = Vector3.zero;
    [Tooltip("热力图在 X 和 Z 方向的半边长；若已填 Bounds Source 则运行时会被覆盖")]
    public Vector2 boundsSize = new Vector2(10f, 10f);

    [Header("网格与显示")]
    [Tooltip("网格分辨率，越大越细腻")]
    public int gridResolution = 64;
    [Tooltip("每个采样点影响的半径（网格单位）；范围小、想让热区更集中时建议调小，如 1")]
    public float blurRadius = 2f;
    [Tooltip("热力图整体透明度；调高（如 0.9）活动更明显")]
    [Range(0.1f, 1f)]
    public float opacity = 0.7f;
    [Tooltip("热力对比：1=不额外提亮；<1 中等活动更显色，>1 只突出最热")]
    [Range(0.3f, 2f)]
    public float heatContrast = 1f;

    [Header("数据来源")]
    [Tooltip("运行时持续记录该物体的位置（玩家 1）")]
    public Transform recordTransform;
    [Tooltip("可选：玩家 2，与玩家 1 同时记录")]
    public Transform recordTransform2;
    [Tooltip("采样间隔（秒）")]
    public float recordInterval = 0.2f;
    [Tooltip("仅当玩家移动超过该距离才记录（轨迹模式：不累计站立，只记经过）")]
    public bool recordOnlyWhenMoved = true;
    [Tooltip("最小移动距离（米），超过才记一点")]
    public float minMoveDistance = 0.3f;
    [Tooltip("仅记录位于 Bounds 范围内的采样点（推荐用于只统计第一关）")]
    public bool recordOnlyInsideBounds = true;
    [Tooltip("或直接提供位置列表（可在 Inspector 中手动添加或由代码写入）")]
    public List<Vector3> samplePositions = new List<Vector3>();

    [Header("显示")]
    [Tooltip("用于显示热力图的平面；不指定则脚本会创建一个 Quad")]
    public MeshRenderer heatmapPlane;
    [Tooltip("冷色 -> 热色：从左到右对应低 -> 高密度")]
    public Gradient heatGradient;

    [Header("背景平面图（俯视图）")]
    [Tooltip("可选：studio2 等场景的俯视平面图，将作为热力图底图。图片的左右对应场景 X 轴，上下对应 Z 轴，需与 Bounds 范围一致。导入图片后请在 Inspector 中勾选 Read/Write Enabled")]
    public Texture2D floorPlanTexture;
    [Tooltip("平面图亮度（0.2~1），降低可使热力更突出")]
    [Range(0.2f, 1f)]
    public float floorPlanBrightness = 0.85f;
    [Header("数据导出")]
    [Tooltip("进入 Play 后对象被销毁/禁用时自动导出本局数据")]
    public bool autoSaveOnDisable = true;
    [Tooltip("应用退出时自动导出本局数据")]
    public bool autoSaveOnQuit = true;
    [Tooltip("最少采样点数量，小于该值则跳过自动导出")]
    public int minSamplesToSave = 10;
    [Tooltip("导出子目录名，保存在「用户/下载/HeatmapSessions」下（可改，如留空则用 HeatmapSessions）")]
    public string outputFolderName = "HeatmapSessions";

    [Header("Google Drive")]
    [Tooltip("保存后是否同时上传到 Google 云端硬盘（需先菜单 Tools → Heatmap → Connect Google Drive 完成连接）")]
    public bool uploadToGoogleDrive = false;
    [Tooltip("Google Drive 配置（含 OAuth Client ID/Secret）；不填则不上传")]
    public GoogleDriveConfig googleDriveConfig;

    public bool exportJson = true;
    public bool exportCsv = true;
    public bool exportPng = true;
    [Tooltip("导出的 PNG 是否强制不透明（便于在图片查看器中观察）")]
    public bool exportOpaquePng = true;
    [Tooltip("导出 PNG 无热区域底色；轨迹热力图请设为白色")]
    public Color pngBackgroundColor = Color.white;
    [Tooltip("导出 PNG 时是否垂直翻转，使图片上方=场景 Z+（与俯视图一致）")]
    public bool exportPngFlipVertical = true;
    [Tooltip("导出 PNG 的放大倍数（如 4 表示 64→256 像素），便于查看")]
    [Min(1)]
    public int exportPngScale = 4;

    [Header("定位参考点（Analytics）")]
    [Tooltip("在地图上标注命名点，导出 PNG 上会画标记，JSON 中会带像素坐标便于分析")]
    public List<HeatmapReferencePoint> referencePoints = new List<HeatmapReferencePoint>();
    [Tooltip("是否在导出的 PNG 上绘制参考点标记")]
    public bool drawReferencePointsOnPng = true;
    [Tooltip("参考点标记颜色")]
    public Color referencePointColor = new Color(0f, 0.8f, 1f, 1f);
    [Tooltip("参考点标记半径（像素）")]
    public int referencePointRadius = 4;

    private float[,] _grid;
    private Texture2D _heatmapTexture;
    private Material _material;
    private float _lastRecordTime;
    private readonly List<HeatmapSampleRecord> _samples = new List<HeatmapSampleRecord>();
    private bool _hasSavedSession;
    private string _sessionId;
    private DateTime _sessionStartUtc;
    private int _lastInBoundsSampleCount;
    private float _lastMaxHeatValue;
    private bool _hasCachedBounds;
    private Vector3 _cachedBoundsCenter;
    private Vector2 _cachedBoundsSize;
    private Vector3? _lastRecordedPos1;
    private Vector3? _lastRecordedPos2;
    private float _lastMinX, _lastMinZ, _lastHalfX, _lastHalfZ;
    private bool _warnedFloorPlanNotReadable;

    [Serializable]
    public class HeatmapReferencePoint
    {
        public string label = "Point";
        public Vector3 worldPosition;
    }

    [Serializable]
    private class HeatmapSampleRecord
    {
        public float x;
        public float y;
        public float z;
        public float t;
    }

    [Serializable]
    private class HeatmapSessionExport
    {
        public string sessionId;
        public string sceneName;
        public string startedUtc;
        public string endedUtc;
        public string endReason;
        public int sampleCount;
        public int inBoundsSampleCount;
        public int gridResolution;
        public float blurRadius;
        public float maxHeatValue;
        public Vector3 boundsCenter;
        public Vector2 boundsSize;
        public int gridWidth;
        public int gridHeight;
        public List<float> grid = new List<float>();
        public List<HeatmapSampleRecord> samples = new List<HeatmapSampleRecord>();
        public List<HeatmapReferencePointExport> referencePointsExport = new List<HeatmapReferencePointExport>();
    }

    [Serializable]
    private class HeatmapReferencePointExport
    {
        public string label;
        public float worldX, worldZ;
        public int pixelX, pixelY;
        public int exportImageWidth, exportImageHeight;
    }

    private void Awake()
    {
        if (heatGradient == null || heatGradient.colorKeys.Length == 0)
            heatGradient = DefaultHeatGradient();

        _grid = new float[gridResolution, gridResolution];
        _sessionStartUtc = DateTime.UtcNow;
        _sessionId = BuildSessionId();
    }

    private void Start()
    {
        RefreshBoundsFromSource();
        BakeHeatmapTexture();
        EnsurePlaneAndMaterial();
    }

    private void Update()
    {
        float interval = Mathf.Max(0.05f, recordInterval);
        bool anyRecord = (recordTransform != null || recordTransform2 != null);
        if (anyRecord && Time.time - _lastRecordTime >= interval)
        {
            _lastRecordTime = Time.time;
            if (recordTransform != null)
            {
                Vector3 pos = recordTransform.position;
                if (!recordOnlyInsideBounds || IsInsideBoundsXZ(pos))
                {
                    if (!recordOnlyWhenMoved || !_lastRecordedPos1.HasValue || (pos - _lastRecordedPos1.Value).sqrMagnitude >= minMoveDistance * minMoveDistance)
                    {
                        AddSample(pos, Time.timeSinceLevelLoad);
                        _lastRecordedPos1 = pos;
                    }
                }
            }
            if (recordTransform2 != null)
            {
                Vector3 pos = recordTransform2.position;
                if (!recordOnlyInsideBounds || IsInsideBoundsXZ(pos))
                {
                    if (!recordOnlyWhenMoved || !_lastRecordedPos2.HasValue || (pos - _lastRecordedPos2.Value).sqrMagnitude >= minMoveDistance * minMoveDistance)
                    {
                        AddSample(pos, Time.timeSinceLevelLoad);
                        _lastRecordedPos2 = pos;
                    }
                }
            }
            BakeHeatmapTexture();
            ApplyTextureToMaterial();
        }
    }

    /// <summary> 添加一个采样点（可从外部调用） </summary>
    public void AddSample(Vector3 worldPosition)
    {
        AddSample(worldPosition, Time.timeSinceLevelLoad);
    }

    public void AddSample(Vector3 worldPosition, float secondsSinceLevelLoad)
    {
        if (recordOnlyInsideBounds && !IsInsideBoundsXZ(worldPosition))
            return;

        samplePositions.Add(worldPosition);
        _samples.Add(new HeatmapSampleRecord
        {
            x = worldPosition.x,
            y = worldPosition.y,
            z = worldPosition.z,
            t = secondsSinceLevelLoad
        });
    }

    /// <summary> 清空采样并刷新 </summary>
    public void ClearSamples()
    {
        samplePositions.Clear();
        _samples.Clear();
        if (_grid != null)
        {
            for (int x = 0; x < gridResolution; x++)
                for (int z = 0; z < gridResolution; z++)
                    _grid[x, z] = 0f;
        }
        BakeHeatmapTexture();
        ApplyTextureToMaterial();
    }

    /// <summary> 根据当前 samplePositions 重新烘焙热力图纹理 </summary>
    public void BakeHeatmapTexture()
    {
        if (_grid == null || gridResolution <= 0) return;

        RefreshBoundsFromSource();

        int res = gridResolution;
        float halfX = boundsSize.x;
        float halfZ = boundsSize.y;
        float minX = boundsCenter.x - halfX;
        float minZ = boundsCenter.z - halfZ;

        if (halfX <= 0f || halfZ <= 0f) halfX = halfZ = 5f;
        if (samplePositions.Count > 0)
        {
            float sxMin = float.MaxValue, sxMax = float.MinValue, szMin = float.MaxValue, szMax = float.MinValue;
            foreach (var pos in samplePositions)
            {
                if (pos.x < sxMin) sxMin = pos.x;
                if (pos.x > sxMax) sxMax = pos.x;
                if (pos.z < szMin) szMin = pos.z;
                if (pos.z > szMax) szMax = pos.z;
            }
            float pad = 2f;
            float useMinX = sxMin - pad;
            float useMaxX = sxMax + pad;
            float useMinZ = szMin - pad;
            float useMaxZ = szMax + pad;
            if (halfX <= 0f || halfZ <= 0f || (minX > useMaxX || minX + 2f * halfX < useMinX || minZ > useMaxZ || minZ + 2f * halfZ < useMinZ))
            {
                halfX = (useMaxX - useMinX) * 0.5f;
                halfZ = (useMaxZ - useMinZ) * 0.5f;
                if (halfX < 1f) halfX = 1f;
                if (halfZ < 1f) halfZ = 1f;
                minX = (useMinX + useMaxX) * 0.5f - halfX;
                minZ = (useMinZ + useMaxZ) * 0.5f - halfZ;
            }
        }

        if (halfX < 0.01f) halfX = 0.01f;
        if (halfZ < 0.01f) halfZ = 0.01f;

        for (int x = 0; x < res; x++)
            for (int z = 0; z < res; z++)
                _grid[x, z] = 0f;

        int radius = Mathf.Max(1, Mathf.RoundToInt(blurRadius));
        int inBoundsCount = 0;

        foreach (var pos in samplePositions)
        {
            float nx = (pos.x - minX) / (2f * halfX);
            float nz = (pos.z - minZ) / (2f * halfZ);
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
            inBoundsCount++;

            int cx = Mathf.Clamp((int)(nx * res), 0, res - 1);
            int cz = Mathf.Clamp((int)(nz * res), 0, res - 1);

            for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int gx = cx + dx;
                    int gz = cz + dz;
                    if (gx < 0 || gx >= res || gz < 0 || gz >= res) continue;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float falloff = Mathf.Clamp01(1f - dist / (radius + 0.5f));
                    _grid[gx, gz] += falloff;
                }
        }

        if (inBoundsCount == 0 && samplePositions.Count > 0)
        {
            float sxMin = float.MaxValue, sxMax = float.MinValue, szMin = float.MaxValue, szMax = float.MinValue;
            foreach (var pos in samplePositions)
            {
                if (pos.x < sxMin) sxMin = pos.x;
                if (pos.x > sxMax) sxMax = pos.x;
                if (pos.z < szMin) szMin = pos.z;
                if (pos.z > szMax) szMax = pos.z;
            }
            float pad = 2f;
            halfX = ((sxMax - sxMin) * 0.5f) + pad;
            halfZ = ((szMax - szMin) * 0.5f) + pad;
            if (halfX < 1f) halfX = 1f;
            if (halfZ < 1f) halfZ = 1f;
            minX = (sxMin + sxMax) * 0.5f - halfX;
            minZ = (szMin + szMax) * 0.5f - halfZ;
            for (int x = 0; x < res; x++)
                for (int z = 0; z < res; z++)
                    _grid[x, z] = 0f;
            inBoundsCount = 0;
            foreach (var pos in samplePositions)
            {
                float nx = (pos.x - minX) / (2f * halfX);
                float nz = (pos.z - minZ) / (2f * halfZ);
                if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
                inBoundsCount++;
                int cx = Mathf.Clamp((int)(nx * res), 0, res - 1);
                int cz = Mathf.Clamp((int)(nz * res), 0, res - 1);
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        int gx = cx + dx, gz = cz + dz;
                        if (gx < 0 || gx >= res || gz < 0 || gz >= res) continue;
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        float falloff = Mathf.Clamp01(1f - dist / (radius + 0.5f));
                        _grid[gx, gz] += falloff;
                    }
            }
        }

        _lastMinX = minX;
        _lastMinZ = minZ;
        _lastHalfX = halfX;
        _lastHalfZ = halfZ;

        float maxVal = 0.0001f;
        for (int x = 0; x < res; x++)
            for (int z = 0; z < res; z++)
                if (_grid[x, z] > maxVal) maxVal = _grid[x, z];
        _lastInBoundsSampleCount = inBoundsCount;
        _lastMaxHeatValue = maxVal;

        if (_heatmapTexture == null || _heatmapTexture.width != res)
            _heatmapTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);

        bool useFloorPlan = floorPlanTexture != null && floorPlanTexture.isReadable;
        if (floorPlanTexture != null && !floorPlanTexture.isReadable && !_warnedFloorPlanNotReadable)
        {
            _warnedFloorPlanNotReadable = true;
            Debug.LogWarning("[HeatmapRenderer] 已指定 Floor Plan Texture，但该贴图未开启 Read/Write。请在 Project 中选中该贴图，在 Inspector 里勾选 Read/Write Enabled。");
        }
        float invRes = res > 1 ? 1f / (res - 1) : 1f;

        for (int x = 0; x < res; x++)
            for (int z = 0; z < res; z++)
            {
                float t = maxVal > 0 ? (_grid[x, z] / maxVal) : 0f;
                t = Mathf.Pow(Mathf.Clamp01(t), heatContrast);
                Color heatColor = heatGradient.Evaluate(t);
                float heatAlpha = t * opacity;

                if (useFloorPlan)
                {
                    float u = x * invRes;
                    float v = z * invRes;
                    Color baseColor = floorPlanTexture.GetPixelBilinear(u, v) * floorPlanBrightness;
                    Color c = Color.Lerp(baseColor, heatColor, heatAlpha);
                    c.a = 1f;
                    _heatmapTexture.SetPixel(x, z, c);
                }
                else
                {
                    heatColor.a = heatAlpha;
                    _heatmapTexture.SetPixel(x, z, heatColor);
                }
            }

        _heatmapTexture.Apply();
    }

    private void EnsurePlaneAndMaterial()
    {
        if (heatmapPlane == null)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HeatmapPlane";
            quad.transform.SetParent(transform, true);
            quad.transform.position = boundsCenter;
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(boundsSize.x * 2f, boundsSize.y * 2f, 1f);
            heatmapPlane = quad.GetComponent<MeshRenderer>();
        }

        if (_material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Sprites/Default");
            _material = new Material(shader);
            heatmapPlane.sharedMaterial = _material;
        }

        ApplyTextureToMaterial();
    }

    private void ApplyTextureToMaterial()
    {
        if (_heatmapTexture == null || _material == null) return;
        if (_material.HasProperty("_BaseMap"))
            _material.SetTexture("_BaseMap", _heatmapTexture);
        else if (_material.HasProperty("_MainTex"))
            _material.SetTexture("_MainTex", _heatmapTexture);
    }

    private static Gradient DefaultHeatGradient()
    {
        var g = new Gradient();
        Color orange = new Color(1f, 0.5f, 0f);
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.yellow, 0.4f), new GradientColorKey(orange, 0.7f), new GradientColorKey(Color.red, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    [ContextMenu("Reset Heat Gradient to default (白→黄→橙→红)")]
    private void ResetHeatGradientToDefault()
    {
        heatGradient = DefaultHeatGradient();
    }

    private void OnValidate()
    {
        gridResolution = Mathf.Clamp(gridResolution, 8, 256);
        minSamplesToSave = Mathf.Max(0, minSamplesToSave);
        if (_grid != null && _grid.GetLength(0) != gridResolution)
            _grid = new float[gridResolution, gridResolution];
        RefreshBoundsFromSource();
    }

    private void OnDrawGizmosSelected()
    {
        RefreshBoundsFromSource();
        float y = boundsCenter.y;
        float hx = boundsSize.x;
        float hz = boundsSize.y;
        Vector3 p0 = boundsCenter + new Vector3(-hx, 0, -hz);
        Vector3 p1 = boundsCenter + new Vector3(hx, 0, -hz);
        Vector3 p2 = boundsCenter + new Vector3(hx, 0, hz);
        Vector3 p3 = boundsCenter + new Vector3(-hx, 0, hz);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);

        if (referencePoints != null)
        {
            Gizmos.color = referencePointColor;
            foreach (var pt in referencePoints)
            {
                Vector3 p = pt.worldPosition;
                Gizmos.DrawSphere(p, 0.3f);
            }
        }
    }

    private void OnDisable()
    {
        if (autoSaveOnDisable)
            SaveCurrentSession("OnDisable");
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit)
            SaveCurrentSession("OnApplicationQuit");
    }

    [ContextMenu("Save session data now")]
    public void SaveSessionNow()
    {
        SaveCurrentSession("Manual");
    }

    private void SaveCurrentSession(string reason)
    {
        if (_hasSavedSession) return;
        if (samplePositions.Count < minSamplesToSave) return;

        BakeHeatmapTexture();
        string folder = GetOutputFolderPath();
        Directory.CreateDirectory(folder);
        string basePath = Path.Combine(folder, _sessionId);

        bool hadJson = false, hadCsv = false, hadPng = false;
        if (exportJson)
        {
            string jsonPath = basePath + ".json";
            File.WriteAllText(jsonPath, BuildSessionJson(reason));
            hadJson = true;
        }

        if (exportCsv)
        {
            string csvPath = basePath + "_samples.csv";
            File.WriteAllText(csvPath, BuildSamplesCsv());
            hadCsv = true;
        }

        if (exportPng && _heatmapTexture != null)
        {
            if (_lastInBoundsSampleCount > 0 && _lastMaxHeatValue > 0.0001f)
            {
                string pngPath = basePath + ".png";
                File.WriteAllBytes(pngPath, BuildPngBytes());
                hadPng = true;
            }
            else
            {
                Debug.LogWarning("[HeatmapRenderer] PNG 未生成（全黑）：没有任何采样点落在热力图范围内。请检查 Bounds Source 或 Bounds Center/Bounds Size 是否包住玩家走过的区域，并确认 Record Transform 已拖入玩家。");
            }
        }

        _hasSavedSession = true;
        Debug.Log($"[HeatmapRenderer] Session saved: {basePath} | samples={samplePositions.Count}, inBounds={_lastInBoundsSampleCount}, maxHeat={_lastMaxHeatValue:F4}");

        if (uploadToGoogleDrive && googleDriveConfig != null && (hadJson || hadCsv || hadPng))
        {
            Action<bool, string> onDone = (success, error) =>
            {
                if (success)
                    Debug.Log("[HeatmapRenderer] Session uploaded to Google Drive.");
                else
                    Debug.LogWarning("[HeatmapRenderer] Google Drive upload failed: " + (error ?? "unknown"));
            };
            if (reason == "OnApplicationQuit")
            {
                Debug.Log("[HeatmapRenderer] 退出时同步上传到 Google Drive…");
                GoogleDriveClient.UploadSessionToDriveSync(googleDriveConfig, basePath, hadJson, hadCsv, hadPng, onDone);
            }
            else
            {
                Debug.Log("[HeatmapRenderer] 准备上传到 Google Drive（协程已启动）");
                GoogleDriveUploadRunner.Run(GoogleDriveClient.UploadSessionToDrive(googleDriveConfig, basePath, hadJson, hadCsv, hadPng, onDone));
            }
        }
        else if (uploadToGoogleDrive && (googleDriveConfig == null || !(hadJson || hadCsv || hadPng)))
        {
            if (googleDriveConfig == null)
                Debug.LogWarning("[HeatmapRenderer] 未上传: 请指定 Google Drive Config。");
            else if (!(hadJson || hadCsv || hadPng))
                Debug.LogWarning("[HeatmapRenderer] 未上传: 本局未导出任何文件（请检查 Export Json/Csv/Png 及采样点是否在范围内）。");
        }
    }

    private string BuildSessionJson(string reason)
    {
        var export = new HeatmapSessionExport
        {
            sessionId = _sessionId,
            sceneName = SceneManager.GetActiveScene().name,
            startedUtc = _sessionStartUtc.ToString("o"),
            endedUtc = DateTime.UtcNow.ToString("o"),
            endReason = reason,
            sampleCount = samplePositions.Count,
            inBoundsSampleCount = _lastInBoundsSampleCount,
            gridResolution = gridResolution,
            blurRadius = blurRadius,
            maxHeatValue = _lastMaxHeatValue,
            boundsCenter = boundsCenter,
            boundsSize = boundsSize,
            gridWidth = gridResolution,
            gridHeight = gridResolution,
            samples = new List<HeatmapSampleRecord>(_samples)
        };

        for (int z = 0; z < gridResolution; z++)
        for (int x = 0; x < gridResolution; x++)
            export.grid.Add(_grid[x, z]);

        int scale = Mathf.Clamp(exportPngScale, 1, Mathf.Max(1, 4096 / Mathf.Max(gridResolution, gridResolution)));
        int outW = gridResolution * scale;
        int outH = gridResolution * scale;
        float hx = _lastHalfX < 0.01f ? 0.01f : _lastHalfX;
        float hz = _lastHalfZ < 0.01f ? 0.01f : _lastHalfZ;
        foreach (var pt in referencePoints)
        {
            float nx = (pt.worldPosition.x - _lastMinX) / (2f * hx);
            float nz = (pt.worldPosition.z - _lastMinZ) / (2f * hz);
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
            int px = (int)(nx * outW);
            int py = (int)(nz * outH);
            if (exportPngFlipVertical) py = outH - 1 - py;
            export.referencePointsExport.Add(new HeatmapReferencePointExport
            {
                label = pt.label,
                worldX = pt.worldPosition.x,
                worldZ = pt.worldPosition.z,
                pixelX = px,
                pixelY = py,
                exportImageWidth = outW,
                exportImageHeight = outH
            });
        }

        return JsonUtility.ToJson(export, true);
    }

    private string BuildSamplesCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("sample_index,time_seconds,x,y,z");
        for (int i = 0; i < _samples.Count; i++)
        {
            var s = _samples[i];
            sb.Append(i).Append(',')
                .Append(s.t.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(s.x.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(s.y.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(s.z.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }
        return sb.ToString();
    }

    private string GetOutputFolderPath()
    {
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string safeFolder = string.IsNullOrWhiteSpace(outputFolderName) ? "HeatmapSessions" : outputFolderName.Trim();
        return Path.Combine(downloads, safeFolder);
    }

    private string BuildSessionId()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        return $"{sceneName}_{timestamp}";
    }

    /// <summary> 从 boundsSource（Cube 的 Collider 或 Renderer）同步 XZ 范围到 boundsCenter / boundsSize；若 source 已禁用则用缓存的 bounds（避免保存时范围被清空） </summary>
    private void RefreshBoundsFromSource()
    {
        if (boundsSource == null)
            return;

        if (!boundsSource.gameObject.activeInHierarchy)
        {
            if (_hasCachedBounds)
            {
                boundsCenter = _cachedBoundsCenter;
                boundsSize = _cachedBoundsSize;
            }
            return;
        }

        Bounds? b = null;
        var col = boundsSource.GetComponent<Collider>();
        if (col != null)
            b = col.bounds;
        else
        {
            var rend = boundsSource.GetComponent<Renderer>();
            if (rend != null)
                b = rend.bounds;
        }

        if (b.HasValue)
        {
            boundsCenter = b.Value.center;
            boundsSize = new Vector2(b.Value.size.x * 0.5f, b.Value.size.z * 0.5f);
            _cachedBoundsCenter = boundsCenter;
            _cachedBoundsSize = boundsSize;
            _hasCachedBounds = true;
        }
    }

    private bool IsInsideBoundsXZ(Vector3 worldPosition)
    {
        float halfX = boundsSize.x;
        float halfZ = boundsSize.y;
        if (halfX <= 0f || halfZ <= 0f) return false;

        return worldPosition.x >= boundsCenter.x - halfX &&
               worldPosition.x <= boundsCenter.x + halfX &&
               worldPosition.z >= boundsCenter.z - halfZ &&
               worldPosition.z <= boundsCenter.z + halfZ;
    }

    private byte[] BuildPngBytes()
    {
        int w = _heatmapTexture.width;
        int h = _heatmapTexture.height;
        int scale = Mathf.Clamp(exportPngScale, 1, Mathf.Max(1, 4096 / Mathf.Max(w, h)));
        int outW = w * scale;
        int outH = h * scale;
        bool flipY = exportPngFlipVertical;

        Texture2D outTex = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
        for (int iy = 0; iy < outH; iy++)
        {
            int srcY = flipY ? (h - 1 - iy / scale) : iy / scale;
            srcY = Mathf.Clamp(srcY, 0, h - 1);
            for (int ix = 0; ix < outW; ix++)
            {
                int srcX = Mathf.Clamp(ix / scale, 0, w - 1);
                Color src = _heatmapTexture.GetPixel(srcX, srcY);
                if (exportOpaquePng)
                {
                    Color bg = pngBackgroundColor;
                    if (src.a < 0.01f)
                        bg = Color.white;
                    Color dst = Color.Lerp(bg, src, Mathf.Clamp01(src.a));
                    dst.a = 1f;
                    outTex.SetPixel(ix, iy, dst);
                }
                else
                    outTex.SetPixel(ix, iy, src);
            }
        }
        outTex.Apply();

        if (drawReferencePointsOnPng && referencePoints != null && referencePoints.Count > 0 && _lastHalfX >= 0.01f && _lastHalfZ >= 0.01f)
        {
            int r = Mathf.Clamp(referencePointRadius, 1, 20);
            foreach (var pt in referencePoints)
            {
                float nx = (pt.worldPosition.x - _lastMinX) / (2f * _lastHalfX);
                float nz = (pt.worldPosition.z - _lastMinZ) / (2f * _lastHalfZ);
                if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
                int cx = (int)(nx * outW);
                int cy = (int)(nz * outH);
                if (flipY) cy = outH - 1 - cy;
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px < 0 || px >= outW || py < 0 || py >= outH) continue;
                    outTex.SetPixel(px, py, referencePointColor);
                }
            }
            outTex.Apply();
        }

        byte[] bytes = outTex.EncodeToPNG();
        Destroy(outTex);
        return bytes;
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh heatmap in editor")]
    private void EditorRefresh()
    {
        if (_grid == null) _grid = new float[gridResolution, gridResolution];
        BakeHeatmapTexture();
        if (heatmapPlane != null && _heatmapTexture != null)
        {
            if (_material == null) _material = heatmapPlane.sharedMaterial;
            ApplyTextureToMaterial();
        }
    }
#endif
}
