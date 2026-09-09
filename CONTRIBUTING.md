# 贡献指南

感谢参与 BossFind。提交改动前，请先阅读 README、架构说明和安全边界。

## 开发流程

1. 从 `main` 创建短生命周期分支，命名使用 `feat/`、`fix/`、`test/` 或 `docs/` 前缀。
2. 只修改与当前目标直接相关的文件，保持 Domain、Application、Infrastructure、Platform 和 App 的边界。
3. 为行为变化补充正常流程、边界和失败/取消路径测试。
4. 本地运行 Release 构建和全量测试，再提交 Pull Request。

## 本地验证

```powershell
.\.dotnet\dotnet.exe restore BossFind.sln --locked-mode
.\.dotnet\dotnet.exe build BossFind.sln -c Release --no-restore -m:1
.\.dotnet\dotnet.exe test BossFind.sln -c Release --no-restore
```

`dotnet format --verify-no-changes` 当前作为信息性检查运行；迁移文件和生成资源的行尾差异需要单独评估，请勿用格式化命令覆盖已有迁移。

## 提交与 Pull Request

- 提交消息使用中文 Conventional Commit，例如 `feat(app): 增加候选人档案搜索`、`test(parser): 补充岗位摘要边界测试`。
- Pull Request 描述包含变更摘要、验证命令、测试结果和已知限制。
- UI 改动附上页面截图或说明人工验证步骤。
- 涉及数据库模型时同时提交迁移、回滚影响和集成测试。
- 不提交 `bin/`、`obj/`、`TestResults/`、MSIX、数据库、日志、`.codex/` 或任何凭据。

## 平台边界

当前仓库使用本地 fixture 验证岗位解析。涉及 WebView2 或招聘平台的改动必须保持本地测试默认路径，不应把真实登录态、密码、Cookie、验证码或个人数据写入代码、测试 fixture、日志或提交历史。
