# Copilot Instructions

## General Guidelines

- Start by reading [`Documentation/maintenance/README.md`](../Documentation/maintenance/README.md) to learn the repository maintenance documentation layout, then follow the locale index it points to.
- In one work session, migrate as many modules as possible. If something is blocked, solve it yourself. Ask the user only when the problem cannot be solved.
- Do not write phrases such as "this round" or "this session" in documentation. Later readers will not know what a round means. Record facts that do not depend on conversation turns. For progress, record which modules are done and which are not; do not record what was completed in the current round.

## Migration Guidelines

- When migrating the WPF repository locally, eliminate project circular dependencies whenever possible. Keep cycle-breaker projects only when the dependency cannot be reasonably split.
- During migration, add `.csproj` files to the solution and keep the solution buildable. Do not fix a build by removing projects.
- When copying content from `origin/`, prefer a verifiable script copy. Do not read files into context and write them back.
- If the `origin/` folder is missing or empty, report the error immediately and stop all further work.

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
