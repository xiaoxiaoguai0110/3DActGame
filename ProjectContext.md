# 项目上下文文档 — 3DActGame

> Unity 2022.3.46f1 | 3D 动作游戏 | 类魂风格练习项目

---

## 1. 项目结构

```
Assets/
└── Scripts/
    ├── Manager/
    │   ├── InputManager.cs      # 输入管理单例
    │   └── AudioManager.cs      # 音频管理单例
    ├── Player.cs                # 玩家移动/连招/锁定
    ├── Enemy.cs                 # 敌人 AI FSM
    ├── Health.cs                # 通用血量组件
    ├── WeaponDamage.cs          # 武器伤害检测
    ├── CameraController.cs      # 摄像机控制
    ├── AudioClipRefsSO.cs       # 音频资源 SO
    ├── UI/
    │   ├── HealthyUI.cs         # 玩家血条
    │   └── EnemyHealthUI.cs     # 敌人血条
    ├── 学习日志-工程意识培养.md
    ├── PlayerController.cs              # InputSystem 自动生成
    └── PlayerController.inputactions    # 输入绑定配置
```

---

## 2. 已完成的功能

### 2.1 输入系统 (InputReader)
- **模式**：单例（`DontDestroyOnLoad`），`Awake` 检查重复
- **轮询属性**：`MoveInput`(Vector2)、`LookInput`(Vector2)、`IsRunning`(bool)→ Update 中读取
- **事件驱动**：`OnAttack`(鼠标左键)、`OnLock`(鼠标中键)→ `performed` 时触发
- **InputSystem**：使用 `PlayerController.inputactions` 绑定配置

### 2.2 玩家控制 (Player)
- **移动**：CharacterController + 基于摄像机朝向的 WASD
  - Walk(5)、Run(10)，重力 `-9.81`
  - `Slerp` 平滑转向，`m_RotationSpeed = 10`
- **锁定移动**：方向以锁定目标为基准（前后=靠近/远离，左右=横移）
- **状态枚举**：`Idle` / `Walk` / `Run`
- **锁定系统**：
  - `Physics.OverlapSphere` 查找 ±60° 前方最近敌人
  - 目标死亡自动解锁；再次按中键手动解锁
  - 锁定时面朝目标（`FaceTarget` 每帧调用）

### 2.3 连招系统（当前实现 - 预备推进模式）

使用 `m_PreparedComboStage` 机制配合 Animator 控制：

- `AdvanceComboAnimatorCondition(stage)` → 设 `ComboStage` + Trigger `OnAttack`
- `HandleAttack` 触发时：
  - `m_ComboStage == 0` → `StartCombo(1)`
  - 不在 combo 动画中 → return
  - `m_PreparedComboStage > 0` → 记录 `m_QueuedAttackAfterPrepared` + 重置 timer
  - `m_ComboStage >= m_MaxStage` → return
  - 否则 → `PrepareComboTransition(stage+1)`（提前设置 Animator 条件）
- `Update` 中检测：
  - `m_PreparedComboStage > 0` 且当前 Animator 状态匹配 `m_PreparedComboStage` → `CommitPreparedComboStage()`
  - 通过 `IsInComboAnimation()` 和 `IsInLocomotionAnimation()` 判断动画状态切换回 Blend Tree 时 → `ResetCombo()`
- **伤害检测**：`Invoke("DisableDamage", 0.5f)` 自动关闭武器碰撞

### 2.4 摄像机 (CameraController)
- **自由模式**：鼠标灵敏度 `1.0`，俯仰 `-30°~80°`
- **锁定模式**：`LerpAngle` 平滑旋转，`m_LockFollowSpeed = 5`
- 始终跟随 `m_Target.position`，`LateUpdate` 执行

### 2.5 敌人 AI (Enemy FSM)

| 状态 | 触发条件 | 行为 |
|------|----------|------|
| Idle | 初始/Patrol到达/Pursuit超出范围 | 等待 3s → PickRandomPatrolPoint → Patrol |
| Patrol | Idle结束时 | NavMeshAgent 寻路随机点，检测玩家 10m→Pursuit |
| Pursuit | 检测到玩家 | 追击，冷却倒计时，进入 AttackRange 3.5m→Attack |
| Attack | 距离≤3.5m + 冷却结束 | 随机选4种攻击，Animation Event `OnAttackHit` 扇形检测 ±60° |
| GetHit | Health.OnHealthChanged | 受击动画，1s 超时保护 → Idle |

### 2.6 血量系统 (Health)
- **事件**：`public event Action OnHealthChanged`（TakeDamage 时触发）
- **接口**：`GetCurrentHP()`、`GetMaxHP()`、`GetHPRatio()`

### 2.7 UI
- **HealthyUI**：玩家 Slider，订阅 `OnHealthChanged`
- **EnemyHealthUI**：敌人 Slider，距离 ≤20m 时显示，`Update` 每帧检测

### 2.8 音频 (AudioManager)
- 单例模式，使用 `AudioClipRefsSO` ScriptableObject
- 当前仅实现攻击音效 `PlayAttackSound()`

---

## 3. Animator 配置

### 玩家 Animator 参数

| 参数 | 类型 | 用途 |
|------|------|------|
| `Speed` | Float (0~1) | Idle=0, Walk=0.5, Run=1 |
| `ComboStage` | Int (0~5) | 当前连招段数 |
| `OnAttack` | Trigger | 触发攻击动画 |

### 敌人 Animator 参数

| 参数 | 类型 | 用途 |
|------|------|------|
| `MoveSpeed` | Float (0~1) | Idle=0, Patrol=0.5, Pursuit=1 |
| `AttackIndex` | Float (0/0.33/0.66/1) | 选4种攻击动画 |
| `OnAttack` | Trigger | 触发攻击 |
| `OnGetHit` | Trigger | 触发受击 |

### 建议的 Animator 状态机（玩家）

```
Any State ──[OnAttack + ComboStage==0]──→ combo_04_1 ──[ComboStage==2 + ExitTime]──→ combo_04_2
                                              │                                            │
                                              └──[ComboStage==0 + ExitTime=1]─→ Blend Tree  └──...→ combo_04_5 ──→ Blend Tree
```

### 敌人 Animation Event

| 事件 | 作用 |
|------|------|
| `OnAttackHit()` | 扇形检测玩家造成伤害 |
| `OnAttackEnd()` | 切回 Pursuit + 进入冷却 |
| `OnGetHitEnd()` | 切回 Idle |

---

## 4. 关键参数配置

| 参数 | 默认值 | 所属 |
|------|--------|------|
| WalkSpeed | 5 | Player |
| RunSpeed | 10 | Player |
| RotationSpeed | 10 | Player |
| ComboWindowDuration | 1.5 | Player |
| ComboMaxStage | 5 | Player |
| ComboDamages | [10,15,20,25,30] | Player |
| LockOnRange | 15 | Player |
| LockOnAngle | 60 | Player |
| MouseSensitivity | 1 | Camera |
| LockFollowSpeed | 5 | Camera |
| DetectionRange | 10 | Enemy |
| AbandonRange | 20 | Enemy |
| AttackRange | 3.5 | Enemy |
| AttackDamage | 20 | Enemy |
| AttackCooldown | 3 | Enemy |
| PatrolRadius | 10 | Enemy |
| IdleDuration | 3 | Enemy |
| DisplayRange | 20 | EnemyHealthUI |

---

## 5. 当前遇到的问题

### 5.1 连招推进时机不匹配
- **现象**：代码 `PrepareComboTransition（stage+1）` 设置了 Animator 条件后，Animator 无法正确中断当前动画进入下一段
- **根因**：段间 Transition 用 Exit Time 控制，但代码实时推进 `ComboStage` 时 Animator 的 Transition 条件匹配逻辑冲突
- **调试手段**：`IsInComboAnimation()` 检测当前 Animator 状态名称（`combo_04_N 0`），`GetComboStage()` 匹配状态名到段数

### 5.2 锁定期间攻击面向
- **需求**：锁定后攻击时玩家面朝目标
- **实现**：`Update` 中连招阶段每帧调用 `FaceTarget()`

---

## 6. 待办事项 / 未完成的工作

1. **连招系统** — 当前的 `m_PreparedComboStage` + `CommitPreparedComboStage` 机制存在推进不稳定的问题，需要重新设计
2. **敌人死亡** — `EnemyState.Dead` 已定义但未实现死亡动画和逻辑
3. **音效多段播放** — `AudioManager` 目前只播放攻击音效，需要支持同时多音效
4. **怪物受击动画 Event** — 需要在受击动画上配置 Animation Event `OnGetHitEnd`
5. **Readme 更新** — 每次功能变更后同步更新 README.md
6. **Git 推送** — 定期推送，提交信息使用中文描述具体变更

---

## 7. UI 设计规范

- 使用 Unity uGUI Slider 作为血条
- 血条更新使用事件驱动（`Health.OnHealthChanged`），避免每帧轮询
- 敌人血条：距离 ≤20m 时显示，`Update()` 中每帧检测距离（轮询不可避免）
- 玩家血条挂载在 Canvas 下，命名 `healthSlider`
- 玩家 Tag 设为 `"Player"`，敌人 Tag 设为 `"Enemy"`

---

## 8. 关键代码模式

### 单例模式
```csharp
public static ClassName Instance { get; private set; }
void Awake() {
    if (Instance != null) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);
}
```

### 事件驱动
```csharp
public event Action OnHealthChanged;  // 声明
OnHealthChanged?.Invoke();            // 触发
m_Health.OnHealthChanged += Handler;  // 订阅
m_Health.OnHealthChanged -= Handler;  // 取消
```

### FSM 状态切换
```csharp
switch (m_CurrentState) {
    case EnemyState.Idle: UpdateIdle(); break;
    case EnemyState.Patrol: UpdatePatrol(); break;
    // ...
}
```

---

## 9. Git 提交规范

- 提交信息用中文，描述具体变更内容
- 推送前更新 README.md
- 示例：`修复敌人受击状态卡死问题，增加 GetHit 超时保护`
