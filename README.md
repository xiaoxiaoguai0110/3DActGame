# 3DActGame - 类魂游戏练习项目

用 Unity 练习游戏客户端工程开发的类魂动作游戏项目。

## 项目结构

```
Assets/Scripts/
├── InputReader.cs          # 输入管理单例（Move/Look/Run/Attack）
├── Player.cs               # 玩家控制（移动/转向/状态/五段连击）
└── CameraController.cs     # 摄像机控制（鼠标旋转/跟随）

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

### 玩家控制 (Player)
- **移动**：基于摄像机朝向的 WASD 移动（面朝移动方向）
- **状态**：Idle / Walk / Run 三状态自动切换
- **转向**：移动时平滑转向移动方向
- **五段连击**：带输入缓冲窗口的连击系统，支持连段过渡

### 摄像机 (CameraController)
- 鼠标控制水平/垂直旋转（Pivot 结构）
- 始终跟随玩家位置
- 俯仰角度限制（-30° ~ 80°）

## 状态流程

```
无输入 ──→ Idle（静止）
有输入 ──→ Walk（5 速度）
Shift + 有输入 ──→ Run（10 速度）
鼠标左键 ──→ 五段连击（攻击中锁定移动）
```

## Animator 参数

| 参数名 | 类型 | 用途 |
|--------|------|------|
| `Speed` | float (0~1) | Idle=0, Walk=0.5, Run=1 |
| `ComboStage` | int (1~5) | 当前连击段数 |
| `OnAttack` | Trigger | 触发攻击动画 |

## 如何运行

1. 用 Unity 打开项目
2. 场景中创建一个空物体 `InputSystem`，挂载 `InputReader` 脚本
3. 创建一个 `CameraPivot` 空物体，挂载 `CameraController` 脚本，
   将 MainCamera 设为子物体，将 Player 拖入 Target 槽位
4. Player 身上挂载 `Player` 脚本 + `Animator` 组件
5. 配置 Animator Controller（Blend Tree + 五段攻击状态）
6. 运行游戏

## Unity 版本

2022.3.46f1
