# 3DActGame

> Unity 2022.3.62f1 | 3D 写实动作游戏 | 类魂风格练习项目

这是一个用于客户端开发实习作品集的 Unity 3D 动作游戏项目，包含玩家连招、锁定、敌人状态机、NavMesh 随机生成、场景流程和中世纪风格 UI。

## v0.5.0 更新内容

- 按职责重构脚本目录，拆分 Player、Enemy、Input、Managers、Combat、Camera、UI 等模块。
- 将主菜单独立到 `0-GameMenu` 场景，战斗内容保留在 `1-GameScene` 场景。
- 重做写实中世纪风格主菜单和战斗 HUD，并保留原有玩家血条的视觉形式。
- 新增 `GameSceneBootstrap`，统一处理战斗场景初始化、开场动画和输入状态。
- 新增 NavMesh 随机敌人生成器：初始生成 3 只、每 6 秒补充、场上最多 5 只。
- 生成点会检查玩家安全距离、怪物间距和 NavMesh 可达路径，降低重叠及不可达问题。
- 敌人血条支持多目标场景，自动显示一定范围内最近的存活敌人。
- 修复 `NavMeshPath` 在 MonoBehaviour 字段初始化阶段创建导致的 Unity 生命周期异常。
- 修复玩家开场动画初始化时组件引用为空的问题。
- 增加菜单 UI、战斗 HUD 和敌人生成器的编辑器配置、校验与预览工具。

## 项目结构

```text
Assets/
├── Editor/                  # 场景配置、校验和 UI 预览工具
├── Prefabs/                 # Player、Enemy 等预制体
├── Scenes/
│   ├── 0-GameMenu.unity     # 独立主菜单
│   └── 1-GameScene.unity    # 战斗场景
├── ScriptableObjects/       # 实际资源配置
└── Scripts/
    ├── Core/                # 通用组件与工具
    ├── Player/              # 玩家移动、锁定、连招和生命周期
    ├── Enemy/               # 敌人状态机、战斗及随机生成
    ├── Combat/              # 武器与伤害判定
    ├── Camera/              # 摄像机跟随、锁定和过渡
    ├── Managers/            # 跨场景管理器
    ├── Input/               # Input System 读取器和配置
    ├── ScriptableObjects/   # ScriptableObject 类型
    └── UI/                  # 菜单、HUD、血条和场景启动流程
```

详细的依赖方向和新增脚本约定见 [`Assets/Scripts/README.md`](Assets/Scripts/README.md)。

## 已实现功能

### 玩家战斗

- CharacterController 移动、行走/奔跑和重力。
- 鼠标中键锁定一定角度内最近的敌人。
- 锁定状态下以敌人为中心移动并自动调整攻击朝向。
- 五段连招、不同阶段伤害和武器碰撞窗口。
- 受伤、死亡、开场动画和摄像机过渡流程。

### 敌人 AI

| 状态 | 主要行为 |
|------|----------|
| Idle | 等待后选择 NavMesh 巡逻点 |
| Patrol | 随机巡逻并检测玩家 |
| Pursuit | 追击玩家，超出范围后放弃 |
| Attack | 在攻击范围内随机选择攻击动作 |
| GetHit | 播放受击反馈，事件与计时器双重退出保护 |

`EnemySpawner` 会在场景 NavMesh 上随机寻找有效位置。默认参数：

| 参数 | 默认值 |
|------|--------|
| 初始数量 | 3 |
| 最大存活数量 | 5 |
| 补充间隔 | 6 秒 |
| 与玩家最小距离 | 15 米 |
| 怪物最小间距 | 6 米 |

### 场景与 UI

- `0-GameMenu`：中世纪暗色调主菜单，负责开始游戏和退出游戏。
- `1-GameScene`：战斗 HUD、任务目标、操作提示和游戏初始化。
- 玩家血条通过 `Health.OnHealthChanged` 事件更新。
- 敌人血条每 0.2 秒选择显示范围内最近的存活敌人，适配最多 5 个敌人。

### 摄像机与音频

- 自由视角和锁定视角，摄像机在 `LateUpdate` 中平滑跟随。
- `AudioManager` 使用 `AudioClipRefsSO` 管理音频资源。

## 主要操作

| 操作 | 按键 |
|------|------|
| 移动 | WASD |
| 奔跑 | Shift |
| 攻击/连招 | 鼠标左键 |
| 锁定/解除锁定 | 鼠标中键 |

## 如何运行

1. 使用 Unity `2022.3.62f1` 打开项目。
2. 确认 Build Settings 中包含 `0-GameMenu` 和 `1-GameScene`，且主菜单位于第一项。
3. 打开 `Assets/Scenes/0-GameMenu.unity` 并进入 Play Mode。
4. 点击 `BEGIN JOURNEY` 进入战斗场景。

项目场景和 Prefab 已配置完成，不需要手动创建 InputSystem、CameraPivot 或角色组件。

## 编辑器辅助工具

Unity 顶部菜单提供项目配置入口，可用于重新生成/修复菜单 UI、战斗 HUD、随机生成器并检查关键引用。建议修改场景后执行对应的校验命令，再保存场景。

## 当前改进方向

- 继续调整连招 Animator Transition 和打击反馈。
- 完善敌人死亡表现、掉落和对象池。
- 增加音效并发、环境音与音量设置。
- 补充可复现的演示关卡、录屏和作品集说明。

## Unity 版本

`2022.3.62f1`
