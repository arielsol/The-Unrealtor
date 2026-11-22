#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
// using UnityEngine.AI; // NavMesh 功能可选，如果项目没有 AI Navigation 包则注释掉

/// <summary>
/// 专业关卡数据导出器：导出地面轮廓、房间轮廓、关键点，渲染成干净的 2D 平面图。
/// 功能：
/// - 地面轮廓（walkable area）：从 NavMesh 或地面对象提取
/// - 房间轮廓（region polygon）：从 Collider 或区域对象提取
/// - 关键点（spawn、objective）：标记生成点和目标点
/// - 干净的 2D 渲染（无阴影/后处理）
/// </summary>
public static class HeatmapFloorPlanExporterPro
{
    private const int DefaultResolution = 1024;
    private const string DefaultFolder = "Assets/Heatmap";
    private const string Studio2ObjectName = "Studio2";

    [MenuItem("Tools/Heatmap/Test Menu (Verify Script Loaded)", false, 0)]
    public static void TestMenu()
    {
        Debug.Log("[Heatmap] Heatmap export tool script loaded successfully!");
        EditorUtility.DisplayDialog("Script Loaded", "Heatmap export tool script loaded successfully.\n\nUse menu: Tools → Heatmap → Export Floor Plan", "OK");
    }

    [MenuItem("Tools/Heatmap/Export Floor Plan (Rendered with Materials)", false, 1)]
    [MenuItem("Window/Heatmap/Export Floor Plan (Rendered)", false, 200)]
    public static void ExportRenderedFloorPlan()
    {
        try
        {
            Transform studio2 = FindInScene(Studio2ObjectName);
            if (studio2 == null)
            {
                EditorUtility.DisplayDialog("Studio2 Not Found", $"Could not find object named \"{Studio2ObjectName}\" in current scene.", "OK");
                return;
            }
            if (!GetBoundsFromTransform(studio2, out Vector3 center, out Vector2 size))
            {
                EditorUtility.DisplayDialog("Cannot Calculate Bounds", $"\"{Studio2ObjectName}\" and its children have no Renderer or Collider.", "OK");
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save Floor Plan (Rendered)", "Studio2_FloorPlan_Rendered", "png", "Select save location", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return;
            ExportRenderedFloorPlanImpl(studio2, center, size, path, DefaultResolution);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Heatmap] Export failed: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
        }
    }

    [MenuItem("Tools/Heatmap/Export Floor Plan (Outline: Walkable + Rooms + Key Points)", false, 2)]
    [MenuItem("Window/Heatmap/Export Floor Plan (Outline)", false, 201)]
    public static void ExportProFloorPlan()
    {
        try
        {
            Transform studio2 = FindInScene(Studio2ObjectName);
            if (studio2 == null)
            {
                EditorUtility.DisplayDialog("Studio2 Not Found", $"Could not find object named \"{Studio2ObjectName}\" in current scene.\nPlease open a scene containing Studio2.", "OK");
                Debug.LogWarning($"[Heatmap] Could not find object named \"{Studio2ObjectName}\" in current scene.");
                return;
            }

            if (!GetBoundsFromTransform(studio2, out Vector3 center, out Vector2 size))
            {
                EditorUtility.DisplayDialog("Cannot Calculate Bounds", $"\"{Studio2ObjectName}\" and its children have no Renderer or Collider.", "OK");
                return;
            }

            Debug.Log($"[Heatmap] Found Studio2, center: {center}, half-size: {size}");

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Floor Plan (Outline)",
                "Studio2_FloorPlan_Pro",
                "png",
                "Select save location (recommended: Assets/Heatmap)",
                DefaultFolder);

            if (string.IsNullOrEmpty(path)) return;

            ExportProFloorPlanImpl(studio2, center, size, path, DefaultResolution);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Heatmap] 导出失败: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("导出失败", $"发生错误：{ex.Message}", "确定");
        }
    }

    /// <summary>
    /// 导出实际渲染的平面图：使用场景中物体的真实材质和纹理
    /// </summary>
    private static void ExportRenderedFloorPlanImpl(Transform studio2Root, Vector3 boundsCenter, Vector2 boundsSize, string pngPath, int resolution)
    {
        float halfX = boundsSize.x;
        float halfZ = boundsSize.y;
        if (halfX < 0.01f) halfX = 5f;
        if (halfZ < 0.01f) halfZ = 5f;
        int w = Mathf.Max(512, resolution);
        int h = Mathf.RoundToInt(w * (halfZ / halfX));
        if (h < 512) h = 512;

        float cameraHeight = Mathf.Max(50f, boundsCenter.y + 50f);
        Vector3 camPos = new Vector3(boundsCenter.x, cameraHeight, boundsCenter.z);
        GameObject camGo = new GameObject("HeatmapRenderedCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = halfZ;
        cam.aspect = (2f * halfX) / (2f * halfZ);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        cam.cullingMask = ~0;
        cam.useOcclusionCulling = false;
        cam.allowHDR = false;
        cam.allowMSAA = false;

        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        rt.Create();

        List<SceneMeshInfo> sceneMeshes = CollectAllSceneMeshes(studio2Root);
        Debug.Log($"[Heatmap] 实际渲染模式：收集到 {sceneMeshes.Count} 个 Mesh（使用原始材质）");

        if (!Application.isPlaying)
        {
            var renderWindow = EditorWindow.CreateInstance<ProFloorPlanRenderWindow>();
            renderWindow.SetupRendered(cam, rt, sceneMeshes, pngPath, w, h, camGo);
            renderWindow.Show();
            return;
        }
        RenderWithOriginalMaterials(cam, rt, sceneMeshes);
        SaveTexture(rt, pngPath, w, h, camGo);
    }

    /// <summary>
    /// 场景 Mesh 信息（带原始材质）
    /// </summary>
    private class SceneMeshInfo
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Material material;
        public int submeshIndex;
    }

    /// <summary>
    /// 收集 Studio2 下所有 Mesh，使用原始材质（排除 level1_bounds）
    /// </summary>
    private static List<SceneMeshInfo> CollectAllSceneMeshes(Transform root)
    {
        List<SceneMeshInfo> list = new List<SceneMeshInfo>();
        HashSet<string> excludeNames = new HashSet<string> { "level1_bounds", "Level1_Bounds", "Level1Bounds", "Bounds" };
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || excludeNames.Contains(r.gameObject.name))
                continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh mesh = mf.sharedMesh;
            Matrix4x4 matrix = r.transform.localToWorldMatrix;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                Material mat = (r is MeshRenderer mr && i < mr.sharedMaterials.Length) ? mr.sharedMaterials[i] : r.sharedMaterial;
                if (mat == null) mat = new Material(Shader.Find("Unlit/Color")) { color = Color.gray };
                list.Add(new SceneMeshInfo { mesh = mesh, matrix = matrix, material = mat, submeshIndex = i });
            }
        }
        return list;
    }

    private static void RenderWithOriginalMaterials(Camera cam, RenderTexture rt, List<SceneMeshInfo> meshes)
    {
        cam.targetTexture = rt;
        RenderTexture.active = rt;
        GL.Clear(true, true, cam.backgroundColor);
        foreach (var info in meshes)
            Graphics.DrawMesh(info.mesh, info.matrix, info.material, info.submeshIndex, cam, 0);
        cam.Render();
    }

    /// <summary>
    /// 导出专业平面图：包含地面轮廓、房间轮廓、关键点（轮廓版）
    /// </summary>
    private static void ExportProFloorPlanImpl(Transform studio2Root, Vector3 boundsCenter, Vector2 boundsSize, string pngPath, int resolution)
    {
        float halfX = boundsSize.x;
        float halfZ = boundsSize.y;
        if (halfX < 0.01f) halfX = 5f;
        if (halfZ < 0.01f) halfZ = 5f;

        // 计算分辨率
        int w = Mathf.Max(512, resolution);
        int h = Mathf.RoundToInt(w * (halfZ / halfX));
        if (h < 512) h = 512;

        // 创建相机（俯视角度）
        float cameraHeight = Mathf.Max(50f, boundsCenter.y + 50f);
        Vector3 camPos = new Vector3(boundsCenter.x, cameraHeight, boundsCenter.z);
        GameObject camGo = new GameObject("HeatmapProExportCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = halfZ;
        cam.aspect = (2f * halfX) / (2f * halfZ);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.cullingMask = ~0;
        cam.useOcclusionCulling = false;
        cam.allowHDR = false;
        cam.allowMSAA = false;

        // 创建 RenderTexture
        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        rt.Create();

        // 收集数据
        List<WalkableArea> walkableAreas = CollectWalkableAreas(studio2Root);
        List<RoomRegion> roomRegions = CollectRoomRegions(studio2Root);
        List<KeyPoint> keyPoints = CollectKeyPoints(studio2Root);

        Debug.Log($"[Heatmap] 收集到：{walkableAreas.Count} 个地面区域，{roomRegions.Count} 个房间区域，{keyPoints.Count} 个关键点");

        // 在 Editor 模式下使用 EditorWindow 渲染
        if (!Application.isPlaying)
        {
            var renderWindow = EditorWindow.CreateInstance<ProFloorPlanRenderWindow>();
            renderWindow.Setup(cam, rt, walkableAreas, roomRegions, keyPoints, pngPath, w, h, camGo, boundsCenter, boundsSize);
            renderWindow.Show();
            return;
        }

        // Play 模式下直接渲染
        RenderProFloorPlan(cam, rt, walkableAreas, roomRegions, keyPoints, w, h);
        SaveTexture(rt, pngPath, w, h, camGo);
    }

    /// <summary>
    /// 地面区域信息
    /// </summary>
    private class WalkableArea
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Color color = new Color(0.9f, 0.9f, 0.85f, 1f); // 浅米色
    }

    /// <summary>
    /// 房间区域信息
    /// </summary>
    private class RoomRegion
    {
        public List<Vector3> vertices; // 房间轮廓顶点（XZ 平面）
        public Color color = new Color(0.7f, 0.8f, 0.9f, 0.3f); // 浅蓝色，半透明
    }

    /// <summary>
    /// 关键点信息
    /// </summary>
    private class KeyPoint
    {
        public Vector3 position;
        public string label;
        public KeyPointType type;
        public Color color;
    }

    private enum KeyPointType
    {
        Spawn,
        Objective,
        Other
    }

    /// <summary>
    /// 收集地面区域（walkable area）
    /// </summary>
    private static List<WalkableArea> CollectWalkableAreas(Transform root)
    {
        List<WalkableArea> areas = new List<WalkableArea>();
        
        // 方法1：从 NavMesh 提取（如果有）- 需要 Unity AI Navigation 包
        // 如果项目安装了 AI Navigation 包，可以取消下面的注释
        /*
        #if UNITY_AI_NAVIGATION
        if (NavMeshSurface.activeSurfaces != null && NavMeshSurface.activeSurfaces.Count > 0)
        {
            Debug.Log("[Heatmap] 检测到 NavMesh，但 NavMesh 数据提取需要额外处理");
        }
        #endif
        */

        // 方法2：从地面对象提取（查找名称包含 "floor", "ground", "walkable" 的对象）
        HashSet<string> walkableKeywords = new HashSet<string> { "floor", "ground", "walkable", "地面", "Floor", "Ground" };
        HashSet<string> excludeNames = new HashSet<string> { "level1_bounds", "Level1_Bounds", "Bounds" };

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            string objName = renderer.gameObject.name.ToLower();
            
            // 排除边界对象
            if (excludeNames.Contains(renderer.gameObject.name))
                continue;

            // 检查是否是地面对象
            bool isWalkable = false;
            foreach (var keyword in walkableKeywords)
            {
                if (objName.Contains(keyword.ToLower()))
                {
                    isWalkable = true;
                    break;
                }
            }

            // 或者检查 Layer（如果地面在特定 Layer）
            // if (renderer.gameObject.layer == LayerMask.NameToLayer("Ground"))

            if (isWalkable)
            {
                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    areas.Add(new WalkableArea
                    {
                        mesh = mf.sharedMesh,
                        matrix = renderer.transform.localToWorldMatrix
                    });
                }
            }
        }

        // 如果没有找到明确的地面对象，使用所有非边界的地面 Mesh
        if (areas.Count == 0)
        {
            Debug.Log("[Heatmap] 未找到明确的地面对象，使用所有 Y 坐标较低的对象作为地面");
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || excludeNames.Contains(renderer.gameObject.name))
                    continue;

                // 只考虑 Y 坐标较低的对象（可能是地面）
                if (renderer.bounds.center.y < root.position.y + 2f)
                {
                    MeshFilter mf = renderer.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        areas.Add(new WalkableArea
                        {
                            mesh = mf.sharedMesh,
                            matrix = renderer.transform.localToWorldMatrix
                        });
                    }
                }
            }
        }

        return areas;
    }

    /// <summary>
    /// 收集房间区域（region polygon）
    /// </summary>
    private static List<RoomRegion> CollectRoomRegions(Transform root)
    {
        List<RoomRegion> regions = new List<RoomRegion>();

        // 方法1：从 BoxCollider 提取房间轮廓
        BoxCollider[] boxColliders = root.GetComponentsInChildren<BoxCollider>(true);
        foreach (var col in boxColliders)
        {
            if (col == null || !col.enabled || col.gameObject.name.Contains("bounds"))
                continue;

            // 提取 BoxCollider 的 XZ 平面轮廓
            Bounds bounds = col.bounds;
            List<Vector3> vertices = new List<Vector3>
            {
                new Vector3(bounds.min.x, bounds.center.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.center.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.center.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.center.y, bounds.max.z)
            };

            regions.Add(new RoomRegion { vertices = vertices });
        }

        // 方法2：从名称包含 "room", "region", "area" 的对象提取
        HashSet<string> regionKeywords = new HashSet<string> { "room", "region", "area", "房间", "区域" };
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            string objName = renderer.gameObject.name.ToLower();
            foreach (var keyword in regionKeywords)
            {
                if (objName.Contains(keyword.ToLower()))
                {
                    Bounds bounds = renderer.bounds;
                    List<Vector3> vertices = new List<Vector3>
                    {
                        new Vector3(bounds.min.x, bounds.center.y, bounds.min.z),
                        new Vector3(bounds.max.x, bounds.center.y, bounds.min.z),
                        new Vector3(bounds.max.x, bounds.center.y, bounds.max.z),
                        new Vector3(bounds.min.x, bounds.center.y, bounds.max.z)
                    };
                    regions.Add(new RoomRegion { vertices = vertices });
                    break;
                }
            }
        }

        return regions;
    }

    /// <summary>
    /// 收集关键点（spawn、objective）
    /// </summary>
    private static List<KeyPoint> CollectKeyPoints(Transform root)
    {
        List<KeyPoint> points = new List<KeyPoint>();

        // 查找名称包含 "spawn", "spawnpoint", "start" 的对象
        HashSet<string> spawnKeywords = new HashSet<string> { "spawn", "spawnpoint", "start", "生成点" };
        HashSet<string> objectiveKeywords = new HashSet<string> { "objective", "target", "goal", "目标", "终点" };

        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == null) continue;

            string objName = t.gameObject.name.ToLower();
            KeyPoint point = null;

            // 检查是否是生成点
            foreach (var keyword in spawnKeywords)
            {
                if (objName.Contains(keyword.ToLower()))
                {
                    point = new KeyPoint
                    {
                        position = t.position,
                        label = t.gameObject.name,
                        type = KeyPointType.Spawn,
                        color = Color.green
                    };
                    break;
                }
            }

            // 检查是否是目标点
            if (point == null)
            {
                foreach (var keyword in objectiveKeywords)
                {
                    if (objName.Contains(keyword.ToLower()))
                    {
                        point = new KeyPoint
                        {
                            position = t.position,
                            label = t.gameObject.name,
                            type = KeyPointType.Objective,
                            color = Color.red
                        };
                        break;
                    }
                }
            }

            if (point != null)
            {
                points.Add(point);
            }
        }

        return points;
    }

    /// <summary>
    /// 渲染专业平面图
    /// </summary>
    private static void RenderProFloorPlan(Camera cam, RenderTexture rt, List<WalkableArea> walkableAreas, 
        List<RoomRegion> roomRegions, List<KeyPoint> keyPoints, int w, int h)
    {
        cam.targetTexture = rt;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.white);

        // 创建材质
        Material walkableMat = new Material(Shader.Find("Unlit/Color"));
        walkableMat.color = new Color(0.9f, 0.9f, 0.85f, 1f);

        Material roomMat = new Material(Shader.Find("Unlit/Color"));
        roomMat.color = new Color(0.7f, 0.8f, 0.9f, 0.5f);

        // 1. 渲染地面区域
        foreach (var area in walkableAreas)
        {
            walkableMat.color = area.color;
            Graphics.DrawMesh(area.mesh, area.matrix, walkableMat, 0, cam, 0);
        }

        // 2. 渲染房间轮廓（使用 GL 绘制线条）
        DrawRoomRegions(roomRegions, cam, rt, w, h);

        // 3. 渲染关键点（使用 GL 绘制圆点）
        DrawKeyPoints(keyPoints, cam, rt, w, h);

        cam.Render();

        Object.DestroyImmediate(walkableMat);
        Object.DestroyImmediate(roomMat);
    }

    /// <summary>
    /// 绘制房间轮廓
    /// </summary>
    private static void DrawRoomRegions(List<RoomRegion> regions, Camera cam, RenderTexture rt, int w, int h)
    {
        // 注意：GL 绘制需要在正确的上下文中进行
        // 这里简化处理，实际应该在 EditorWindow 的 OnGUI 中调用
    }

    /// <summary>
    /// 绘制关键点
    /// </summary>
    private static void DrawKeyPoints(List<KeyPoint> points, Camera cam, RenderTexture rt, int w, int h)
    {
        // 注意：GL 绘制需要在正确的上下文中进行
        // 这里简化处理，实际应该在 EditorWindow 的 OnGUI 中调用
    }

    /// <summary>
    /// 临时的 EditorWindow，用于在 Editor 模式下渲染
    /// </summary>
    private class ProFloorPlanRenderWindow : EditorWindow
    {
        private Camera cam;
        private RenderTexture rt;
        private List<WalkableArea> walkableAreas;
        private List<RoomRegion> roomRegions;
        private List<KeyPoint> keyPoints;
        private List<SceneMeshInfo> sceneMeshesForRendered;
        private string pngPath;
        private int width, height;
        private GameObject camGo;
        private Vector3 boundsCenter;
        private Vector2 boundsSize;
        private bool hasRendered = false;
        private bool isRenderedMode = false;

        public void Setup(Camera camera, RenderTexture renderTexture, List<WalkableArea> areas, 
            List<RoomRegion> regions, List<KeyPoint> points, string path, int w, int h, 
            GameObject cameraGo, Vector3 center, Vector2 size)
        {
            cam = camera;
            rt = renderTexture;
            walkableAreas = areas;
            roomRegions = regions;
            keyPoints = points;
            sceneMeshesForRendered = null;
            isRenderedMode = false;
            pngPath = path;
            width = w;
            height = h;
            camGo = cameraGo;
            boundsCenter = center;
            boundsSize = size;
            position = new Rect(100, 100, w, h);
            titleContent = new GUIContent("渲染专业平面图...");
        }

        public void SetupRendered(Camera camera, RenderTexture renderTexture, List<SceneMeshInfo> sceneMeshes, string path, int w, int h, GameObject cameraGo)
        {
            cam = camera;
            rt = renderTexture;
            sceneMeshesForRendered = sceneMeshes;
            walkableAreas = null;
            roomRegions = null;
            keyPoints = null;
            isRenderedMode = true;
            pngPath = path;
            width = w;
            height = h;
            camGo = cameraGo;
            boundsCenter = Vector3.zero;
            boundsSize = Vector2.zero;
            position = new Rect(100, 100, w, h);
            titleContent = new GUIContent("渲染平面图（实际材质）...");
        }

        private void OnGUI()
        {
            if (!hasRendered && cam != null && rt != null)
            {
                cam.targetTexture = rt;
                RenderTexture.active = rt;
                
                Material walkableMat = null;
                Material roomMat = null;
                if (isRenderedMode && sceneMeshesForRendered != null)
                {
                    // 实际渲染模式：使用原始材质
                    GL.Clear(true, true, cam.backgroundColor);
                    foreach (var info in sceneMeshesForRendered)
                        Graphics.DrawMesh(info.mesh, info.matrix, info.material, info.submeshIndex, cam, 0);
                    cam.Render();
                }
                else
                {
                    // 轮廓模式
                    GL.Clear(true, true, Color.white);
                    walkableMat = new Material(Shader.Find("Unlit/Color"));
                    roomMat = new Material(Shader.Find("Unlit/Color"));
                    foreach (var area in walkableAreas)
                    {
                        walkableMat.color = area.color;
                        Graphics.DrawMesh(area.mesh, area.matrix, walkableMat, 0, cam, 0);
                    }
                    DrawRoomRegionsGL(roomRegions);
                    DrawKeyPointsGL(keyPoints);
                    cam.Render();
                }

                // 等待一帧确保渲染完成
                EditorApplication.delayCall += () =>
                {
                    RenderTexture.active = rt;
                    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();

                    if (!isRenderedMode && roomRegions != null && keyPoints != null)
                        DrawOnTexture(tex, roomRegions, keyPoints);

                    byte[] png = tex.EncodeToPNG();
                    Object.DestroyImmediate(tex);

                    string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", pngPath));
                    string dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllBytes(fullPath, png);
                    AssetDatabase.Refresh();

                    string assetPath = pngPath.Replace('\\', '/');
                    if (!assetPath.StartsWith("Assets/"))
                        assetPath = "Assets/" + assetPath.TrimStart('/');
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }

                    Debug.Log($"[Heatmap] 专业平面图已导出: {pngPath}（{width}x{height}）。");

                    RenderTexture.active = null;
                    rt.Release();
                    Object.DestroyImmediate(rt);
                    Object.DestroyImmediate(camGo);
                    if (walkableMat != null) Object.DestroyImmediate(walkableMat);
                    if (roomMat != null) Object.DestroyImmediate(roomMat);
                    hasRendered = true;
                    Close();
                };
            }
        }

        private void DrawRoomRegionsGL(List<RoomRegion> regions)
        {
            // GL 绘制需要在 Begin/End 之间
            // 这里简化处理，实际绘制在 DrawOnTexture 中进行
        }

        private void DrawKeyPointsGL(List<KeyPoint> points)
        {
            // GL 绘制需要在 Begin/End 之间
            // 这里简化处理，实际绘制在 DrawOnTexture 中进行
        }

        /// <summary>
        /// 在 Texture2D 上绘制房间轮廓和关键点
        /// </summary>
        private void DrawOnTexture(Texture2D tex, List<RoomRegion> regions, List<KeyPoint> points)
        {
            // 将世界坐标转换为像素坐标
            float minX = boundsCenter.x - boundsSize.x;
            float maxX = boundsCenter.x + boundsSize.x;
            float minZ = boundsCenter.z - boundsSize.y;
            float maxZ = boundsCenter.z + boundsSize.y;

            // 绘制房间轮廓
            foreach (var region in regions)
            {
                if (region.vertices == null || region.vertices.Count < 3)
                    continue;

                // 将世界坐标转换为像素坐标
                List<Vector2> pixelPoints = new List<Vector2>();
                foreach (var v in region.vertices)
                {
                    float u = (v.x - minX) / (maxX - minX);
                    float v_coord = (v.z - minZ) / (maxZ - minZ);
                    int px = (int)(u * width);
                    int py = (int)((1f - v_coord) * height); // 翻转 Y 轴
                    pixelPoints.Add(new Vector2(px, py));
                }

                // 绘制多边形轮廓
                DrawPolygonOutline(tex, pixelPoints, region.color);
            }

            // 绘制关键点
            foreach (var point in points)
            {
                float u = (point.position.x - minX) / (maxX - minX);
                float v_coord = (point.position.z - minZ) / (maxZ - minZ);
                int px = (int)(u * width);
                int py = (int)((1f - v_coord) * height);

                DrawCircle(tex, px, py, 8, point.color); // 8 像素半径
            }
        }

        /// <summary>
        /// 绘制多边形轮廓
        /// </summary>
        private void DrawPolygonOutline(Texture2D tex, List<Vector2> points, Color color)
        {
            if (points.Count < 2) return;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p1 = points[i];
                Vector2 p2 = points[(i + 1) % points.Count];
                DrawLine(tex, (int)p1.x, (int)p1.y, (int)p2.x, (int)p2.y, color, 2);
            }
        }

        /// <summary>
        /// 绘制直线
        /// </summary>
        private void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            // Bresenham 直线算法
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // 绘制点及其周围（形成厚度）
                for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
                {
                    for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                    {
                        int px = x0 + tx;
                        int py = y0 + ty;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            tex.SetPixel(px, py, color);
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>
        /// 绘制圆形
        /// </summary>
        private void DrawCircle(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            tex.SetPixel(px, py, color);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 保存纹理
    /// </summary>
    private static void SaveTexture(RenderTexture rt, string pngPath, int w, int h, GameObject camGo)
    {
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", pngPath));
        string dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(fullPath, png);
        AssetDatabase.Refresh();

        string assetPath = pngPath.Replace('\\', '/');
        if (!assetPath.StartsWith("Assets/"))
            assetPath = "Assets/" + assetPath.TrimStart('/');
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Debug.Log($"[Heatmap] 专业平面图已导出: {pngPath}（{w}x{h}）。");

        RenderTexture.active = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
    }

    /// <summary>
    /// 在场景中查找指定名称的 Transform
    /// </summary>
    private static Transform FindInScene(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (string.Equals(parent.name, name, System.StringComparison.OrdinalIgnoreCase))
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindRecursive(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 获取 Transform 及其子物体的合并 Bounds
    /// </summary>
    private static bool GetBoundsFromTransform(Transform root, out Vector3 center, out Vector2 size)
    {
        center = root.position;
        size = Vector2.zero;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            center = b.center;
            size = new Vector2(b.size.x * 0.5f, b.size.z * 0.5f);
            return size.x >= 0.01f && size.y >= 0.01f;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                b.Encapsulate(colliders[i].bounds);
            center = b.center;
            size = new Vector2(b.size.x * 0.5f, b.size.z * 0.5f);
            return size.x >= 0.01f && size.y >= 0.01f;
        }

        return false;
    }
}
#endif
