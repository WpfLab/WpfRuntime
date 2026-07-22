# 无人值守接手指引

该文件不是历史记录。接手后先完成安全检查，再按既定优先级连续推进，不等待用户拆分任务。

## 起手安全检查

1. 确认当前目录是仓库根目录，根入口为 [`../Microsoft.Dotnet.Wpf.slnx`](../Microsoft.Dotnet.Wpf.slnx)。
2. 确认 `origin/` 存在且非空。若为空，立即停止迁移，不执行复制、清理或构建修复。
3. 检查 Git 变更，识别并保护已有修改；不要覆盖来源不明的工作。
4. 牢记 `origin/src` 被 `origin/.gitignore` 排除，不受外层 Git 状态保护。
5. 禁止执行 `git clean -xdf`。只清理已确认可再生成且与当前验证直接相关的输出。

## 必读

1. [README.md](README.md)：文档职责、事实维护与安全规则。
2. [00-overview.md](00-overview.md)：当前状态的唯一事实源。
3. [01-phase-plan.md](01-phase-plan.md)：后续阶段与完成标准。
4. `.github/copilot-instructions.md`：仓库级实施规则。

其他专题文档只用于具体实现参考；其中的状态若与 `00-overview.md` 冲突，以 `00-overview.md` 为准。

## 首条命令

在仓库根目录先执行安全检查，不要先清理：

```powershell
if (-not (Test-Path origin) -or -not (Get-ChildItem origin -Force | Select-Object -First 1)) { throw 'origin 为空，停止迁移' }; git status --short
```

确认 `origin/` 非空并审阅 Git 变更后，再进行构建或编辑。

## 执行入口

当前构建结果、首个真实阻塞和验证边界只在 [00-overview.md](00-overview.md) 维护，不在该文件复制。完成安全检查后：

1. 读取 `00-overview.md` 的“当前构建状态”和“当前未决项”。
2. 从 [01-phase-plan.md](01-phase-plan.md) 的第一个未完成阶段开始执行。
3. 使用 `00-overview.md` 记录的当前入口、命令和错误作为起点；若错误已经变化，以重新执行得到的首个真实错误为准。
4. 保存可复验日志，状态变化后只更新 `00-overview.md`；阶段边界变化时再更新 `01-phase-plan.md`。

## 连续推进规则

- 严格按 [01-phase-plan.md](01-phase-plan.md) 的阶段顺序推进；一个阶段达到完成标准后立即进入下一个阶段。
- 阶段内出现新错误时，先更新 [00-overview.md](00-overview.md) 的当前事实，再继续处理同一阶段的首个真实阻塞。
- 不因专题文档中保留的历史线索跳过当前验证，也不把局部成功外推为完整解决方案成功。
- 局部成功后继续下一项，不以文档整理代替实施。

## 停止条件

只有出现以下情况才停止连续推进：

- `origin/` 不存在或为空。
- 下一步会覆盖无法归属、无法备份或无法隔离的现有 Git 修改。
- 已取得可复现证据，确认所需工具链、权限或外部依赖在当前环境不可用，且没有安全的本地替代路径。
- 连续修复会扩大到未经授权的仓库范围，无法遵守当前任务的文件或模块边界。

停止时记录精确命令、首个错误、已排除原因和恢复条件；不要用未验证推测替代证据。