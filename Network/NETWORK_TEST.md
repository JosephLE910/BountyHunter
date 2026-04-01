# 网络同步模块测试方法

## 场景搭建步骤

### 1. 配置 NetworkManager

在测试场景（或主场景）中：
1. 新建空物体，命名 `NetworkManager`
2. Add Component → `Network Manager`（Unity Netcode）
3. Add Component → `Unity Transport`
4. NetworkManager Inspector 中：
   - **Player Prefab** → 拖入 KartPrefab（见下方）

### 2. 制作 KartPrefab

1. 场景中新建空物体，命名 `KartPlayer`
2. 依次 Add Component：
   - `Network Object`（NGO 必须，自动分配 NetworkObjectId）
   - `Rigidbody`
   - `KartController`（Physics 模块，填入 WheelCollider 引用）
   - `InputHandler`（Shared 模块）
   - `ClientPrediction`
   - `InterpolationSystem`（默认 disabled，非 Owner 自动启用）
   - `NetworkKartController`（Network 模块，填入各引用）
   - `LagCompensator`（可选，用于碰撞判定）
3. 配置好后，从 Hierarchy 拖到 Project 面板生成 Prefab
4. 将 Prefab 拖入 NetworkManager 的 **Player Prefab** 槽

### 3. 配置 LobbyManager UI

在场景 Canvas 下新建空物体，Add Component → `LobbyManager`，建立以下子 UI：

| UI 元素 | 类型 | 说明 |
|---------|------|------|
| HostButton | Button | 点击后启动 Host |
| JoinButton | Button | 点击后作为 Client 连接 |
| IpField | TMP_InputField | 输入目标 IP |
| StatusText | TextMeshProUGUI | 显示连接状态 |

---

## 本机双开测试（最快验证方法）

### 方法 A：Editor + Build

1. Unity Editor 中运行场景，点 **Host**
2. 菜单 File → Build And Run，打开 exe，点 **Join**（IP 填 `127.0.0.1`）
3. 观察：两个窗口中都应出现两辆车

### 方法 B：ParrelSync（推荐，无需 Build）

1. Package Manager 安装 **ParrelSync**（Add from git URL）
2. 菜单 ParrelSync → Clone Manager → Add new clone
3. 克隆项目中打开同一场景，点 **Join**
4. 主项目点 **Host**

---

## 验证点

| 测试项 | 预期现象 | 对应代码 |
|--------|----------|----------|
| 基础连接 | 两端均出现两辆车 | `NetworkManager`、`LobbyManager` |
| 远端车辆平滑移动 | Client 看到 Host 的车平滑移动，无跳帧 | `InterpolationSystem` |
| 客户端预测 | 本地车操控无延迟 | `ClientPrediction` + `OwnerFixedUpdate` |
| 纠错回滚 | 模拟 200ms 延迟时本地车偶发轻微抖动后恢复 | `OnAuthorativeStateReceived` |
| 外推 | 对方网络中断后，远端车继续沿原方向短暂滑行后停止 | `InterpolationSystem.Extrapolate` |

---

## 模拟弱网测试

Unity Transport 支持内置网络模拟：

1. 选中 NetworkManager 下的 `Unity Transport` 组件
2. Inspector → **Simulator Pipeline** 勾选启用
3. 参数建议：
   - **Packet Delay**: 100ms（模拟 100ms RTT）
   - **Packet Jitter**: 20ms（抖动）
   - **Packet Drop Rate**: 5%（丢包率）

弱网下观察：
- 远端车移动是否仍然平滑（插值有效）
- 丢包后是否触发外推（`MaxExtrapolateTime = 0.5s`）
- 本地车是否偶发位置纠正（回滚阈值 `CorrectionThreshold = 0.2m`）
