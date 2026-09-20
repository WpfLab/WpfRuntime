# Builder clean 命令与清理边界

## 命令入口

直接运行独立清理命令：

```powershell
dotnet run --project eng/Builder/Builder.csproj -- clean
```

如果 Builder 已完成还原和构建，也可避免重复构建：

```powershell
dotnet restore eng/Builder/Builder.csproj
dotnet build eng/Builder/Builder.csproj --no-restore
dotnet run --project eng/Builder/Builder.csproj --no-build -- clean
```

仓库根解决方案入口统一为 `Microsoft.Dotnet.Wpf.slnx`。`clean` 只负责删除已知可再生成输出，不调用根解决方案、不执行 Restore/Build，也不验证项目加载或编译状态。

## 独立 clean 的清理范围

`CleanService.Run` 当前处理以下路径：

| 范围 | 行为 |
|---|---|
| `artifacts/` | 递归删除整个目录；无法删除的文件或目录会被保留 |
| `src/**/bin/`、`src/**/obj/` | 在 `src/` 树中查找并递归删除名为 `bin` 或 `obj` 的目录，包括 `src/Microsoft.DotNet.Wpf/cycle-breakers/` |
| `Demo/**/bin/`、`Demo/**/obj/` | 清理 Demo 项目的局部输出 |
| `.vs/` | 尝试递归删除 Visual Studio 缓存 |
| 仓库根 `*.log` | 只删除仓库根目录直接包含的日志文件，不递归搜索 |

Builder 自身的 `eng/Builder/bin/` 不在独立 `clean` 清单中，因此命令不会删除正在运行的 Builder、已生成的 `nupkg/` 或 `package-tests/`。其他未列出的工具缓存、用户目录和未跟踪文件也不会被清理。

## 默认构建命令的内置清理

默认 Builder 构建不会调用上述完整 `clean` 命令。`BuildService` 开始时只执行：

1. 尝试删除 `artifacts/bin/`。
2. 尝试删除 `artifacts/obj/`。
3. 尝试删除 `artifacts/` 根目录直接包含的文件。
4. 删除 `eng/Builder/bin/staging/`；后续资产收集按需重新创建其中的目录。

它会保留 `artifacts/log/` 等其他子目录，也不会扫描 `src/`、`Demo/` 或 `.vs/`。因此需要更广的已知输出清理时，应显式运行 `clean`，不能把默认构建开头的清理等同于独立命令。

## 锁定文件和进程边界

清理代码对 `UnauthorizedAccessException` 和 `IOException` 采用“跳过并继续”的策略。独立 `clean` 会在结尾输出删除和跳过数量，但该统计是尽力而为：部分文件删除失败或目录无法枚举时不会形成完整的文件级计数。只要命令本身没有未处理异常，当前 `CleanCommand` 返回 `0`，即使仍有锁定文件或目录。因此：

- `clean` 返回成功不等于所有目标都已删除。
- 必须查看 `Directories skipped (locked)`、`Files skipped (locked)` 和警告输出；零跳过也不能证明所有目标均已成功枚举和删除。
- Visual Studio、正在调试的应用、测试进程或其他持有 DLL/缓存句柄的程序都可能阻止删除。
- Windows PowerShell 或 PowerShell 启动的 WPF 进程也可能持有 `WpfDemo` 输出 DLL；仅关闭 Visual Studio 不一定解除所有锁。
- 若需要更完整的清理，应先正常停止构建、调试和相关应用，再关闭持有文件的 Visual Studio/PowerShell 进程，随后重新运行 `clean`。

不要通过结束不相关进程、强制删除来源不明的文件或扩大清理范围来掩盖锁问题。先根据错误中的路径和进程信息确认实际持有者。

## 安全约束

禁止在仓库级执行：

```powershell
git clean -xdf
```

该命令会越过 Builder 的已知输出白名单，并可能删除被忽略但不可安全重建的迁移源或本地材料。需要补充清理项时，应先确认目录可再生成，再把范围明确加入受控清理实现或使用针对该目录的安全操作。

## 清理后的验证

清理完成后，如需验证根入口，应单独运行目标配置，例如：

```powershell
msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal
```

这条命令只是示例验证入口。`clean` 不保证后续 Restore/Build 成功；锁定文件被跳过、工具链缺失、还原问题或源码错误都可能使构建失败。构建结论必须依据实际执行的入口、配置、日志和退出码，不能从清理结果外推。