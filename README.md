# TM-08 竞速品类游戏研究与实践

基于 Unity Karting Microgame 模板，实现三个核心模块：车辆物理与操控、赛车 AI、网络同步。

研究对象：《极品飞车：集结》（主）/ 《QQ飞车》《马里奥赛车》《火箭联盟》（对比）

---

## 快速开始

### 环境要求
- Unity **2022.3 LTS**（推荐 2022.3.62f1）
- [Karting Microgame 模板](https://github.com/zivoy/karting-microgame)

### 部署步骤

```bash
# 1. 克隆 Karting Microgame 模板
git clone https://github.com/zivoy/karting-microgame.git KartingMicrogame

# 2. 克隆本仓库
git clone <本仓库地址> BountyHunter

# 3. 同步覆盖（将 BountyHunter 中的核心脚本覆盖到模板对应目录）
bash BountyHunter/sync.sh KartingMicrogame

# 4. 用 Unity Hub 打开 KartingMicrogame，等待包解析完成后运行
```

### 仓库结构说明

```
BountyHunter/
├── Physics/    → 模块一：车辆物理（含修改后的 ArcadeKart.cs）
├── AI/         → 模块二：赛车 AI
├── Network/    → 模块三：网络同步
├── Shared/     → 公共组件
├── sync.sh     → 同步脚本（将模板改动及各模块脚本备份回仓库）
└── README.md
```

`sync.sh` 将 KartingMicrogame 中被修改的模板文件（如 `ArcadeKart.cs`）以及各模块脚本复制回本仓库，保持两侧同步。每次修改模板文件后需手动运行：

```bash
bash sync.sh "D:/Computer/KartingMicrogame"
```

---

---

## 模块一：车辆物理与操控手感

### 设计目标

任务书要求实现"既真实又有趣"的车辆动力学，核心矛盾是**物理真实性与游戏性之间的权衡**：

| 游戏 | 物理风格 | 核心设计取向 |
|------|----------|-------------|
| 《极品飞车：集结》 | 写实 | WheelCollider + Pacejka 轮胎模型，重量感强，油门控制精细 |
| 《QQ飞车》 | 夸张 | 大幅降低横向抓地力，漂移易触发，三段蓄力 Boost 强化爽快感 |
| 《马里奥赛车》 | 纯 Arcade | 几乎运动学控制，完全服务于游戏性，无真实物理 |

本实现以《极品飞车：集结》的 Pacejka 模型为理论基础，引入《QQ飞车》的漂移蓄力机制，通过 `PacejkaDriftD`、`DriftSidewaysFriction` 等可调参数在真实感与爽快感之间调节。

---

### 实现方案

修改文件：`Assets/Karting/Scripts/KartSystems/ArcadeKart.cs`

采用**注释原逻辑、新增替换代码**的方式，保留原版 Karting Microgame 作为对比基准。

#### 1. Pacejka 魔术公式轮胎模型

**原版问题：** 漂移时使用固定 `DriftGrip = 0.4f`，导致侧向力恒定，漂移"粘滞"——一旦速度下降就立即停止，没有真实轮胎的滑移特性。

**改进方案：** 内联 Pacejka Magic Formula，在漂移状态下动态计算侧向力系数：

```
F = D × sin(C × arctan(B×slip - E×(B×slip - arctan(B×slip))))
```

- **B（刚度系数）**：控制小滑移角时侧向力的增长速率
- **C（形状系数）**：控制曲线整体形状
- **D（峰值系数）**：漂移时设为较低值（0.35），使大滑移角下力自然衰减
- **E（曲率系数）**：控制峰值后的下降曲线

效果：漂移角越大，侧向力越小，车辆继续侧滑而不是突然抓地，漂移过渡更自然。

对应代码（`ArcadeKart.cs`）：

```csharp
float PacejkaLateral(float slipAngleRad, float b, float c, float d, float e)
{
    float slip  = slipAngleRad * Mathf.Rad2Deg;
    float bSlip = b * slip;
    return d * Mathf.Sin(c * Mathf.Atan(bSlip - e * (bSlip - Mathf.Atan(bSlip))));
}
```

速度旋转时以 Pacejka 系数替换原版固定 `m_CurrentGrip`：

```csharp
// 原版：
// Rigidbody.velocity = Quaternion.AngleAxis(... * m_CurrentGrip * ...) * Rigidbody.velocity;

float effectiveGrip = m_CurrentGrip;
if (IsDrifting)
{
    float slipRad = Mathf.Atan2(lateralVel, forwardVel);
    effectiveGrip = Mathf.Abs(PacejkaLateral(slipRad, PacejkaB, PacejkaC, PacejkaDriftD, PacejkaE));
}
Rigidbody.velocity = Quaternion.AngleAxis(... * effectiveGrip * ...) * Rigidbody.velocity;
```

#### 2. 降低横向摩擦力

漂移开始时降低四轮 `WheelCollider.sidewaysFriction.stiffness`，允许车辆侧滑；漂移结束时恢复。

```csharp
// 漂移开始
SetWheelSidewaysFriction(DriftSidewaysFriction); // 默认 0.2

// 漂移结束
SetWheelSidewaysFriction(m_NormalSidewaysFriction); // 恢复原值
```

#### 3. 增加特定转向响应

漂移时转向灵敏度乘以 `DriftSteerMultiplier`（默认 1.8），反打方向盘响应提升 80%，模拟赛车手在漂移中精确控制车尾的操作手感：

```csharp
// 原版：DriftControl * deltaTime
// 新版：DriftControl * DriftSteerMultiplier * deltaTime
m_DriftTurningPower = Mathf.Clamp(
    m_DriftTurningPower + (turnInput * Mathf.Clamp01(DriftControl * DriftSteerMultiplier * Time.fixedDeltaTime)),
    -driftMaxSteerValue, driftMaxSteerValue);
```

#### 4. 漂移蓄力 + 涡轮加速（参考《QQ飞车》）

**原版问题：** 漂移结束无任何奖励，且 `DriftDampening = 10` 过大，松开方向盘漂移立即停止，无法做长漂。

**改进方案：** 三段蓄力 + 涡轮加速

| 等级 | 蓄力时间 | 颜色标识 | 效果 |
|------|----------|----------|------|
| Lv.1 | ≥ 0.5s | 蓝焰 | 小 Boost |
| Lv.2 | ≥ 1.2s | 橙焰 | 中 Boost |
| Lv.3 | ≥ 2.5s | 粉焰（完美） | 最强 Boost |

涡轮加速由两部分组成：
- **瞬间冲量**：`Rigidbody.velocity += forward * (TurboSpeedBonus × t × 1.5)`，提供"向前冲"的即时手感
- **持续加速**：通过 `AddPowerup()` 注入 `TopSpeed + Acceleration` 双加成 Powerup，维持更高速度上限并缩短达到顶速的时间

```csharp
float t = DriftChargeLevel / 3f;
Rigidbody.velocity += transform.forward * (TurboSpeedBonus * t * 1.5f);
m_BoostBypassTimer = BoostBypassDuration;
AddPowerup(new StatPowerup
{
    PowerUpID = "DriftTurbo",
    MaxTime   = TurboBoostDuration * t,
    modifiers = new Stats
    {
        TopSpeed     = TurboSpeedBonus * t,
        Acceleration = TurboAccelBonus * t
    }
});
TurboTimer = TurboBoostDuration * t;
TurboLevel = DriftChargeLevel;
```

同时将 `DriftDampening` 衰减系数降至 0.3 倍，使长漂成为可能。

#### 5. 漂移触发与结束逻辑改进

**原版问题：** 松开刹车后漂移仍继续（车身需对齐速度方向才结束），与直觉不符。

**改进方案：** 增加"松键立即结束漂移"逻辑，同时新增 Space 键作为专用漂移键：

```csharp
// 新增 Space 键触发
m_DriftKeyHeld = Input.Brake || UnityEngine.Input.GetKey(KeyCode.Space);
WantsToDrift   = m_DriftKeyHeld && forwardSpeed > 0;

// 松键立即触发 Boost 并结束漂移
if (!m_DriftKeyHeld) canEndDrift = true;
```

---

### UI 反馈（DriftBoostUI.cs）

`DriftBoostUI` 挂在场景 Canvas 下，实时显示漂移状态：

| UI 元素 | 内容 | 说明 |
|---------|------|------|
| SpeedText | `xxx km/h` | 复用场景已有 Speed 文字 |
| DriftLevelText | `Lv.1` / `Lv.2` / `Lv.3 PERFECT!` | 漂移中显示，颜色随等级变化 |
| DriftChargeBar | 进度条（Filled Image）| 蓄力进度，颜色随等级变化 |
| TurboText | `TURBO!` 闪烁 | 直接读 `ArcadeKart.TurboTimer`，100% 可靠触发 |

---

### Inspector 可调参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `PacejkaB` | 8 | 轮胎刚度，越大小角度抓地越强 |
| `PacejkaDriftD` | 0.35 | 漂移峰值，越小越滑（偏《QQ飞车》） |
| `DriftSidewaysFriction` | 0.2 | 漂移时横向摩擦力 |
| `DriftSteerMultiplier` | 1.8 | 漂移转向灵敏度倍率 |
| `DriftMinSpeedPercent` | 0.2 | 触发漂移所需的最低速度百分比（相对最高速），降低可在低速弯道漂移 |
| `DriftChargeLevel1/2/3Time` | 0.5 / 1.2 / 2.5s | 三级蓄力时间阈值 |
| `TurboBoostDuration` | 2.0s | 涡轮持续时间（三级最大） |
| `TurboSpeedBonus` | 5 m/s | 涡轮速度加成（冲量 + TopSpeed Powerup） |
| `TurboAccelBonus` | 20 | 涡轮加速度加成（Powerup，缩短达顶速时间） |
| `BoostBypassDuration` | 0.4s | 涡轮冲量后的 CoastingDrag 免疫时间，防止松 W 立即抵消冲量 |

---

### 遇到的问题及修复记录

#### 问题 1：Safe Mode 编译错误 — KartInput 无法序列化
**错误：** `NetworkBehaviourILPP: Don't know how to serialize BountyHunter.Shared.KartInput`

**原因：** Netcode for GameObjects 的 ILPP 要求所有 ServerRpc 参数实现序列化接口，`KartInput` 结构体未声明。

**修复：** 为 `KartInput` 实现 `INetworkSerializeByMemcpy`。该接口适用于仅含基础类型（`float`/`bool`/`uint`）的结构体，直接按内存布局拷贝，无需手写序列化逻辑：

```csharp
public struct KartInput : INetworkSerializeByMemcpy { ... }
```

---

#### 问题 2：Unity 6 API 不兼容
**错误：** `Rigidbody.linearVelocity` 在 Unity 2022.3 中不存在

**原因：** 初始代码在 Unity 6 环境下编写，`linearVelocity` 是 Unity 6 新增属性，Unity 2022 中对应属性为 `velocity`。

**修复：** 批量替换所有 `.linearVelocity` → `.velocity`：

```bash
sed -i 's/\.linearVelocity/.velocity/g' Physics/KartController.cs Network/NetworkKartController.cs Network/ClientPrediction.cs
```

---

#### 问题 3：Assembly Definition 缺少引用
**错误：** `The type or namespace name 'Netcode' does not exist in the namespace 'Unity'`

**原因：** 脚本放在 Karting Microgame 的 `KartGame.asmdef` 管辖范围内，但该程序集未引用 `Unity.Netcode.Runtime`。我们的脚本有自己独立的 `.asmdef` 文件，同样需要显式声明依赖。

**修复：** 为各模块创建独立 `.asmdef` 并声明依赖关系：

```
BountyHunter.Shared   → Unity.Netcode.Runtime
BountyHunter.Physics  → BountyHunter.Shared, KartGame, Unity.TextMeshPro
BountyHunter.AI       → BountyHunter.Shared, BountyHunter.Physics
BountyHunter.Network  → BountyHunter.Shared, BountyHunter.Physics, Unity.Netcode.Runtime
```

---

#### 问题 4：NGO 版本与 Unity 2022 不兼容
**原因：** 最初配置 `com.unity.netcode.gameobjects: 2.4.0`，该版本要求 Unity 6。Unity 2022.3 对应的最高兼容版本为 1.x。

**修复：** `manifest.json` 中降级为 `1.12.0`。

---

#### 问题 5：漂移松键后仍持续
**原因：** 原版 `ArcadeKart` 的漂移结束条件是"车身朝向与速度方向对齐"，与按键状态无关。一旦触发漂移，即使松开刹车，只要车身还在偏转就不会结束。

**修复：** 新增漂移键状态检测，松键时强制结束漂移并触发 Boost：

```csharp
if (!m_DriftKeyHeld) canEndDrift = true;
```

---

#### 问题 6：TURBO! UI 时显时不显
**原因：** 最初通过检测速度突变（`speedDelta > 1.5f`）来判断涡轮是否激活，在高速或低等级漂移时速度差值不足阈值，导致 UI 不触发。

**修复：** 在 `ArcadeKart` 中新增 `TurboTimer` 和 `TurboLevel` 公开属性，涡轮触发时直接赋值，`DriftBoostUI` 直接读取，彻底消除猜测逻辑：

```csharp
// ArcadeKart.cs
TurboTimer = TurboBoostDuration * t;
TurboLevel = DriftChargeLevel;

// DriftBoostUI.cs
if (Kart.TurboTimer > 0f) { /* 显示闪烁 */ }
```

---

#### 问题 7：涡轮冲量被 CoastingDrag 立即抵消
**原因：** `ArcadeKart` 在未踩油门（`accelInput ≈ 0`）时会施加 `CoastingDrag` 减速力。涡轮触发时玩家往往已松开刹车键，但还未来得及按油门，导致冲量在同一帧被抵消，完全感受不到加速。

**修复：** 新增 `m_BoostBypassTimer`，涡轮触发时置为 `BoostBypassDuration`（0.4s），在此期间跳过 CoastingDrag：

```csharp
bool boostBypassing = m_BoostBypassTimer > 0f;
if (Mathf.Abs(accelInput) < k_NullInput && GroundPercent > 0.0f && !boostBypassing)
{
    // CoastingDrag 仅在未 Boost 时施加
}
```

---

#### 问题 8：涡轮冲量感不强，缺少"向前冲"的感觉
**原因：** 初始冲量系数为 `0.6f`，叠加的 Powerup 只有 `TopSpeed` 加成，加速度不变导致达到新顶速很慢，整体手感"软"。

**修复：**
1. 冲量系数从 `0.6f` 提升至 `1.5f`，即时感更强
2. Powerup 同时加入 `Acceleration = TurboAccelBonus * t`，使车辆更快达到提升后的顶速
3. 速度上限（`ClampMagnitude`）保持不变，依然通过 `TopSpeed Powerup` 自然限速，不突破物理上限

---

#### 问题 9：主场景弯道多、速度低，漂移难以触发
**原因：** 原版漂移触发阈值复用了 `MinSpeedPercentToFinishDrift = 0.5`，即需要达到最高速的 50% 才能漂移。测试场景直道多容易达速，但主场景弯道密集，车速普遍偏低，导致几乎无法触发漂移。

**修复：** 新增独立参数 `DriftMinSpeedPercent`（默认 0.2），将触发阈值从 50% 降至 20%，在低速弯道也能正常漂移，同时保留 Inspector 可调性：

```csharp
// 原版（复用结束阈值）：
// if ((WantsToDrift || isBraking) && currentSpeed > maxSpeed * MinSpeedPercentToFinishDrift)

// 新版（独立触发阈值）：
if ((WantsToDrift || isBraking) && currentSpeed > maxSpeed * DriftMinSpeedPercent)
```

---

## 模块二：赛车 AI

> 待编写

## 模块三：网络同步与多人对战

### 核心研究问题

任务书的核心问题是：**如何在网络延迟和丢包的情况下，为所有玩家提供流畅、公平的竞速体验？**

---

### 技术选型：状态同步 vs 帧同步

| 维度 | 帧同步 | 状态同步 |
|------|--------|----------|
| 一致性 | 所有客户端结果完全相同 | 每客户端独立模拟，存在误差 |
| 碰撞精度 | 精确，适合需要精确碰撞的游戏（如《火箭联盟》） | 不一致，对方车辆位置为预测/插值结果 |
| 网络容错 | 差——一个客户端卡顿会阻塞所有人 | 好——每端独立运行，延迟只影响视觉同步 |
| 带宽 | 仅传输输入（极小），但需严格对齐 tick | 传输完整状态（较大），但可以限速 |
| 适用场景 | PC 稳定网络（《星际争霸》《火箭联盟》） | 移动端/不稳定网络（《极品飞车：集结》） |

**本实现选择状态同步**，理由与《极品飞车：集结》相同：
- 赛车游戏对碰撞精度要求低于格斗/MOBA（擦碰不影响胜负）
- 局域网/移动端网络不稳定，帧同步的"全体等最慢者"代价过高
- 状态同步配合插值，对手车辆的视觉表现已足够流畅

---

### 架构设计

```
[Host / Server]                    [Client]
  ArcadeKart (物理权威)               ArcadeKart (kinematic，不运算物理)
  NetworkArcadeKartController         NetworkArcadeKartController
    FixedUpdate → CaptureState()        OnValueChanged → ReceiveState()
    _netState.Value = state             InterpolationSystem.Update()
                                          Lerp(older, newer, t)
                                          Extrapolate(velocity * dt)
```

使用 **Unity Netcode for GameObjects 1.x**（NGO），采用**客户端权威（Owner Write）**模型：
- 每个玩家对自己的车有写权限（`NetworkVariableWritePermission.Owner`）
- 以 20Hz（`SendRate = 0.05s`）广播状态快照给所有其他玩家
- 非 Owner 玩家的 `Rigidbody` 设为 Kinematic，完全由插值系统驱动，不参与物理运算

> 注：`NetworkKartController` 实现了完整的**服务端权威 + 客户端预测 + 回滚**架构（见下文），但因其依赖自定义 `KartController` 物理，与 Karting Microgame 原版 `ArcadeKart` 不兼容，实际运行使用的是 `NetworkArcadeKartController`（客户端权威简化版）。

---

### 延迟补偿实现

#### 1. 插值（Interpolation）— `InterpolationSystem.cs`

核心思路：渲染时刻**主动落后**真实时间 `InterpolationDelay`（默认 100ms），在缓冲区的两条快照之间插值，消除因网络抖动导致的位置跳变。

```csharp
// 找最后两条快照，按 tick 差估算时间区间
float duration = (newer.Tick - older.Tick) * Time.fixedDeltaTime;
float t = Mathf.Clamp01((Time.time - renderTime) / duration);

transform.position = Vector3.Lerp(older.Position, newer.Position, t);
transform.rotation = Quaternion.Slerp(older.Rotation, newer.Rotation, t);
```

**弱网降级：外推（Extrapolation）**

当缓冲区快照不足（包丢失或延迟过大）时，切换为外推模式：用最后一条快照的速度向量推算当前位置，最多外推 `MaxExtrapolateTime`（500ms），超时则停止等待下一个快照：

```csharp
transform.position += _lastState.Velocity * dt;
```

#### 2. 客户端预测（Client Prediction）— `ClientPrediction.cs`

`NetworkKartController` 中实现的完整预测-回滚机制（服务端权威版本）：

1. **本地立即执行**：Owner 收到输入后不等服务端，直接在本地运行物理，消除操控延迟
2. **存入预测缓冲**：将每帧的输入和预测状态按 tick 存入环形缓冲区
3. **接收权威状态**：服务端广播的 `KartNetState` 到达后，比对本地预测
4. **回滚重推演（Rollback & Replay）**：若误差超过 `CorrectionThreshold`（默认 0.2m），从权威状态重新推演所有尚未确认的帧：

```csharp
float posError = Vector3.Distance(predicted.Position, next.Position);
if (posError > CorrectionThreshold)
{
    _prediction.Rollback(next);       // 将状态回退到权威快照
    _prediction.Replay(_kart, _localTick); // 重推演所有未确认输入
}
```

这套机制参考了《Replicating Chaos: Vehicle Replication in Watch Dogs 2》（GDC）的思路：车辆物理高度非线性，单纯插值不够，必须结合物理重推演才能保证纠错后运动连贯。

---

### 同步数据结构

```csharp
public struct KartNetState : INetworkSerializable
{
    public uint       Tick;       // 物理帧序号，用于插值排序和预测对齐
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Velocity;   // 用于外推
    public Vector3    AngularVel; // 用于旋转外推
}
```

每条状态包 **52 字节**（4+12+16+12+12），20Hz 下单玩家上行约 **8 KB/s**，局域网环境完全可接受。

---

### 局域网房间发现 — `LANDiscovery.cs`

不依赖中央服务器，通过 **UDP 子网广播**实现自动房间发现：

```
Host → 每 2s 向 x.x.x.255:47777 广播 "BountyHunter|<hostIP>|<gamePort>"
     → 同时向 127.0.0.1:47777 发送（确保同机测试可用）
Client → 后台线程监听 :47777 → 解析消息 → 主线程回调 OnRoomFound
```

**关键细节：**
- 使用子网定向广播（`x.x.x.255`）而非 `255.255.255.255`，穿透性更好
- Host 自身通过 `_broadcasting && hostIP == _localIP` 过滤自己的广播（仅自己作为 Host 时过滤，纯 Client 不过滤）
- 后台线程到主线程回调通过 `MainThreadDispatcher`（Queue + lock）传递，符合 Unity 单线程模型

---

### 遇到的问题及修复记录

#### 问题 1：NGO 版本与 Unity 2022 不兼容
**现象：** 配置 `com.unity.netcode.gameobjects: 2.4.0` 后项目无法打开，提示版本要求。

**原因：** NGO 2.x 要求 Unity 6，Unity 2022.3 LTS 最高兼容 1.x。

**修复：** `manifest.json` 降级为 `1.12.0`。

---

#### 问题 2：NetworkVariable 序列化报错
**错误：** `NetworkBehaviourILPP: Don't know how to serialize BountyHunter.Shared.KartInput`

**原因：** NGO 的 ILPP 要求 ServerRpc 参数实现序列化接口，普通 struct 不满足。

**修复：** 对仅含基础类型的结构体实现 `INetworkSerializeByMemcpy`，直接按内存布局拷贝，无需手写序列化：

```csharp
public struct KartInput : INetworkSerializeByMemcpy { ... }
```

---

#### 问题 3：非 Owner 玩家的车被本地物理驱动，两端位置不一致
**原因：** 非本地玩家的 `ArcadeKart` 仍持有活跃的 `Rigidbody`，本地物理引擎会持续施力，导致其位置与插值结果冲突，出现抖动和传送现象。

**修复：** `OnNetworkSpawn` 中，非 Owner 玩家的 `Rigidbody.isKinematic = true`，同时禁用 `KeyboardInput`（`BaseInput.enabled = false`），使 Transform 完全由 `InterpolationSystem` 控制：

```csharp
if (!IsOwner)
{
    var keyInput = GetComponent<BaseInput>();
    if (keyInput != null) keyInput.enabled = false;
    var rb = _kart.Rigidbody;
    if (rb != null) rb.isKinematic = true;
    _interpolation.enabled = true;
}
```

---

#### 问题 4：多人模式下车辆出生在赛道外
**原因：** NGO 的 PlayerPrefab 在服务端 Spawn 时使用 Prefab 原始位置（世界原点附近），未对接场景中的出生点。

**修复：** 在 `OnNetworkSpawn` 的 Owner 分支中，按 `OwnerClientId % 8` 查找场景内名为 `SpawnPoint_0`、`SpawnPoint_1` 等的 Transform，同时更新 `Rigidbody.position` 和 `transform`，防止物理位置与视觉位置不一致：

```csharp
rb.position = spawnPoint.position;
rb.rotation = spawnPoint.rotation;
rb.velocity = rb.angularVelocity = Vector3.zero;
transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
```

---

#### 问题 5：Host 自己能搜索到自己的房间
**原因：** Host 在监听状态时收到了自己发出的广播包（Windows 会将广播包回环给同一机器的所有 UDP socket）。

**修复：** `ParseAndNotify` 中判断：如果 `_broadcasting`（即自己是 Host），则过滤与本机 IP 相同的广播：

```csharp
if (_broadcasting && hostIP == _localIP) return;
```

---

#### 问题 6：同一台机器上 Client 搜不到 Host
**原因：** Windows 不会将广播包回环给同一机器的其他进程（只回环给同一 socket），导致 exe 中的 Client 收不到编辑器 Host 的广播。

**修复：** Host 广播时额外向 `127.0.0.1` 发送一条相同内容的单播，确保同机进程可以收到：

```csharp
// 子网广播（给局域网其他机器）
_broadcaster.Send(data, data.Length, new IPEndPoint(GetSubnetBroadcast(_localIP), DiscoveryPort));
// loopback 单播（给同机其他进程）
_broadcaster.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, DiscoveryPort));
```

---

#### 问题 7：两台电脑联机时 Client 搜不到 Host
**原因：** 两台机器连接同一 WiFi 时，路由器可能开启了 **AP 隔离（AP Isolation）**，阻止同一 WiFi 下设备间的直接通信，UDP 广播被完全屏蔽。

**排查方法：** 先用 `ping <HostIP>` 确认两机是否能互通，不通则为 AP 隔离。

**解决方法：**
1. 登录路由器管理页面，关闭「AP 隔离」/「无线隔离」
2. 两台机器均开放 UDP 47777（发现）和 TCP 7777（游戏）端口的 Windows 防火墙入站规则：
```powershell
netsh advfirewall firewall add rule name="BountyHunter UDP" protocol=UDP dir=in localport=47777 action=allow
netsh advfirewall firewall add rule name="BountyHunter TCP" protocol=TCP dir=in localport=7777 action=allow
```

---

#### 问题 8：重新打开项目进入 Safe Mode — BountyHunterSetup.cs 引用已删除字段
**错误：**
```
'GameModeSelector' does not contain a definition for 'JoinButton'
'GameModeSelector' does not contain a definition for 'IpField'
```

**原因：** `GameModeSelector.cs` 重写时删除了手动输入 IP 的 `JoinButton`、`IpField` 字段（改为 LAN 自动发现），但 Editor 工具脚本 `BountyHunterSetup.cs` 仍在为这两个字段赋值。

**修复：** 同步更新 `BountyHunterSetup.cs`，删除 `JoinButton`/`IpField` 的创建与连线代码，改为创建 `RoomListContent`（VerticalLayoutGroup 容器）并连接到 `selector.RoomListContent`。

**教训：** Editor 工具脚本与运行时组件字段必须同步修改，否则每次 Reimport 都会进入 Safe Mode，工程无法正常打开。

---

#### 问题 9：远端玩家移动卡顿、不流畅
**现象：** 联机时对方的车辆每隔一段时间跳一下，帧间几乎不动。

**原因：** `InterpolationSystem` 中插值进度 `t` 的计算有根本性错误：

```csharp
float renderTime = Time.time - InterpolationDelay;
float t = Mathf.Clamp01((Time.time - renderTime) / duration);
// 展开后：t = InterpolationDelay / duration
// 这是一个常数，不随时间变化！
```

`t` 固定不变，所以每帧位置相同，只有收到新快照时才发生跳变。

**修复：** 为每条入站快照记录本地到达时间戳（`ArrivalTime = Time.time`），插值时用 `renderTime` 在两条快照的到达时间之间做比例：

```csharp
float span = newer.ArrivalTime - older.ArrivalTime;
float t = Mathf.Clamp01((renderTime - older.ArrivalTime) / span);
// t 随 Time.time 线性增长，每帧平滑推进
```

---

#### 问题 10：同机测试时两个进程互相搜不到
**现象：** 修复插值后重新测试，同一台机器上编辑器和 exe 无法互相发现房间。

**原因：** 两个进程进入多人界面时都调用 `StartListening()`，各自尝试绑定 UDP `47777` 端口。Windows 默认不允许两个进程同时绑定同一端口，第二个进程绑定失败（`_listening` 被置为 `false`），后续 Host 发出的 loopback 单播无人接收。

**修复：** 创建 listener socket 时设置 `SO_REUSEADDR`，允许多进程共享同一端口：

```csharp
var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
socket.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
_listener = new UdpClient { Client = socket, EnableBroadcast = true };
```
