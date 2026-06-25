---
name: "git-conflict-resolver"
description: "Handles git rebase/merge conflicts safely. NEVER use --ours or --theirs blindly. ALWAYS read and manually merge conflicted files. Invoke whenever git conflict occurs during pull/rebase/merge."
---

# Git Conflict Resolver

## 血泪教训（为什么这个 skill 存在）

**发生经过：**
在 push 代码时远程有新的提交，执行 `git pull --rebase` 后产生了冲突（3 个文件）。错误地使用了 `git checkout --ours -- <files>` 来解决冲突，导致本地几百行代码改动全部被远程旧版本覆盖。

**后果：**
- `Player.cs` 和 `Enemy.cs` 中的受击/死亡/血效/震动功能全部丢失
- 需要重新恢复所有代码改动
- 用户需要重新测试所有功能

## 核心规则

### 规则 1：永远不要无脑使用 `--ours` 或 `--theirs`

```
❌ 错误：git checkout --ours -- Assets/Scripts/Player.cs  （会用远程旧版覆盖本地改动）
❌ 错误：git checkout --theirs -- Assets/Scripts/Player.cs（会用远程旧版覆盖本地改动）
✅ 正确：手动打开文件，合并两边的改动
```

`--ours` 和 `--theirs` 在 rebase 语境下和 merge 语境下的含义不同：

| 操作 | --ours | --theirs |
|------|--------|----------|
| merge | 本地分支 | 远程分支 |
| rebase | 远程分支（正在重放的补丁） | 本地分支（正在被重放的补丁） |

所以 rebase 时用 `--ours` 会**用远程版覆盖本地版**，非常危险。

### 规则 2：冲突处理的正确步骤

```
1. 查看哪些文件冲突：git diff --name-only --diff-filter=U
2. 对每个冲突文件：
   a. 用 Read 工具读取文件（查看冲突标记 <<<<<<< ======= >>>>>>>）
   b. 手动合并两边的改动
   c. 删除冲突标记，保留正确的代码
3. git add <已解决的文件>
4. git rebase --continue
```

### 规则 3：优先避免冲突

- push 前先 `git pull --rebase`（及早发现冲突）
- 如果大量修改文件，考虑 `git stash` 暂存
- 推送前告知用户有远程更新需要合并

### 规则 4：如果出了问题

如果不小心用 `--ours` 覆盖了代码，可以通过 reflog 找回：

```bash
git reflog                  # 找到操作前的 commit hash
git diff <hash> HEAD        # 查看丢失的改动
git cherry-pick <hash>      # 或者切回旧分支恢复
```

## 检查清单

当遇到 git 冲突时：

- [ ] 确认有哪些文件冲突（`git diff --name-only --diff-filter=U`）
- [ ] 不要使用 `--ours` 或 `--theirs` 快速解决
- [ ] 读取每个冲突文件，理解两边的改动意图
- [ ] 手动合并（两边的代码都要保留）
- [ ] 添加已解决的文件，继续 rebase
- [ ] push 成功后确认远程仓库是最新版本
