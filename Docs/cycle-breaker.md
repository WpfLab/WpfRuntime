有必要先评估，而且当前证据看，cycle-breaker 仍然有必要保留，至少在这轮迁移里是这样。
理由很简单：
1.	当前不是“偶然的项目文件写错了” 现有项目文件里明确写了：
•	PresentationUI-PresentationFramework-impl-cycle.csproj
•	PresentationFramework-ReachFramework-impl-cycle.csproj
•	PresentationFramework-System.Printing-api-cycle.csproj
•	ReachFramework-System.Printing-api-cycle.csproj
这些名字已经说明它们不是临时产物，而是原始 WPF 工程里专门用于打断循环依赖的结构。
2.	这些循环里包含 API 层循环，不是只靠调整构建顺序能解决 从当前项目引用能看到：
•	PresentationFramework.csproj 引用了 ReachFramework.csproj 和 System.Printing-ref.csproj
•	ReachFramework.csproj 又引用了 System.Printing-ref.csproj，同时还依赖 PresentationFramework-ReachFramework-impl-cycle.csproj
•	System.Printing-ref.csproj 又反向引用：
•	PresentationFramework-System.Printing-api-cycle.csproj
•	ReachFramework-System.Printing-api-cycle.csproj
•	PresentationUI.csproj 直接引用 PresentationFramework.csproj 和 ReachFramework.csproj
•	PresentationFramework.csproj 又依赖 PresentationUI-PresentationFramework-impl-cycle.csproj
这说明这里不只是实现层互调，还有公开契约层互相看见的问题。
3.	本地迁移阶段应优先保证“忠实重建 + 可构建” 如果现在强行消除这些循环，通常只有几种办法：
•	挪类型
•	拆 API 到新公共程序集
•	改公开依赖边界
•	改资源/标记编译/打印相关装配边界
这些都属于架构重构，已经超出“迁移原始 WPF 结构”的范围，风险很高，也会让后续和原始仓库难以对照。
---
目前可确认的循环关系
按当前项目文件，至少有这几组：
1. PresentationFramework ↔ PresentationUI
证据：
•	PresentationUI.csproj 引用 PresentationFramework.csproj
•	PresentationFramework.csproj 引用 PresentationUI-PresentationFramework-impl-cycle.csproj
判断：
•	这是一个实现层循环，因此名字里是 impl-cycle
2. PresentationFramework ↔ ReachFramework
证据：
•	PresentationFramework.csproj 引用 ReachFramework.csproj
•	ReachFramework.csproj 引用 PresentationFramework-ReachFramework-impl-cycle.csproj
•	PresentationFramework-ref.csproj 引用 ReachFramework-ref.csproj
•	ReachFramework-ref.csproj 也引用 PresentationFramework-ReachFramework-impl-cycle.csproj
判断：
•	这是 PresentationFramework / ReachFramework 之间的双向依赖
•	既有实现层影响，也牵涉 ref 面
3. PresentationFramework ↔ System.Printing
证据：
•	PresentationFramework.csproj 引用 System.Printing-ref.csproj
•	System.Printing-ref.csproj 反向引用 PresentationFramework-System.Printing-api-cycle.csproj
判断：
•	这是API 层循环
•	名字里是 api-cycle，说明问题在公开引用面，不只是实现代码
4. ReachFramework ↔ System.Printing
证据：
•	ReachFramework.csproj 引用 System.Printing-ref.csproj
•	ReachFramework-ref.csproj 也引用 System.Printing-ref.csproj
•	System.Printing-ref.csproj 反向引用 ReachFramework-System.Printing-api-cycle.csproj
判断：
•	这也是API 层循环
---
结论
当前阶段建议是：
•	保留 cycle-breaker 设计
•	先把缺失的 cycle-breaker 项目补回，打通迁移链路
•	等仓库能稳定构建后，再单独评估是否值得做“去 cycle-breaker 化”的架构重构
什么时候可以考虑删除 cycle-breaker
只有在下面条件满足时才建议动它：
•	已经确认只是历史遗留，而不是当前公开 API 必需
•	能把共享契约抽到更底层程序集
•	不会破坏与原始 WPF 结构的对照关系
•	有完整构建验证和回归验证能力
否则，迁移阶段删它，成本通常大于收益。
如果继续，我下一步可以直接把这 4 组循环依赖整理成一份明确的项目依赖图，并标出每个 cycle-breaker 应该放在哪个目录。
