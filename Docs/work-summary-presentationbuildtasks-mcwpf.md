# 工作总结 - PresentationBuildTasks 和 mcwpf 迁移

## 完成时间

2024年12月

## 工作目标

完成 `PresentationBuildTasks` 和 `mcwpf` 两个项目的迁移工作,使其能够在当前解决方案中成功构建,并消除对外部构建系统的依赖。

## 完成的工作

### 1. PresentationBuildTasks 项目迁移

#### 问题诊断

- **原始问题**: 项目目标框架为 `net472;net9.0`,但当前 `global.json` 固定 SDK 版本为 `8.0.206`
- **错误代码**: `NETSDK1045` - 当前 .NET SDK 不支持面向 .NET 9.0
- **影响**: 项目无法构建,阻塞解决方案纳管

#### 解决方案

- 将 `PresentationBuildTasks.csproj` 中的 `<TargetFrameworks>` 从 `net472;net9.0` 修改为 `net472;net8.0`
- 匹配当前 SDK 版本 `8.0.206`

#### 验证结果

- **独立构建**: ✅ 成功
  ```
  msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\PresentationBuildTasks\PresentationBuildTasks.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /clp:ErrorsOnly
  ```
- **解决方案构建**: ✅ 成功
- **状态**: 已加入 `Microsoft.Dotnet.Wpf.sln`

### 2. mcwpf 项目现代化

#### 问题诊断

- **原始问题**: 项目使用旧的非 SDK 风格格式,依赖内部构建系统路径
  - 导入 `$(_NTDRIVE)$(_NTROOT)\tools\Microsoft.DevDiv.Settings.targets`
  - 导入 `$(ToolsPath)\Microsoft.DevDiv.targets`
- **项目类型**: 事件跟踪代码生成工具,从 ETW manifest 生成 C# 代码
- **影响**: 在标准开发环境中无法构建

#### 解决方案

1. **项目格式现代化**
   - 从旧的 `<Project xmlns="...">` 格式改为 SDK 风格 `<Project Sdk="Microsoft.NET.Sdk">`
   - 移除所有内部构建系统导入
   - 设置目标框架为 `net8.0`

2. **配置调整**
   - 设置 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` (源代码中已有 AssemblyVersion 特性)
   - 将模板文件 `wpf_template.cs` 排除在编译之外:
     ```xml
     <Compile Remove="wpf_template.cs" />
     <None Include="wpf_template.cs">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </None>
     ```
   - 移除显式的源文件引用 (SDK 风格项目自动包含所有 .cs 文件)

3. **保留的配置**
   - `<OutputType>Exe</OutputType>`
   - `<AssemblyName>mcwpf</AssemblyName>`
   - `<RootNamespace>mcwpf</RootNamespace>`
   - `<Platforms>AnyCPU;x64;arm64</Platforms>`

#### 验证结果

- **独立构建**: ✅ 成功
  ```
  msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\Shared\Tracing\mcwpf\mcwpf.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /clp:ErrorsOnly
  ```
- **解决方案构建**: ✅ 成功
- **状态**: 已加入 `Microsoft.Dotnet.Wpf.sln`

### 3. 解决方案整体验证

- **构建命令**: `msbuild Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1`
- **结果**: ✅ 构建成功
- **新增项目数**: 2 个 (PresentationBuildTasks + mcwpf)

## 文档更新

已同步更新以下文档:

1. **Docs/00-overview.md**
   - 更新"当前解决方案已纳入的项目"列表
   - 更新"当前已存在但尚未纳入解决方案的主要项目"列表
   - 添加 PresentationBuildTasks 和 mcwpf 的详细迁移记录

2. **Docs/01-phase-plan.md**
   - 更新阶段 3 的"当前已验证状态"部分
   - 移除 PresentationBuildTasks 和 mcwpf 的阻塞记录

3. **Docs/02-next-session-handoff.md**
   - 更新"当前解决方案已纳入的项目"列表
   - 更新"当前磁盘已存在但尚未纳入解决方案的主要项目"列表
   - 添加"最近完成的工作"章节

4. **Docs/backlog.md**
   - 保持不变 (Perl 依赖移除工作继续作为后备待办)

## 关键技术决策

### 1. SDK 版本匹配策略

- **决策**: 优先匹配当前 `global.json` 固定的 SDK 版本,而不是升级 SDK
- **理由**: 
  - 保持仓库构建环境的稳定性
  - 避免引入新的依赖或不兼容性
  - 遵循"最小必要修改"原则

### 2. mcwpf 项目格式选择

- **决策**: 采用 SDK 风格项目格式,而不是保留旧格式
- **理由**:
  - SDK 风格是 .NET Core/.NET 5+ 的标准格式
  - 更简洁,更易维护
  - 移除对内部构建系统的依赖
  - 符合项目现代化目标

### 3. 模板文件处理

- **决策**: 将 `wpf_template.cs` 从编译中排除,但保留在项目中并复制到输出目录
- **理由**:
  - 该文件是代码生成模板,不应被编译
  - 运行时需要访问该模板文件
  - 保持工具的完整功能

## 遇到的问题及解决

### 问题 1: SDK 风格项目重复包含源文件

- **现象**: `NETSDK1022` 错误 - 包含了重复的"Compile"项
- **原因**: SDK 风格项目默认自动包含所有 .cs 文件,显式指定会导致重复
- **解决**: 移除 `<ItemGroup>` 中对 `CommandLineParser.cs` 和 `mcwpf.cs` 的显式引用

### 问题 2: AssemblyVersion 特性重复

- **现象**: `CS0579` 错误 - AssemblyVersion 特性重复
- **原因**: SDK 风格项目默认自动生成 AssemblyInfo,与源代码中的特性冲突
- **解决**: 设置 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`

### 问题 3: wpf_template.cs 不应被编译

- **现象**: `CS0234` 错误 - 命名空间中不存在类型 (MS.Internal)
- **原因**: 模板文件包含占位符代码,不应被编译
- **解决**: 使用 `<Compile Remove="wpf_template.cs" />` 排除编译

## 影响范围

### 直接影响

- ✅ `PresentationBuildTasks` 项目可以在当前环境中构建
- ✅ `mcwpf` 项目可以在当前环境中构建
- ✅ 两个项目已加入解决方案,可随解决方案一起构建

### 间接影响

- ✅ 解决方案纳管项目数增加,覆盖更多原始 WPF 仓库功能
- ✅ 减少对外部构建系统的依赖
- ✅ 提高项目的可移植性和可维护性

### 无影响

- ✅ 现有项目的构建状态未受影响
- ✅ 解决方案其他部分保持稳定

## 下一步建议

1. **继续处理 System.Printing C++/CLI 项目**
   - 解决 SafeMemoryHandle、PrintQueue 等类型重定义
   - 解决 System.IO.Packaging 引用缺失

2. **清理迁移妥协代码**
   - 优先恢复 `PresentationUI` 的真实标记编译生成链路，替换 XAML partial 占位
   - 收敛 `ReachFramework` / `PresentationFramework` / `PresentationUI` 的打印链路动态边界
   - 逐项评估 bridge 文件（`SafeMemoryHandle.cs`、`PrintQueueBridge.cs`、`DocumentReferenceBridge.cs` 等）是否可替换为更接近 origin 的方案
   - 消除主题项目、Ribbon、`PresentationUI`、`WindowsFormsIntegration` 对完整 `PresentationFramework` 输出的显式 HintPath

3. **为 PenImc 和 WpfGfx 接入 NuGet 二进制 DLL**
   - 按 `Docs/04-NuGet-Binary.md` 的方案操作
   - 这两个模块不再做源码迁移

## 遵循的原则

1. ✅ **最小必要修改**: 只修改必要的配置,不进行过度重构
2. ✅ **保持构建稳定**: 确保每次修改后解决方案仍可构建
3. ✅ **文档同步更新**: 修改后立即更新相关文档
4. ✅ **可验证性**: 每个修改都有明确的验证命令和结果
5. ✅ **不破坏现有功能**: 新增项目不影响已有项目的构建
6. ✅ **遵循项目约定**: 按照 .github/copilot-instructions.md 的指导原则执行

## 技术栈

- .NET SDK 8.0.206
- MSBuild
- C# 项目系统 (SDK 风格)
- Visual Studio 2026 (18.5.1)

## 文件清单

### 修改的文件

1. `src/Microsoft.DotNet.Wpf/src/PresentationBuildTasks/PresentationBuildTasks.csproj`
   - 修改: TargetFrameworks 从 net9.0 改为 net8.0

2. `src/Microsoft.DotNet.Wpf/src/Shared/Tracing/mcwpf/mcwpf.csproj`
   - 完全重写: 从旧格式改为 SDK 风格

3. `Microsoft.Dotnet.Wpf.sln`
   - 添加: PresentationBuildTasks 和 mcwpf 项目引用

4. `Docs/00-overview.md`
   - 更新项目列表和构建状态

5. `Docs/01-phase-plan.md`
   - 更新阶段验证状态

6. `Docs/02-next-session-handoff.md`
   - 更新项目列表和添加工作总结

### 创建的备份文件

1. `src/Microsoft.DotNet.Wpf/src/Shared/Tracing/mcwpf/mcwpf.csproj.old`
   - mcwpf 项目的原始版本备份

## 总结

完成了 PresentationBuildTasks 和 mcwpf 两个重要项目的迁移工作,使其能够在当前标准开发环境中成功构建。这些工作进一步完善了 WPF 仓库的重组,减少了对内部构建系统的依赖,提高了项目的可移植性。

所有修改都经过了充分的验证,解决方案保持稳定,文档已同步更新。这为后续继续处理 System.Printing 和其他待迁移项目打下了良好的基础。
