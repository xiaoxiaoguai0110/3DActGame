# 第三方资源依赖

本仓库公开展示原创 gameplay、Editor 工具、场景流程和 UI 代码，不重新分发第三方美术与音频源文件。
GitHub CI 只构建临时空场景验证代码可编译；完整 Windows 成品需在拥有合法资源的本机环境构建。

## 本机完整项目使用的资源

| 本机目录 | 用途 | 仓库策略 |
|---|---|---|
| `Assets/Flooded_Grounds/` | 写实场景、建筑和环境脚本 | 不提交 |
| `Assets/Knights_(Pack)/` | 玩家骑士模型与武器 | 不提交 |
| `Assets/HEROIC FANTASY CREATURES FULL PACK VOL 1/` | 敌人模型 | 不提交 |
| `Assets/Rapier_Anim_Set/` | 玩家动作来源 | 不提交源包 |
| `Assets/Combat_Whooshes_Sounds/` | 战斗挥砍音效 | 不提交 |
| `Assets/100BestEffectPack/` | 部分视觉特效 | 不提交 |

请通过资源作者或原始商店页面合法取得资源，再导入到上表对应目录。不同版本的资源可能使用不同 GUID，
此时应在 Unity 中重新绑定 Prefab/材质，不能通过复制 `.meta` 文件伪造引用。

## 打开公开源码时

- C# 脚本、Animator 配置、Animation Event、UI 流程和自动化测试可直接审查。
- 缺少上表资源时，写实角色和环境不会完整显示，这是预期的许可证边界。
- 使用完整本机项目时，执行 `Tools/3DActGame/Portfolio P0/Validate` 检查核心引用和可选依赖。
- `Packages/manifest.json` 与 `Packages/packages-lock.json` 已纳入版本控制。

## 发布检查

1. 在包含合法第三方资源的本机项目执行 P0、P1 Play Mode 测试。
2. 构建 Windows 版本并实际启动检查菜单、战斗、胜负和重试。
3. 如资源许可证允许对外发布成品，只上传编译后的 Build，不上传第三方 Unity 源资源或 `.unitypackage`。
4. 在作品集页面标注第三方资源仅用于非商业学习演示，并保留资源作者信息。
