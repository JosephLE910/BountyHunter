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

## 模块二：赛车 AI

> 待编写

## 模块三：网络同步

> 待编写
