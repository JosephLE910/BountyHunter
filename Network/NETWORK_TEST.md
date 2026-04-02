# 网络同步模块测试流程

场景：`Assets/Karting/Scenes/GameplayGyms/PhysicsPlayground.unity`

---

## 前置确认（打开场景后先做）

### 1. 检查 Console 无报错
`Window > General > Console`，确认没有红色 Error（黄色 Warning 可忽略）。

### 2. 确认 Hierarchy 中已有以下对象

| 对象名 | 位置 | 说明 |
|--------|------|------|
| `NetworkManager` | 根层级 | 含 `UnityTransport`，端口 7777 |
| `AdditionalInGameData/LobbyPanel` | Canvas 子层级 | Host / Join 按钮 + IP 框 + 状态文字 |
| `NewKartClassic_Player` | 根层级 | 含 `NetworkObject` + `NetworkArcadeKartController` |

### 3. 确认 NetworkManager 配置

点选 Hierarchy 中的 `NetworkManager`，Inspector 中确认：
- **Player Prefab** → `NetworkKartPlayer`（不能为 None）
- **Network Transport** → `UnityTransport`，Protocol Type = `Unity Transport`

---

## 阶段一：单端 Host 连通性验证

**目的**：确认 NGO 正常启动、车辆物理不受影响。

1. 点击 Unity 工具栏的 **▶ Play**
2. Game 视图左上角出现 `LobbyPanel`（黑色半透明面板）
3. 点击 **Host（主机）** 按钮
4. 观察状态文字变为：`已启动 Host，等待玩家加入...`
5. 用 **WASD / 方向键** 开车，确认物理正常
6. 打开 Console，确认没有 NGO 错误
7. 点击 **▶ Stop**（或再按一次 Play）

---

## 阶段二：本机双开测试（Editor + Build）

这是验证同步效果的核心测试。

### 第一步：Build

1. 菜单 **File > Build Settings**
2. 点 **Add Open Scenes**，确认 `PhysicsPlayground` 已勾选
3. Platform 选 **PC, Mac & Linux Standalone**，Target Platform = **Windows**
4. 点 **Build**，选一个输出目录（如 `D:/Build/NetworkTest/`）
5. 等待 Build 完成（约 1 ~ 3 分钟）

### 第二步：Editor 端作为 Host

1. Unity Editor 中点 **▶ Play**
2. 点 **Host（主机）**
3. 状态变为 `已启动 Host，等待玩家加入...`
4. **不要关闭 Play 模式**

### 第三步：Build 端作为 Client

1. 运行刚才 Build 出的 `.exe`
2. 左上角 LobbyPanel 出现
3. IP 输入框默认是 `127.0.0.1`，不用改
4. 点击 **Join（加入）**
5. 状态变为 `连接成功！`

### 第四步：观察验证

**在 Editor（Host）端**：
- Console 打印 `玩家加入 (id=1)，当前人数: 2`
- 场景中出现 **两辆车**：一辆是 Host 自己，一辆是 Client 刚 spawn 的

**在 Build（Client）端**：
- 场景中同样出现 **两辆车**
- 自己的车可以用 WASD 驾驶
- 另一辆车（Host 的）会跟随 Host 的操作移动（InterpolationSystem 平滑）

### ⚠️ 摄像机说明

CinemachineVirtualCamera 当前跟随的是场景内原来放置的那辆车（Host 的）。

- **Host 端**：摄像机正常跟随自己的车 ✓
- **Client 端**：摄像机也跟随的是 Host 的车（Client 自己的车在屏幕外）

**临时修复（运行时手动操作）**：
1. Build 连接后，在 Editor 的 Hierarchy 中找到新 spawn 出来的 `NetworkKartPlayer(Clone)`
2. 点选 `CinemachineVirtualCamera`
3. 在 Inspector 中把 **Follow** 和 **Look At** 拖到 `NetworkKartPlayer(Clone)` 的子物体 `KartArtGroup` 上

> 永久修复方案：在 `NetworkArcadeKartController.OnNetworkSpawn()` 中，当 `IsOwner = true` 时，用代码把 CinemachineVirtualCamera 的 Follow/LookAt 设为自己。详见下方「摄像机自动跟随」章节。

---

## 阶段三：弱网模拟

验证 InterpolationSystem 和外推逻辑在丢包/高延迟下的表现。

1. 点选 Hierarchy 中的 `NetworkManager`，展开子组件 `UnityTransport`
2. Inspector 中找到 **Simulator Pipeline** 区域（需要先勾选 **Use Network Simulator** 复选框）
3. 设置参数：

| 参数 | 推荐值 | 说明 |
|------|--------|------|
| Packet Delay | `100` ms | 模拟 100ms 单向延迟（相当于 200ms RTT） |
| Packet Jitter | `20` ms | 随机抖动 |
| Packet Drop Rate | `5` % | 丢包率 |

4. 重新测试双开，观察：
   - 远端车移动是否仍然平滑（插值有效：yes → InterpolationSystem 正常）
   - 丢包后远端车是否短暂继续沿原方向滑行（外推有效：yes → `Extrapolate()` 触发）
   - 丢包严重时是否停止外推（`MaxExtrapolateTime = 0.5s` 达到上限）

---

## 验证检查表

| 测试项 | 预期现象 | 对应代码 |
|--------|----------|----------|
| Host 启动 | Console：`已启动 Host，等待玩家加入...` | `LobbyManager.StartHost()` |
| Client 加入 | 双端均出现 2 辆车 | `NetworkManager` Player Prefab spawn |
| 远端车平滑移动 | 无跳帧，曲线路径 | `InterpolationSystem.Update()` |
| 弱网外推 | 短暂 500ms 滑行后停止 | `InterpolationSystem.Extrapolate()` |
| Client 断开 | Host Console：`玩家离开 (id=1)` | `LobbyManager.OnClientDisconnected` |

---

## 摄像机自动跟随（可选改进）

在 `NetworkArcadeKartController.cs` 的 `OnNetworkSpawn()` 中加入以下代码，让 Owner 的 spawn 自动接管摄像机：

```csharp
public override void OnNetworkSpawn()
{
    // ... 现有代码 ...

    if (IsOwner)
    {
        // 自动让摄像机跟随本地玩家的车
        var vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            // KartArtGroup 是车的视觉根节点（Cinemachine 跟随目标）
            var followTarget = transform.Find("KartArtGroup") ?? transform;
            vcam.Follow   = followTarget;
            vcam.LookAt   = followTarget;
        }
    }
}
```

> 需要 `using Cinemachine;` 和在 asmdef 中加引用 `com.unity.cinemachine`。

---

## 常见问题

**Q：Build 报错"找不到 NetworkManager"**  
A：确认 `PhysicsPlayground` 已加入 Build Settings，且 NetworkManager 在场景中。

**Q：Client Join 后状态一直是"正在连接..."**  
A：确认 Host 已经先点了 Host，且防火墙未拦截 7777 端口（本机测试一般没问题）。

**Q：两端只看到一辆车**  
A：NetworkManager 的 Player Prefab 是否设置？`NetworkKartPlayer` prefab 上是否有 `NetworkObject` 组件？

**Q：远端车位置跳动**  
A：属于正常现象（弱网下位置纠错），如果跳动频繁可适当调大 `CorrectionThreshold`（默认 1.0f）。
