# Builder `clean` Command and Cleanup Bounds

## Command entry

Run the standalone cleanup command:

```powershell
dotnet run --project eng/Builder/Builder.csproj -- clean
```

If Builder has already been restored and built, avoid rebuilding it:

```powershell
dotnet restore eng/Builder/Builder.csproj
dotnet build eng/Builder/Builder.csproj --no-restore
dotnet run --project eng/Builder/Builder.csproj --no-build -- clean
```

The repository root solution entry is `Microsoft.Dotnet.Wpf.slnx`. `clean` only deletes known regenerable outputs. It does not invoke the root solution, does not run Restore/Build, and does not verify project load or compile status.

## Standalone `clean` scope

`CleanService.Run` currently handles these paths:

| Scope | Behavior |
|---|---|
| `artifacts/` | Recursively delete the entire directory. Files or directories that cannot be deleted are kept |
| `src/**/bin/`, `src/**/obj/` | Find and recursively delete directories named `bin` or `obj` under `src/`, including `src/Microsoft.DotNet.Wpf/cycle-breakers/` |
| `Demo/**/bin/`, `Demo/**/obj/` | Clean local Demo project outputs |
| `.vs/` | Try to recursively delete the Visual Studio cache |
| Root `*.log` | Delete only log files directly in the repository root. Do not search recursively |

Builder's own `eng/Builder/bin/` is not in the standalone `clean` list, so the command does not delete the running Builder, generated `nupkg/`, or `package-tests/`. Other unlisted tool caches, user directories, and untracked files are also not cleaned.

## Built-in cleanup of the default build command

The default Builder build does not call the full `clean` command above. At start, `BuildService` only:

1. Tries to delete `artifacts/bin/`.
2. Tries to delete `artifacts/obj/`.
3. Tries to delete files directly in the `artifacts/` root.
4. Deletes `eng/Builder/bin/staging/`. Later asset collection recreates directories there as needed.

It keeps other subdirectories such as `artifacts/log/` and does not scan `src/`, `Demo/`, or `.vs/`. For broader known-output cleanup, run `clean` explicitly. Do not treat the default-build startup cleanup as the standalone command.

## Locked files and process bounds

Cleanup code uses skip-and-continue for `UnauthorizedAccessException` and `IOException`. Standalone `clean` prints deleted and skipped counts at the end, but the counts are best-effort: failed file deletes or directories that cannot be enumerated do not produce a complete file-level count. As long as the command has no unhandled exception, `CleanCommand` currently returns `0`, even if locked files or directories remain. Therefore:

- A successful `clean` exit does not mean every target was deleted.
- Inspect `Directories skipped (locked)`, `Files skipped (locked)`, and warning output. Zero skips also do not prove that every target was successfully enumerated and deleted.
- Visual Studio, a debugging app, a test process, or any other program holding DLL/cache handles may block deletion.
- A WPF process started from Windows PowerShell or PowerShell may also hold `WpfDemo` output DLLs. Closing Visual Studio alone may not release every lock.
- For a more complete cleanup, first stop builds, debugging, and related apps normally, then close Visual Studio/PowerShell processes that hold files, then rerun `clean`.

Do not hide lock problems by killing unrelated processes, forcibly deleting unexplained files, or expanding cleanup scope. First identify the actual holder from the path and process information in the error.

## Safety constraints

Do not run this at repository scope:

```powershell
git clean -xdf
```

That command bypasses Builder's known-output allowlist and may delete ignored migration sources or local materials that cannot be rebuilt safely. When adding cleanup items, first confirm the directory is regenerable, then add the scope to a controlled cleanup implementation or use a safe operation targeted at that directory.

## Verification after cleanup

After cleanup, if the root entry must be verified, run the target configuration separately, for example:

```powershell
msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal
```

This command is only an example verification entry. `clean` does not guarantee later Restore/Build success. Skipped locked files, a missing toolchain, restore problems, or source errors can still fail the build. Build conclusions must come from the entry, configuration, logs, and exit code that actually ran. Do not infer them from cleanup results.
