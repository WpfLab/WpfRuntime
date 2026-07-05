# Builder 清理工具

## 背景

在 WPF 仓库重组过程中，经常需要在干净状态下验证 `msbuild -restore` 是否能通过。由于 Visual Studio 会锁定 `.vs/` 等目录中的文件，直接使用 `git clean -xdf` 经常失败，且需要用户手动确认。

为此，在 `eng/Builder/` 项目中新增了 `clean` 命令，用 C# 代码实现清理逻辑，可容忍锁定文件。

## 用法

```powershell
dotnet run --project eng\Builder\Builder.csproj -- clean
```

## 清理范围

`clean` 命令会删除以下内容：

| 目标 | 说明 |
|------|------|
| `artifacts/` | 整个目录递归删除（构建输出、中间产物） |
| `src/**/bin/` | `src/` 下所有 `bin/` 目录递归删除 |
| `src/**/obj/` | `src/` 下所有 `obj/` 目录递归删除 |
| `Demo/**/bin/`、`Demo/**/obj/` | Demo 目录下的构建产物 |
| `cycle-breakers/**/bin/`、`cycle-breakers/**/obj/` | cycle-breakers 目录下的构建产物 |
| `.vs/` | Visual Studio 缓存目录（可能部分锁定） |
| `*.log` | 仓库根目录下的散落日志文件 |

## 锁定文件处理

当文件被 Visual Studio 或其他进程锁定时，`clean` 命令会跳过该文件并继续清理其他内容，不会中断。清理结束后会输出统计：

```
=== Clean summary ===
  Directories deleted: 605
  Directories skipped (locked): 5
  Files deleted: 0
  Files skipped (locked): 0
[WARN] Some files/directories were locked (likely by Visual Studio).
[WARN] Close Visual Studio and re-run 'clean' for a fully clean state.
```

如需完全清理，请先关闭 Visual Studio，再重新运行 `clean` 命令。

## 实现说明

- `clean` 命令不会删除 `eng/Builder/bin/`（Builder 自身的输出目录），避免正在运行的程序自删。
- 清理逻辑通过递归遍历目录树实现，先删除子目录中的文件，再删除空目录。
- 对 `UnauthorizedAccessException` 和 `IOException` 进行捕获，将锁定文件标记为"跳过"而非失败。

## 后续 AI 对话使用指引

当需要在干净状态下验证构建时，不要使用 `git clean -xdf`（会因 VS 锁定失败），而是使用：

```powershell
dotnet run --project eng\Builder\Builder.csproj -- clean
```

清理后即可用以下命令验证干净构建：

```powershell
msbuild Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly
```
