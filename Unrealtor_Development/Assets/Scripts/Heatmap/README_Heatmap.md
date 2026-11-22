# 3D 场景热力图使用说明

---

## 零基础：每一关活动范围怎么设（用地板还是 Cube？）

### 用「地板」还是「Cube」？

- **推荐用「能包住整间房的一个物体」**，两种都可以，看哪个方便：
  - **Cube / 方盒子**：一整间房就是一个 Cube 时，用这个最简单。拖到 **Bounds Source** 就行，范围就是 Cube 的 XZ 底面。
  - **地板（Floor）**：如果这一关只有**一块**地板，且地板正好就是活动范围，可以用地板。需要地板带有 **Collider** 或 **MeshRenderer**（一般都有）。
- **选哪个**：  
  - 有一块**完整的地板**且能代表整间房 → 用地板做 Bounds Source 没问题。  
  - 地板很多块、或形状不规则、或不好选 → 在房间里放一个**空物体 + Box Collider**（见下面步骤），用这个当「关卡范围盒子」更清晰。

下面用「第一关小房间」举例，按步骤做即可。

---

### 第一关小房间：具体设置步骤（无经验也能做）

1. **定好“这一关的范围”要用哪个物体**
   - 若是**一个 Cube** 代表房间：在 Hierarchy 里选中这个 Cube。
   - 若是**一块地板**：选中代表第一关活动范围的那块地板。
   - 若没有现成的：在 Hierarchy 里 **右键 → 3D Object → Cube**，把 Cube 拉到能包住整间房的大小和位置，改名为例如 `Level1_Bounds`。

2. **保证这个物体有“范围”可读**
   - 选中该物体，看 Inspector 里有没有 **Box Collider**（或 **Mesh Renderer**）。
   - **没有 Collider**：点 **Add Component** → 搜 **Box Collider** → 添加。勾不勾 **Is Trigger** 都可以，热力图只读范围。
   - 有 **Mesh Renderer** 也行，脚本会用它算范围。

3. **创建热力图并绑范围**
   - Hierarchy 里 **右键 → Create Empty**，命名为例如 `Heatmap_Level1`。
   - 选中 `Heatmap_Level1`，**Add Component** → 搜 **Heatmap Renderer**，添加。
   - 在 Heatmap Renderer 里找到 **Bounds Source**，把上面第 1 步选中的**地板或 Cube** 从 Hierarchy 拖进去。

4. **让热力图记录玩家**
   - 同一组件里找到 **Record Transform**，把**玩家**从 Hierarchy 拖进去。
   - 确认 **Record Only Inside Bounds** 打勾（只记录在范围内的点）。

5. **确认范围在 Scene 里能看到**
   - 选中 `Heatmap_Level1`，在 **Scene 视图**里会看到**绿色线框**，就是当前热力图范围。线框要包住小房间地板，不要偏太多。

6. **运行与查看数据**
   - 点 Play，在第一关小房间里走一会儿，然后退出 Play 或切场景。
   - 数据会自动保存。查看方式见下文「每次游玩自动保存」的目录说明。

这样第一关的范围就设好了：**用地板或 Cube 都可以，只要把它拖到 Bounds Source，并保证有 Collider 或 Renderer**。

---

### 多关卡时（每一关一个范围、游戏结束看数据）

- **每一关一个热力图物体**：例如 `Heatmap_Level1`、`Heatmap_Level2`…，每个的 **Bounds Source** 拖该关的**地板或 Cube**。
- **只记录当前关**：在关卡切换时，**只启用当前关的 Heatmap 物体**（当前关的 GameObject 勾选激活，其它关的取消勾选）。这样：
  - 进入某关时启用该关的 Heatmap；
  - 离开该关时禁用该关的 Heatmap，脚本会 **OnDisable** 自动保存这一关的数据。
- 游戏结束时，每个关卡会对应各自的导出文件（按场景名/时间戳区分），在导出目录里就能看到各关的热力图数据。

---

## 在 Unity 里怎么用

1. **挂到场景里**
   - 在场景里新建一个空物体（GameObject），命名为例如 `Heatmap`。
   - 把 `HeatmapRenderer.cs` 挂到这个物体上。

2. **设好范围**
   - **Bounds Center**：热力图覆盖区域的中心（世界坐标），一般是地板的中心。
   - **Bounds Size**：X、Z 方向的半边长（例如 10 表示从中心向左右、前后各 10 米）。

3. **数据从哪来（二选一或一起用）**
   - **Record Transform**：拖入玩家或要统计的物体，运行时会按 **Record Interval** 间隔自动记录位置。
   - **Sample Positions**：在代码里调用 `GetComponent<HeatmapRenderer>().AddSample(某个世界坐标)`，或把预先算好的位置放进列表（需在运行前通过脚本写入）。

4. **运行**
   - 进入 Play 模式后，若没有指定 **Heatmap Plane**，脚本会自动在 `boundsCenter` 位置创建一个平铺的 Quad 显示热力图。
   - 热力图会随采样点更新（若开了 Record Transform 或你在代码里持续 AddSample）。

5. **在编辑器里预览**
   - 在 Inspector 里对 `HeatmapRenderer` 右键 → **Refresh heatmap in editor**，会根据当前 **Sample Positions** 刷新一次显示（需先有采样点）。

## 可选调整

- **Grid Resolution**：越大越细腻，性能开销也略增（如 64、128）。
- **Blur Radius**：每个点影响周围多少格，越大越糊、越平滑。
- **Heat Gradient**：冷色（蓝/绿）表示少，热色（黄/红）表示多；可在 Inspector 里改渐变。
- **Opacity**：整体透明度。
- **Auto Save On Disable / Auto Save On Quit**：自动导出每局数据（推荐开启）。
- **Output Folder Name**：导出目录名（位于 `Application.persistentDataPath` 下）。
- **Export Json / Csv / Png**：导出格式开关。
- **Min Samples To Save**：最少采样点数量，防止误保存空数据。
- **Reference Points（标注点）**：在 **Reference Points** 列表里添加 `label` + `worldPosition`（如门、出生点），用于在导出 JSON 中记录「地图上哪里是哪里」。勾选 **Draw Reference Points On Png** 时，导出 PNG 上会画出这些点的圆点，方便和 JSON 里的 `referencePointsExport`（含 label、worldX/Z、pixelX/Y、图片宽高）对照做分析。

## 典型用法

- **玩家停留热力图**：把玩家 Transform 赋给 **Record Transform**，跑一段时间后看哪里站得久。
- **点击/事件热力图**：在点击或事件发生时调用 `AddSample(hit.point)`，再定期或结束时调用 `BakeHeatmapTexture()` 或依赖 Update 里的自动刷新（若你是在 Update 里 AddSample）。

## 每次游玩自动保存（用于数据分析）

脚本会按“每局一个 session”导出文件，默认目录：

- `Application.persistentDataPath/HeatmapSessions/`

每局会生成（按开关决定）：

- `场景名_时间戳.json`：完整会话数据（元信息、网格、样本点）。
- `场景名_时间戳_samples.csv`：原始样本点（time, x, y, z），便于 Python/R 分析。
- `场景名_时间戳.png`：热力图可视化快照。

可在组件右键菜单手动触发：

- **Save session data now**

### 上传到 Google 云端硬盘（代码直连）

保存到本地的同时，可将同一批文件（JSON / CSV / PNG）自动上传到你的 Google Drive：

1. **在 Google Cloud Console 配置 OAuth**
   - 打开 [Google Cloud Console](https://console.cloud.google.com/) → 选择或新建项目 → **API 和服务** → **凭据** → **创建凭据** → **OAuth 2.0 客户端 ID**。
   - 应用类型选 **Web 应用**（或桌面应用），**授权重定向 URI** 添加：`http://localhost:8765/`（必须一致）。
   - 在 **API 和服务** → **已启用的 API** 中启用 **Google Drive API**。
   - 复制 **客户端 ID** 和 **客户端密钥**。

2. **在 Unity 里创建配置资产**
   - Project 窗口右键 → **Create** → **Heatmap** → **Google Drive Config**，在 Inspector 中填入上面的 **Client ID** 和 **Client Secret**。
   - 可选：在 **Folder Id** 中填入要上传到的文件夹 ID（在网盘打开该文件夹，地址栏 URL 里 `folders/` 后面那串即为 ID）；留空则上传到「我的云端硬盘」根目录。

3. **连接账号**
   - 菜单 **Tools** → **Heatmap** → **Connect Google Drive**，在浏览器中登录 Google 并授权，授权完成后回到 Unity 即可。

4. **启用上传**
   - 在挂有 `HeatmapRenderer` 的物体上，勾选 **Upload To Google Drive**，并把上面创建的 **Google Drive Config** 资产拖到 **Google Drive Config** 栏。
   - 之后每次自动或手动保存 session 时，会先写入本地（Downloads/HeatmapSessions），再在后台上传到 Google Drive。

建议将 Google Drive Config 资产或所在目录加入版本控制忽略，避免泄露密钥。

### macOS 下目录示例

- `~/Library/Application Support/<CompanyName>/<ProductName>/HeatmapSessions/`

---

## 导出图片看起来不对时

- **方向反了 / 上下颠倒**：勾选 **Export Png Flip Vertical**（默认已勾），导出的 PNG 会与 Scene 俯视图一致（图片上方 = 场景 Z+）。
- **整张图几乎一个颜色**：检查 **Bounds Source** 或 **Bounds Center / Bounds Size** 是否包住玩家路径；看 Console 里保存日志的 `inBounds=` 是否大于 0。
- **热区位置和场景对不上**：在 Scene 里选中 Heatmap 物体，看绿色线框是否就是关卡范围；若用 Bounds Source，确认拖入的是该关的 Cube/地板。
- **太糊或太碎**：调 **Blur Radius**（大一点更平滑）、**Grid Resolution**（大一点更细腻）。
