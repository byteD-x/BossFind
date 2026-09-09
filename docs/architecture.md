# 架构说明

## 总体分层

BossFind 使用本地优先的分层结构。依赖方向从上层指向下层，领域层不依赖 UI、数据库或 WebView2。

| 层 | 目录 | 职责 |
| --- | --- | --- |
| Domain | `src/BossFind.Domain` | `CandidateProfile`、`CandidateFact`、事实分类等稳定业务模型 |
| Application | `src/BossFind.Application` | 档案服务、岗位导入、匹配规则、仓储和导出接口 |
| Infrastructure | `src/BossFind.Infrastructure` | EF Core/SQLite、仓储实现、JSON 导出、日志注册 |
| Platform | `src/BossFind.Platform.Boss` | WebView2 环境、独立 Profile、CDP/Playwright 封装和 HTML 解析 |
| App | `src/BossFind.App` | WinUI 页面、ViewModel、Generic Host、依赖注入和导航 |

## 数据流

1. App 层通过 ViewModel 接收页面输入。
2. ViewModel 调用 Application 服务，不直接操作 EF Core。
3. Application 服务使用仓储接口读写 `CandidateProfile` 和 `CandidateFact`。
4. Infrastructure 通过 `IDbContextFactory<AppDbContext>` 创建短生命周期上下文，并使用 SQLite 持久化。
5. Platform 层从本地 fixture 或 WebView2 DOM 得到岗位摘要，再交给 Application 导入用例。
6. 设置页通过 `ICandidateProfileExportService` 输出稳定排序的 UTF-8 JSON。

## WebView2 隔离

Boss 浏览器使用独立用户数据目录，和候选人本地数据库、应用日志分开。当前页面通过虚拟主机映射加载 `src/BossFind.App/Assets/WebView2/fixture.html`，默认路径不发起真实平台请求。CDP、Playwright 和 PDF 类型只提供封装与验证入口，真实 GUI 连通性仍需桌面环境门禁。

## 数据完整性

- `CandidateProfile` 与 `CandidateFact` 为一对多关系。
- 档案删除时级联删除事实。
- 档案和事实更新使用版本字段支持并发冲突检测。
- 事实保留来源文本、置信度和确认状态，导入的岗位事实使用固定来源标记。
- EF Core 迁移位于 `src/BossFind.Infrastructure/Persistence/Migrations`。

## 测试分层

- Domain：实体规则和输入约束。
- Application：档案服务、岗位导入、岗位匹配和状态边界。
- Infrastructure：SQLite、迁移、仓储查询和 JSON 导出。
- Platform：HTML 解析、WebView2 Profile 路径和运行时封装。
- UI：ViewModel 状态、导出反馈和 FlaUI 依赖基线。
