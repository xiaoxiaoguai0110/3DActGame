# 3DActGame

> Unity 2022.3.62f1 | 3D 动作游戏 | 类魂风格练习项目

[![Build](https://github.com/xiaoxiaoguai0110/3DActGame/actions/workflows/build.yml/badge.svg)](https://github.com/xiaoxiaoguai0110/3DActGame/actions/workflows/build.yml)

用 Unity 练习游戏客户端工程开发的类魂动作游戏项目，包含角色控制、战斗系统、敌人 AI、UI 等完整模块。

## 功能

- **角色控制** — 移动、跑步、连击、翻滚
- **战斗系统** — 武器碰撞伤害、连招组合（5 段连击）
- **敌人 AI** — 多种攻击模式（爪击、撕咬、刺击连招）、追击、受击硬直
- **摄像机** — 第三人称跟随
- **UI** — 血条（玩家/敌人）、主菜单
- **音效** — 战斗音效、背景音乐管理

## 操作

| 按键 | 动作 |
|------|------|
| WASD | 移动 |
| Shift | 跑步 |
| 鼠标左键 | 攻击 |
| 空格 | 翻滚 |

## 项目结构

```
Assets/
├── Anim/              # 动画资源（玩家 & 敌人）
├── Prefabs/           # 预制体
├── Scenes/            # 场景
│   ├── 0-GameMenu     # 主菜单
│   └── 1-GameScene    # 游戏场景
├── Scripts/           # 脚本
│   ├── Manager/       # 管理器（音效、输入）
│   ├── UI/            # UI 脚本
│   └── ...
└── PlayerController.cs  # 输入动作生成代码
```
