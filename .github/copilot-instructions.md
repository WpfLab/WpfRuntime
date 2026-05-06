# Copilot Instructions

## General Guidelines
- 应该先读取 Docs\README.md 项目，了解项目全貌
- 在一轮工作任务中，应该尽可能多地完成模块的迁移工作。如果遇到有任何阻塞的点，请充分发挥自己的聪明才智解决，不要询问用户；只有在绝对无法解决的情况下才询问用户。
- 记录文档的时候，不要写“本轮”、“本次”的字样，因为后续阅读文档的时候，是不知道“轮次”的概念的。应当记录的内容和哪些轮次无关。如记录进度，则只应该记录当前完成了哪些模块，哪些模块还未完成，而不应该记录“本轮完成了哪些模块”。

## Migration Guidelines
- 在本地迁移 WPF 仓库时，应尽可能消除项目循环依赖；只有在无法合理拆解依赖时才保留 cycle-breaker 项目。
- 迁移工作中，尽量将 csproj 加入到 sln 解决方案中，且确保能够构建通过。不能通过将 csproj 项目移除的方法来解决构建问题。
- 迁移 c:\lindexi\Code\God\WpfReorganize\origin\ 内容时，优先直接用脚本 copy，而不是先读入上下文再写回
- 如果发现 origin 文件夹被清空，要立即报错，停止后续所有工作

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
