# 开发与验证

## 环境要求

- Windows 10 19041 或更高版本，x64。
- .NET SDK 10.0.400，版本由 `global.json` 锁定。
- 运行桌面应用需要 WebView2 Evergreen Runtime。
- `dotnet-ef` 版本由 `.config/dotnet-tools.json` 管理。

## 还原、构建和测试

```powershell
.\.dotnet\dotnet.exe restore BossFind.sln --locked-mode
.\.dotnet\dotnet.exe tool restore
.\.dotnet\dotnet.exe build BossFind.sln -c Release --no-restore -m:1
.\.dotnet\dotnet.exe test BossFind.sln -c Release --no-restore
```

CI 使用 Windows runner 执行相同的还原、构建和测试步骤，并上传 TRX 测试结果。格式检查当前为信息性步骤。

## 数据库迁移

```powershell
.\.dotnet\dotnet.exe tool restore
.\.dotnet\dotnet.exe ef migrations list `
  --project src\BossFind.Infrastructure `
  --startup-project src\BossFind.App
```

应用启动时会通过 `IDbContextFactory<AppDbContext>` 执行迁移。生成新迁移前先确认领域模型和 `AppDbContext` 的关系配置，再提交迁移文件及对应集成测试。

## MSIX 发布

发布前先完成普通 Release 构建和全量测试。未签名 x64 包可使用：

```powershell
.\.dotnet\dotnet.exe restore src\BossFind.App\BossFind.App.csproj -r win-x64 --locked-mode
.\.dotnet\dotnet.exe publish src\BossFind.App\BossFind.App.csproj `
  -c Release -p:Platform=x64 -r win-x64 --self-contained true `
  --no-restore -p:PublishAppxPackage=true `
  -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never `
  -p:AppxPackageSigningEnabled=false -p:AppxSymbolPackageEnabled=false
```

生成的 `AppPackages/` 和 `.msix` 已加入 `.gitignore`，发布包需要通过构建产物或 Release 附件分发。

## 本地运行边界

应用数据默认位于 `%LOCALAPPDATA%\BossFind`。岗位浏览页默认打开 BOSS 直聘，测试使用本地 fixture。网页登录态仅保存在独立 WebView2 Profile 中，应用不读取密码、Cookie 或验证码。投递和复核都由用户在平台页面手动完成。

运行 UI 冒烟检查时，在 Windows 桌面确认主窗口启动、页面导航、WebView2 初始化、岗位记录、PDF 导出和档案操作。`Start-BossFind.cmd` 每次都会还原依赖并从当前源码构建 Debug 版本，再启动应用。
