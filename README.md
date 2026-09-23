# BossFind

[![Build](https://github.com/byteD-x/BossFind/actions/workflows/build.yml/badge.svg)](https://github.com/byteD-x/BossFind/actions/workflows/build.yml)

BossFind 是一个本地优先的 Windows 求职工作台原型，使用 WinUI 3 管理候选人档案、事实记录、岗位摘要和本地数据导出。项目把招聘平台浏览器容器与本地业务数据分层，当前以固定 HTML fixture 驱动平台解析测试。

## 当前能力

- WinUI 3 导航、工作台、候选人档案和事实记录管理。
- 按姓名、职位标题和所在地搜索档案。
- 使用 AngleSharp 解析岗位详情，并将浏览记录、收藏和岗位信息保存在本地。
- 将岗位摘要导入候选人档案，并生成来源可追溯的经验/技能事实。
- 使用确定性规则计算岗位与候选人匹配结果，输出匹配技能、缺失技能和解释文本。
- 搜索和筛选岗位记录，跟进手动投递记录与人工复核队列，并查看本地岗位分析。
- 将候选人档案和事实导出为稳定排序的 UTF-8 JSON。
- 独立 WebView2 Profile、CDP/Playwright/PDF 封装、SQLite/EF Core 迁移和 Serilog 日志基线。
- Windows x64 MSIX 发布配置与 GitHub Actions 构建测试流程。

## 当前边界

Boss 浏览器默认打开 BOSS 直聘，也保留本地示例岗位入口；页面在独立 WebView2 Profile 中运行，岗位详情会按常见页面结构解析并保存到本地。投递动作仍由用户在平台页面手动确认，JD 深度解析、AI 生成和批量自动化属于后续阶段。

## 技术栈

- C# / .NET SDK 10.0.400
- WinUI 3 / Windows App SDK
- CommunityToolkit.Mvvm
- SQLite / Entity Framework Core
- WebView2 / Microsoft.Playwright
- AngleSharp / Serilog
- xUnit / Microsoft.NET.Test.Sdk / FlaUI UIA3

## 目录结构

```text
src/
  BossFind.App/             WinUI 3 应用、页面和 ViewModel
  BossFind.Domain/          领域实体与枚举
  BossFind.Application/    用例、档案服务、岗位导入和匹配规则
  BossFind.Infrastructure/ EF Core、SQLite、JSON 导出和日志
  BossFind.Platform.Boss/  WebView2、CDP、解析器和平台边界封装
tests/
  BossFind.Domain.Tests/
  BossFind.Application.Tests/
  BossFind.Infrastructure.Tests/
  BossFind.Platform.Boss.Tests/
  BossFind.Ui.Tests/
docs/                       架构、开发和贡献说明
```

## 快速开始

要求：Windows 10 19041 或更高版本、x64、.NET SDK 10.0.400，以及运行桌面应用时可用的 WebView2 Evergreen Runtime。仓库包含本地 SDK 目录时，可直接使用下面的命令；也可以将前缀替换为系统 `dotnet`。

在 Windows 下可直接双击仓库根目录的 `Start-BossFind.cmd` 启动应用。脚本会先还原依赖、构建当前源码，再启动 Debug 版本。

```powershell
.\.dotnet\dotnet.exe restore BossFind.sln --locked-mode
.\.dotnet\dotnet.exe tool restore
.\.dotnet\dotnet.exe build BossFind.sln -c Release --no-restore -m:1
.\.dotnet\dotnet.exe test BossFind.sln -c Release --no-restore
```

启动和发布说明见 [docs/development.md](docs/development.md)。架构边界见 [docs/architecture.md](docs/architecture.md)。

## 数据位置

桌面应用默认把 SQLite 数据库和日志写入 `%LOCALAPPDATA%\BossFind`，WebView2 使用独立的 Boss Profile。设置页提供候选人数据 JSON 导出和 Profile 清理入口。

## 许可证

本项目使用 [MIT License](LICENSE)。
