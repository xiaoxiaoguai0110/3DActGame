# Scripts 目录约定

脚本按“游戏职责”分组，目录结构参考 KitchenChaos 类 Unity 项目的组织方式。脚本类名和 Unity `.meta` GUID 保持不变，因此现有场景与 Prefab 不需要重新拖拽组件。

```text
Scripts/
├── Core/              可复用的基础组件与纯 C# 工具
├── Player/            玩家控制、移动、连招与锁定
│   ├── Player.cs      玩家组件协调器（保留 Prefab 挂载点）
│   ├── Player.Movement.cs
│   ├── Player.LockOn.cs
│   ├── Player.Combat.cs
│   └── Player.Lifecycle.cs
├── Enemy/             敌人状态机与敌人行为
│   ├── Enemy.cs       敌人状态机协调器（保留 Prefab 挂载点）
│   ├── Enemy.Movement.cs
│   ├── Enemy.Combat.cs
│   └── EnemySpawner.cs NavMesh 随机生成与数量控制
├── Combat/            伤害判定、武器、攻击相关组件
├── Camera/            摄像机跟随、锁定与过渡
├── Managers/          跨场景的全局管理器
├── Input/             Input System 读取器及自动生成代码
├── ScriptableObjects/ ScriptableObject 类型定义
└── UI/                场景 UI 与 UI 事件订阅
    ├── MainMenuUI.cs           主菜单场景跳转
    ├── GameSceneBootstrap.cs   战斗场景初始化
    ├── CombatHUDController.cs  战斗 HUD 协调
    ├── HealthyUI.cs            玩家血条
    └── EnemyHealthUI.cs        最近敌人血条
```

## 依赖方向

```text
Input ───────┐
             ├──> Player ───> Combat ───> Core.Health
Camera ──────┘                    └──────> Managers.Audio
Enemy ────────────────────────────┘
UI ───────────────────────────────> Core.Health
```

新增脚本时优先按职责选择目录：

- 纯逻辑、计时器、通用组件放入 `Core`。
- 只服务玩家或敌人的行为放入对应角色目录。
- 角色脚本优先使用 `partial` 按职责拆分；只有需要独立生命周期、独立 Inspector 配置或独立测试时，才提取为新的 MonoBehaviour。
- 只负责碰撞/攻击/伤害的组件放入 `Combat`。
- 跨场景单例和全局服务放入 `Managers`。
- 资源配置类型放入 `ScriptableObjects`，实际资产放在 `Assets/ScriptableObjects`。
