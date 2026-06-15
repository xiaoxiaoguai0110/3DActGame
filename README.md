# 3DActGame - 类魂游戏练习项目

用 Unity 练习游戏客户端工程开发的类魂动作游戏项目。

## 项目结构

```
Assets/Scripts/
├── Manager/
│   └── InputManager.cs       # 输入管理单例（Move/Look/Run/Attack/LockOn）
├── Player.cs                 # 玩家控制（移动/转向/状态/五段连击/锁定）
├── CameraController.cs       # 摄像机控制（鼠标旋转/锁定追踪）
├── Enemy.cs                  # 敌人 AI（Idle/Patrol/Pursuit/Attack 四状态）
├── Health.cs                 # 血量组件（受伤/当前血量/最大血量）
├── WeaponDamage.cs          # 武器伤害检测（Trigger Collider + Overlap）
└── 学习日志-工程意识培养.md   # 开发踩坑记录

Assets/
├── PlayerController.cs           # InputSystem 自动生成的代码
└── PlayerController.inputactions # 输入绑定配置
```

## 已实现的功能

### 输入系统 (InputReader)
- 单例模式，跨场景持久化
- WASD 移动输入 → `MoveInput`
- 鼠标视角输入 → `LookInput`
- Shift 奔跑 → `IsRunning`
- 攻击事件 → `OnAttack`
- 锁定事件 → `OnLock`（鼠标中键）

### 玩家控制 (Player)
- **移动**：基于摄像机朝向的 WASD 移动（面朝移动方向）
- **状态**：Idle / Walk / Run 三状态根据输入和 Shift 自动切换
- **转向**：移动时平滑转向移动方向
- **五段连击**：带输入缓冲窗口的连击系统，每段可配置独立伤害值
- **武器伤害**：剑上的 Trigger Collider + IsKinematic Rigidbody，击中敌人扣血
- **锁定系统**：鼠标中键锁定前方 ±60° 范围内最近的敌人

#### 锁定模式
- 锁定状态下，WASD 变为以锁定目标为基准：前后=靠近/远离，左右=横移
- 锁定目标死亡后自动解锁
- 再次按锁定键手动解锁

### 摄像机 (CameraController)
- **自由模式**：鼠标控制水平/垂直旋转，始终跟随玩家位置，俯仰角度限制（-30° ~ 80°）
- **锁定模式**：镜头自动旋转，保持锁定目标在画面中央

### 敌人 AI (Enemy)
- **Idle**：静止待机，3 秒后随机巡逻
- **Patrol**：NavMeshAgent 在随机范围内巡逻
- **Pursuit**：发现玩家后追击（检测范围 10m，放弃范围 20m）
- **Attack**：进入攻击范围后随机选择 4 种攻击动画之一
  - **扇形伤害检测**：距离 + 前方 ±60° 角度判定
  - **攻击冷却**：每次攻击后冷却 3 秒才能再次攻击

### 血量系统 (Health)
- 通用组件，挂载到 Player 和 Enemy 上
- 提供受伤、当前血量、最大血量、血量比例接口
- Inspector 实时显示当前血量（`[SerializeField]`）

## 状态流程

```
无输入 ──→ Idle（静止）
有输入 ──→ Walk（5 速度）
Shift + 有输入 ──→ Run（10 速度）
鼠标左键 ──→ 五段连击（攻击中锁定移动）
鼠标中键 ──→ 锁定/解锁目标（锁定后移动方式切换）
```

## Animator 参数

| 参数名 | 类型 | 用途（玩家） |
|--------|------|-------------|
| `Speed` | float (0~1) | Idle=0, Walk=0.5, Run=1 |
| `ComboStage` | int (1~5) | 当前连击段数 |
| `OnAttack` | Trigger | 触发攻击动画 |

| 参数名 | 类型 | 用途（敌人） |
|--------|------|-------------|
| `MoveSpeed` | float (0~1) | Idle=0, Patrol=0.5, Pursuit=1 |
| `AttackIndex` | float (0/0.33/0.66/1) | 选择 4 种攻击动画 |
| `OnAttack` | Trigger | 触发攻击动画 |

## 如何运行

1. 用 Unity 打开项目
2. 场景中创建一个空物体 `InputSystem`，挂载 `InputReader` 脚本
3. 创建一个 `CameraPivot` 空物体，挂载 `CameraController` 脚本，
   将 MainCamera 设为子物体，将 Player 拖入 Target 槽位
4. Player 身上挂载 `Player` 脚本 + `Animator` + `CharacterController` + `Health`
5. 玩家的剑上挂载 `WeaponDamage` + `Box Collider(Is Trigger)` + `Rigidbody(IsKinematic)`
6. 怪物身上挂载 `Enemy` + `Animator` + `NavMeshAgent` + `Collider` + `Health`，Tag 设为 `Enemy`
7. 配置好 Animator Controller
8. 运行游戏

## Unity 版本

2022.3.46f1
