# 3DActGame

> Unity 2022.3.46f1 | 3D 动作游戏 | 类魂风格练习项目

用 Unity 练习游戏客户端工程开发的类魂动作游戏项目。

## 项目架构

### 战斗系统 (Player/WeaponDamage)

使用 `ComboStage` 机制配合 Animator 控制五段连招：

- `HandleAttack` 触发时：
  - 无连招 → 启动连招第 1 段
  - 不在连招动画中 → 忽略
  - 已有预备段数 → 记录排队攻击，重置 timer
  - 已达最大段数 → 忽略
  - 否则 → 预备下一段
- `Update` 中检测：
  - 有预备段数且当前动画匹配 → 提交预备段
  - 动画切回 Blend Tree → 重置连招
- **伤害检测**：`Invoke("DisableDamage", 0.5f)` 自动关闭武器碰撞

### 摄像机 (CameraController)
- **自由模式**：鼠标灵敏度 `1.0`，俯仰 `-30°~80°`
- **锁定模式**：`LerpAngle` 平滑旋转，`m_LockFollowSpeed = 5`
- 始终跟随目标，`LateUpdate` 执行

### 敌人 AI (Enemy FSM)

| 状态 | 触发条件 | 行为 |
|------|----------|------|
| Idle | 初始/Patrol到达/Pursuit超出范围 | 等待 3s → 随机选巡逻点 → Patrol |
| Patrol | Idle结束时 | NavMeshAgent 寻路随机点，检测玩家 10m → Pursuit |
| Pursuit | 检测到玩家 | 追击，冷却倒计时，进入 3.5m → Attack |
| Attack | 距离 ≤ 3.5m + 冷却结束 | 随机选 4 种攻击动画，Animation Event `OnAttackHit` 扇形检测 ±60° |
| GetHit | Health.OnHealthChanged 触发 | 受击动画，1s 超时保护 → Idle |

### 血量系统 (Health)
- 通用组件，挂载到 Player 和 Enemy
- `OnHealthChanged` 事件驱动受伤通知
- 接口：`GetCurrentHP()`、`GetMaxHP()`、`GetHPRatio()`

### UI
- **玩家血条**：Slider，订阅 `OnHealthChanged` 事件驱动更新
- **敌人血条**：Slider，距离 ≤ 20m 时显示，`Update` 每帧检测距离

### 音频 (AudioManager)
- 单例模式，使用 `AudioClipRefsSO` ScriptableObject
- 当前实现攻击音效 `PlayAttackSound()`

## Animator 参数

### 玩家

| 参数名 | 类型 | 用途 |
|--------|------|------|
| `Speed` | float (0~1) | Idle=0, Walk=0.5, Run=1 |
| `ComboStage` | int (0~5) | 当前连招段数 |
| `OnAttack` | Trigger | 触发攻击动画 |

### 敌人

| 参数名 | 类型 | 用途 |
|--------|------|------|
| `MoveSpeed` | float (0~1) | Idle=0, Patrol=0.5, Pursuit=1 |
| `AttackIndex` | float (0/0.33/0.66/1) | 选择 4 种攻击动画 |
| `OnAttack` | Trigger | 触发攻击动画 |
| `OnGetHit` | Trigger | 触发受击动画 |

### 敌人 Animation Event

| 事件名 | 触发时机 | 作用 |
|--------|---------|------|
| `OnAttackHit()` | 攻击动画关键帧 | 扇形检测玩家，造成伤害 |
| `OnAttackEnd()` | 攻击动画结束 | 切回 Pursuit + 进入冷却 |
| `OnGetHitEnd()` | 受击动画结束 | 切回 Idle（计时器兜底） |

## 关键参数配置

| 参数 | 默认值 | 所属 |
|------|--------|------|
| WalkSpeed | 5 | Player |
| RunSpeed | 10 | Player |
| RotationSpeed | 10 | Player |
| ComboWindowDuration | 1.5 | Player |
| ComboMaxStage | 5 | Player |
| ComboDamages | [10, 15, 20, 25, 30] | Player |
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

## 当前已知问题

1. **连招推进时机不匹配** — `PrepareComboTransition(stage+1)` 设置 Animator 条件后，Exit Time 控制的 Transition 与代码实时推进 `ComboStage` 逻辑冲突
2. **锁定期间攻击面向** — 连招阶段每帧调用 `FaceTarget()` 确保面朝目标

## 待办事项

1. 连招系统重新设计（当前预备推进机制不稳定）
2. 实现敌人死亡动画和逻辑（`EnemyState.Dead` 已定义未实现）
3. 音频系统支持多音效同时播放
4. 怪物受击动画配置 Animation Event `OnGetHitEnd`

## 状态流程

```
无输入 ──→ Idle（静止）
有输入 ──→ Walk（5 速度）
Shift + 有输入 ──→ Run（10 速度）
鼠标左键 ──→ 五段连击（攻击中锁定移动）
鼠标中键 ──→ 锁定/解锁目标（锁定后移动方式切换）
受击 ──→ GetHit（1s 后自动切回 Idle）
```

## 如何运行

1. 用 Unity 2022.3.46f1 打开项目
2. 场景中创建一个空物体 `InputSystem`，挂载 `InputReader` 脚本
3. 创建一个 `CameraPivot` 空物体，挂载 `CameraController` 脚本，将 MainCamera 设为子物体，将 Player 拖入 Target 槽位
4. Player 身上挂载 `Player` + `Animator` + `CharacterController` + `Health`
5. 玩家的剑上挂载 `WeaponDamage` + `Box Collider(Is Trigger)` + `Rigidbody(IsKinematic)`
6. 怪物身上挂载 `Enemy` + `Animator` + `NavMeshAgent` + `Collider` + `Health`，Tag 设为 `Enemy`
7. 在 Enemy 的 Inspector 中将 Player 拖入 `Player Obj` 字段
8. 配置好 Animator Controller
9. 运行游戏

## Unity 版本

2022.3.46f1
