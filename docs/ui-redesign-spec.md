# BossFind 界面重设计规格说明

> 阶段：第一阶段已落地，持续迭代中。<br>
> 目标平台：Windows 10/11 原生桌面应用，WinUI 3 + Windows App SDK + XAML。<br>
> 配套原型：`artifacts/ui-redesign-prototype.svg`（1440×900 主窗口高保真图）、`artifacts/ui-redesign-prototype.html`（离线说明页）。

---

## 0. 范围与约束

- 本次只定义界面结构、层级、状态与交互规范，不改变任何功能与数据流。
- 全部界面使用 Windows 原生控件语汇：`NavigationView`、`CommandBar`/工具栏、`InfoBar`、`ContentDialog`、`ListView`、`SplitView`、`TeachingTip`、`ProgressRing/ProgressBar`。
- 不做营销式落地页，不用大圆角卡片墙、渐变、装饰图形；信息优先，chrome 最少。
- 单一强调色：浅色 `#2563EB` / 深色 `#7AA7FF`。
- 兼顾鼠标与键盘操作；窗口从 640px 到 4K 宽屏可用；浅色/深色主题全部通过 `ThemeResource` 适配。
- 页面以 `Click` 事件处理器 + `Tag` 传参的既有模式驱动命令，不重写为 `ICommand`（见 §7 契约）。

## 1. 设计主张（frontend skill 三要素的工作台转译）

### 1.1 Visual thesis（视觉主张）

一间安静、光线充足的本地工作间：纸白表面、细线分隔、单一青绿色作为唯一"行动信号"，信息密度高但每个数字都可以被一眼读到。

转译规则：

- **材质**：Mica 窗口背景 + 纸白内容表面（`AppSurfaceBrush`），不用阴影堆叠，用 1px 细分隔线（`AppBorderBrush`）建立层级。
- **层级**：页面标题 26px SemiBold → 区块标题 18px SemiBold → 正文 14px → 辅助说明 12px 灰绿。数字是工作台的主角，指标数字 26px SemiBold。
- **颜色纪律**：青绿只用于"可行动/当前状态/关键数字"；正常态用墨色，弱化信息用 `AppMutedTextBrush`；警告/风险才用琥珀色 `#94600C`；成功/已完成用 `#287348`。
- **圆角纪律**：按钮与输入框 4px，信息区块 6px，头像/步骤序号用正圆。无胶囊、无大圆角。

### 1.2 Content plan（内容规划）

不是落地页的"hero → 卖点 → CTA"，而是操作台的三层：

1. **定位层（每页顶部）**：页面标题 + 一句"这里能做什么" + 主操作按钮（右侧）。操作员只扫标题就能理解页面。
2. **工作层（页面主体）**：本页的核心工作面——工作台的指标与下一步、浏览器的 WebView2、列表页的"列表 + 详情"、分析页的选择器与结果。
3. **上下文层（底部/侧栏）**：状态、环境信息、隐私说明、空态引导。

每页一个主要 takeaway：工作台回答"下一步做什么"，浏览器回答"这个岗位值不值得投"，投递页回答"还有几件事没跟完"。

### 1.3 Interaction thesis（交互主张）

桌面应用的"动效"是**响应与反馈**，不是表演：

1. **即时反馈**：所有命令触发后 200ms 内给出视觉反馈——`ProgressRing`（页头）、`ProgressBar`（浏览器加载）、`InfoBar`（成功/失败结果）、按钮 `IsEnabled` 状态切换。
2. **就地展开**：详情、编辑、匹配结果不跳页，用右侧 SplitView 面板、行内展开或 `ContentDialog` 就地完成；浏览器页面板宽屏内联、窄屏浮层（已有 `AdaptiveTrigger 900px`，保留并推广为全局断点规范）。
3. **键盘可达**：常用命令全部有快捷键与 `ToolTip` 标注（地址栏 `Ctrl+L`、刷新 `F5`、前进后退 `Alt+←/→`），列表支持方向键 + `Enter` 打开 + `Space` 选择，焦点框清晰可见。

## 2. 设计令牌（与 `App.xaml` 对齐）

| 令牌 | 浅色 | 深色 | 用途 |
|---|---|---|---|
| AppAccentBrush | `#176B5C` | `#4DB6A0` | 唯一强调色：主按钮、当前导航、状态标签、关键数字 |
| AppBackgroundBrush | `#F4F6F4`(D9) | `#1B1E1C`(CC) | 页面底色 |
| AppSurfaceBrush | `#FFFFFF`(F2) | `#242825`(F2) | 信息区块表面 |
| AppSurfaceRaisedBrush | `#F1F4F2` | `#333834` | 序号圆、头像底 |
| AppSurfaceInsetBrush | `#F7F8F7` | `#232725` | 只读内容区、代码/描述块 |
| AppBorderBrush | `#DCE3DF` | `#3A403C` | 细分隔线 |
| AppBorderStrongBrush | `#BCC8C1` | `#4A524D` | 输入框、强调边 |
| AppMutedTextBrush | `#53615A` | `#A3B0A8` | 辅助文字 |
| AppGoodBrush | `#287348` | `#6CC78F` | 已完成/成功 |
| AppWarningBrush | `#94600C` | `#E0B45C` | 待处理/风险提示 |

- 字体：系统默认（Segoe UI / 中文回退微软雅黑），不引入第二字体。
- 间距：页面外边距 28px；区块间距 24px；区块内部 8–12px；列表行高 ≥ 44px。
- 控件规则：Button/TextBox/ComboBox 圆角 4px、MinHeight 36px；信息区块圆角 6px、Padding 14–18px、1px `AppBorderBrush` 描边；`ToolButtonStyle` 36×36 图标按钮用于浏览器工具栏。
- 所有颜色禁止硬编码在页面，统一走 `ThemeResource`，保证深浅色切换零改动。

## 3. 应用外壳

### 3.1 自定义标题栏（48px）

- 左侧：应用图标 20px + "BossFind" SemiBold + "本地求职工作区" 11px 弱化。
- 右侧：系统最小化/最大化/关闭按钮，悬停态跟随系统；关闭按钮悬停红色。
- 标题栏整体可拖拽；双击切换最大化；`ExtendsContentIntoTitleBar = true`，Windows 11 使用 `MicaBackdrop`，Windows 10 自动回退默认背景。

### 3.2 NavigationView 信息架构

- 展开 232px / 收起 48px（`OpenPaneLength`/`CompactPaneLength` 已是该值，保留）；窗口 < 1008px 时进入 LeftCompact，< 640px 进入 LeftMinimal（汉堡按钮）。
- 分组："工作区"标签下依次：工作台、Boss 直聘浏览器、岗位记录、求职者简历、投递与复核；页脚固定"设置"。岗位分析在浏览器右侧工具区完成，`IsSettingsVisible=False`，设置是普通 FooterMenuItem，Tag=`settings`。
- 选中项：左侧 3px 强调色指示条 + 浅青绿底 + 强调色文字。
- 图标：Segoe Fluent Icons / SymbolIcon 线性符号（Home / World / Save / Contact / Send / Important / Setting），不用彩色图标。
- Header 区：当前页标题（`ShellViewModel.CurrentPageTitle`）+ "档案与岗位记录保存在此设备" + 右侧"本地模式"徽章——隐私承诺常驻可见。

### 3.3 页面公共骨架

每页自上而下：页头（26px 标题 + 副标题 + 主操作 + `ProgressRing`）→ 工作区 → 状态/上下文区。操作结果统一用页内 `InfoBar`（成功绿 / 警告琥珀 / 错误红）替代裸 `StatusMessage` 文本，3–5 秒自动收起，可被键盘 `Esc` 关闭；保留 `StatusMessage` 绑定作为 InfoBar 的文本来源，不新增 ViewModel 成员。

## 4. 界面地图

导航跳转关系（全集）：

```
工作台 ─┬─→ Boss 直聘浏览器（打开招聘浏览器 / 打开岗位 OpenPosting）
        ├─→ 岗位记录（查看全部 / 求职路径步骤）
        ├─→ 求职者简历（页头按钮 / 求职路径步骤）
        └─→ 投递与复核（下一步主按钮 / 投递跟进区）
Boss 直聘浏览器 ── 就地 ContentDialog 展示岗位分析摘要（不跳转，Provider/Summary/Suggestions）
Boss 直聘浏览器 ── 选择简历 → 面板内显示简历匹配度和打招呼语
岗位记录 ─→ Boss 直聘浏览器（"打开岗位"→ OpenPosting 回到原始页面）
投递与复核 ─→ Boss 直聘浏览器（"打开岗位"→ OpenPosting）
```

### 4.1 工作台 Dashboard

**目标**：30 秒内回答"我现在该做什么"，并提供通往各模块的最短路径。

**主要区域**：

1. 页头：标题"工作台" + 副标题 + 右侧 `ProgressRing`、"求职者简历"、"刷新"按钮。
2. 指标带（8 项：求职者简历、已确认内容、已记录岗位、待处理事项、收藏岗位、投递记录、已完成投递、流程进度）。**目标布局**为一行八列均分、列间细竖线分隔，宽度不足时自动折行（`ItemsRepeater` + `UniformGridLayout`）；当前实现为两行四列，重设计时调整为自适应折行。
3. 下一步横幅：强调色"下一步"标签 + 20px 任务标题 + 一行说明 + 右侧主按钮（文案随 `NextStepActionLabel` 动态变化）。当 `NextStepRoute == "browser"` 且 `NextStepUrl` 非空时，按钮改为 `OpenPosting(NextStepUrl)` 直达岗位。
4. 左列"求职路径"（2fr）：5 步流程，序号圆 + 标题 + 一行说明 + 右侧状态（已完成=绿 / 现在处理=强调色 / 待开始=灰）与"前往"按钮；区块标题右侧显示 `WorkflowProgress`（如 3/5）。
5. 右列"最近查看的岗位"（1fr）：最近 5 条取前若干（`RecentPostings`，标题/公司/城市 + "打开岗位"按钮）+ "查看全部"链接 + "打开招聘浏览器"按钮。
6. 底部双区："运行环境"（`ReadinessItems`：WebView2 运行时状态）与"投递跟进"（`PendingActionCount` 大数字 + 说明 + 跳转按钮）。

**关键数据**：`ProfileCount`、`ConfirmedFactCount`、`SavedJobCount`、`FavoriteJobCount`、`ApplicationCount`、`SubmittedApplicationCount`、`PendingActionCount`、`WorkflowProgress`、`NextStepTitle/Description/ActionLabel/Route/Url`、`WorkflowSteps`（5 步，`Number/Title/Description/StatusLabel/ActionLabel/Route/IsCurrent`）、`RecentPostings`、`ReadinessItems`、`EmptyJobsMessage`、`StatusMessage`。

**操作**：刷新（`F5`）、跳转五个模块、打开岗位原始链接。

**状态**：

- 加载：页头 `ProgressRing`，指标区显示骨架线（灰条），按钮禁用。
- 空：无简历且无岗位时，"下一步"指向"新建求职者简历"，最近岗位区显示 `EmptyJobsMessage` 引导 + "打开招聘浏览器"。
- 错误：刷新失败时 `StatusMessage` 变为"工作台加载失败：…"，页内 `InfoBar`（错误）+ 保留上一次数据。
- 成功：数据就绪，指标数字短促地由灰转墨色（一次性计数出现动画，≤200ms，尊重系统"关闭动画"设置）。

**键盘与缩放**：`F5` 刷新；`Tab` 顺序 = 页头按钮 → 下一步按钮 → 路径步骤 → 最近岗位。窗口 < 1100px 时指标带折为两行；< 860px 时左/右列堆叠为单列滚动。

### 4.2 Boss 直聘浏览器

**目标**：在不离开应用的前提下浏览 Boss 直聘，识别岗位、沉淀记录、辅助填写，且隐私边界清晰可见。

**主要区域**：

1. 浏览器工具栏（两行）：第一行 后退/前进/刷新·停止/缩放（−、百分比、+）/记录岗位/岗位信息面板开关；第二行 地址栏 + 外部打开按钮。窄屏时第一行横向可滚动（已有 `ScrollViewer` 横向模式，保留）。
2. 加载进度：2px `ProgressBar` 置顶（`IsLoading` 控制可见性，刷新/停止图标互斥切换 `ShowReloadIcon/ShowStopIcon`）。
3. 主区 `SplitView`：左为 WebView2，右为 380px 岗位信息面板。≥900px 内联（Inline），< 900px 浮层（Overlay，现有 `AdaptiveTrigger`），面板可一键收起。
4. 右侧面板分区（滚动）：当前网页（`PageTitle/CurrentUrl`）→ 岗位摘要（`JobTitle/Company/City`，薪资/经验/学历/福利四格、`Tags`）→ 操作区（选择 `SelectedCandidateProfile`、收藏、分析匹配度、生成打招呼语、完善 Boss 在线简历、打开投递页、记录岗位）→ 打招呼语与简历匹配度结果→ 底部环境信息（`ProfilePath`）与隐私说明。
5. 状态栏：左侧 `Status` 文本，右侧操作提示。

**关键数据**：导航状态（`CanGoBack/CanGoForward/IsLoading/ZoomFactor/ZoomText`）、页面元数据、岗位摘要、`IsSummaryReady`、`CandidateProfiles`、`ResumeChunks`（`Title/Content`）、`Matches`。

**操作**：全部见上；未识别到岗位时操作区按钮整体禁用（`IsSummaryReady`）。"岗位分析"为就地 `ContentDialog`（标题含 Provider，内容 = Summary + Suggestions），不跳转页面。

**状态**：

- 加载：顶部 `ProgressBar` + 刷新键变停止键。
- 空：岗位摘要区显示引导文案"尚未识别到岗位信息…"。
- 错误：导航失败/解析失败写入 `Status` 并在状态栏 + `InfoBar` 提示，WebView2 保留当前页。
- 成功：解析成功后摘要区一次性淡入；保存历史、加入队列等操作以 `InfoBar`（成功）确认。
- 隐私：独立 Profile 路径与"不读取密码、Cookie 或验证码"说明常驻面板底部。

**键盘与缩放**：`Ctrl+L` 聚焦地址栏、`Alt+←/→` 前进后退、`F5/Ctrl+R` 刷新（均已实现，保留并在 `ToolTip` 标注）；网页缩放与 UI 缩放互不影响；面板宽度在 900–1200px 窗口保持 380px，主浏览区最小可读宽度 480px。

### 4.3 岗位记录

**目标**：已沉淀岗位的检索、复查与再行动入口。

**主要区域**：页头（标题 + 副标题 + `ProgressRing`/`StatusMessage`）→ 左列 320px（搜索框 + 筛选下拉 + 岗位 `ListView`，行内显示标题/公司/城市+薪资）→ 右侧详情（标题公司、地点/薪资/经验/学历/技能字段格、描述、关联简历下拉 `SelectedCandidateProfile`，操作行：打开岗位 / 收藏切换 `FavoriteActionLabel` / 加入复核队列）。

**关键数据**：`Postings`、`FilteredPostings`、`SelectedPosting`、`Filters`（全部岗位/仅收藏）、`CandidateProfiles`、`HasSelectedPosting`、`EmptyMessage`。

**操作**：搜索（输入即过滤）、筛选（全部/仅收藏）、收藏切换、加入复核队列、打开原始页面（`OpenPosting` 回到浏览器）。

**状态**：空列表显示 `EmptyMessage` + 跳转引导；加载 `ProgressRing`；收藏/入队成功写 `StatusMessage` + `InfoBar`。

**键盘与缩放**：搜索框 `Ctrl+F` 聚焦；列表方向键移动、行内"打开岗位"可 `Enter`。窗口 < 960px 时左右列改为上下堆叠，列表限高 40%。

### 4.4 求职者简历

**目标**：档案与事实记录（技能、经历、教育等）的完整维护。

**主要区域**：页头（标题 + `ProgressRing` + "新建档案"主按钮）→ 左列 260px（搜索框 `SearchText` + 搜索/清除 + `SearchResultMessage` + 档案列表 `Profiles`：姓名/标题/地点）→ 右侧编辑区（基本信息表单：姓名 `Name`、职位标题 `Headline`、所在地 `Location`/电话 `Phone`、邮箱 `Email` + 保存/删除 `CanDeleteProfile`；分隔线；经历与技能：类别 `Categories`、事实内容 `FactContent`、来源 `FactSourceText`、置信度 `FactConfidence`（NumberBox 0–1）、已确认 `FactIsConfirmed` + `FactActionLabel`（添加事实/保存事实）/取消编辑 `IsEditingFact` + 事实列表 `Facts`，行内编辑/删除）。

**关键数据**：`Profiles`、`SelectedProfile`、表单字段、`Facts`、`Categories`（`CandidateFactCategory` 枚举）、置信度 0–1。

**操作**：档案 CRUD、事实 CRUD 与确认；删除档案与删除事实均弹 `ContentDialog` 二次确认（说明影响范围，如"该档案关联的匹配记录将保留"）。

**状态**：无档案时右侧显示空态引导 + "新建档案"；保存成功写 `StatusMessage` + `InfoBar`；校验失败（姓名为空等）在字段下方红字就地提示，不弹窗。

**键盘与缩放**：表单 `Tab` 顺序自上而下；`Ctrl+S` 保存当前档案；`Esc` 取消事实编辑。< 900px 堆叠，档案列表折叠为下拉选择器。

### 4.5 投递与复核

**目标**：人工投递流程的台账——记录、跟进、复核，一条不漏。

**主要区域**：页头（标题 + 副标题 + `ProgressRing`/`StatusMessage`）→ 招聘管道看板：待办、已投、已读/沟通、面试中、Offer、已结束六列；卡片展示公司、岗位、JD、投递状态和所用简历，状态由用户手动选择。

**关键数据**：`Applications`、`SelectedApplication`、`ReviewTasks`、`SelectedTask`。投递状态由用户手动更新，复核任务独立记录处理进度。

**操作**：见上；状态转换按钮按 `CanMarkSubmitted / CanWithdraw / CanStartReview / CanCompleteReview / CanCancelTask` 精确禁用。

**状态**：两个列表各自独立空态文案（`EmptyApplicationsMessage`/`EmptyTasksMessage`）；状态变更成功写 `StatusMessage` + `InfoBar` + 列表就地刷新；"撤销记录"弹 `ContentDialog` 确认。

**键盘与缩放**：列表行 `Enter` 打开岗位；< 900px 双列堆叠为单列。

### 4.6 岗位分析

**目标**：岗位与所选简历的匹配判断依据，可读、可解释。浏览器右侧工具区可直接运行同一分析。

**主要区域**：页头（标题 + 副标题 + `ProgressRing`/`StatusMessage`）→ 选择器行（岗位下拉 `Postings` 2fr + 简历下拉 `CandidateProfiles`（可选）1fr + "分析岗位"主按钮 `HasSelectedPosting`）→ "岗位信息"区（`Summary` + `Suggestions` 列表）→ "简历匹配度"区（`MatchSummary` + 命中技能 `MatchedSkills` / 待补充技能 `MissingSkills` 双列）。

**关键数据**：`Postings`、`SelectedPosting`、`CandidateProfiles`、`SelectedCandidateProfile`、`Summary`、`Suggestions`、`MatchSummary`、`MatchedSkills`、`MissingSkills`、`EmptyMessage`。

**操作**：选择岗位（必选）、选择候选人（可选）、运行分析。浏览器页的"岗位分析"按钮就地弹窗展示同一分析结果，不携带跳转；如需深入可在本页重新选择岗位。

**状态**：未选岗位时按钮禁用并显示 `EmptyMessage` 引导；分析中 `ProgressRing` + 结果区骨架；无候选人时仅显示岗位信息区；匹配完成时命中/缺失技能以双列对照呈现。

**键盘与缩放**：选择器行在 < 860px 时折为两行；结果双列 < 760px 堆叠。

### 4.7 设置

**目标**：本地数据与隐私控制，全部操作可预期、可解释。

**主要区域**：单列窄版心（MaxWidth 760）→ 标题 + 说明"管理本地数据文件。不会导出平台登录态或访问网络" → 候选人数据导出路径 `ExportPath` + "导出 JSON"按钮 + `ProgressRing` + `Status` 状态文本 → 分隔线 → "网页容器"区（说明文案 + "清理 WebView2 Profile"按钮）。

**关键数据**：`ExportPath`、`IsBusy`、`Status`。

**操作**：导出 JSON（完成后 `InfoBar` 显示文件路径，可点击打开所在文件夹）；清理 Profile 弹 `ContentDialog` 强确认（说明将移除登录态与缓存，确认按钮使用警示样式）。

**状态**：导出中禁用按钮；路径非法就地红字；清理后 `Status` 更新为"已清理，下次启动浏览器时重建"。

**键盘与缩放**：页面本身窄版心，任意窗口宽度可读。

## 5. 全局规范

### 5.1 反馈与状态控件选型

| 场景 | 控件 |
|---|---|
| 页面级加载 | 页头 `ProgressRing` |
| 网页加载 | 2px `ProgressBar` |
| 操作成功/信息 | `InfoBar` Severity=Success/Informational，自动收起 |
| 数据异常/风险提示 | `InfoBar` Severity=Warning，常驻至关闭 |
| 操作失败 | `InfoBar` Severity=Error + 状态栏文本 |
| 破坏性操作确认 | `ContentDialog`，主按钮为安全项或红色警示 |
| 新功能首次提示 | `TeachingTip`，只出现一次，记录已读 |

### 5.2 键盘

- 全局：`F5` 刷新当前页数据；`Ctrl+1..7` 直达七个导航项。
- 浏览器页：`Ctrl+L` 地址栏、`Alt+←/→`、`F5/Ctrl+R`（已实现的 `KeyboardAccelerator` 原样保留）。
- 列表页：`Ctrl+F` 聚焦搜索、`Enter` 打开选中项、`Delete` 触发删除（带确认）。
- 所有快捷键在 `ToolTip` 中标注；焦点框 2px 强调色，不被自定义样式移除。

### 5.3 窗口缩放断点

| 宽度 | 行为 |
|---|---|
| ≥ 1200px | 完整多列布局 |
| 900–1200px | 工作台指标自动折行；浏览器面板保持 380px 内联 |
| 640–900px | 左右列堆叠；浏览器面板转 Overlay；导航 LeftCompact |
| < 640px | 导航 LeftMinimal；表单单列；工具栏横向滚动 |

### 5.4 主题与可访问性

- 全部颜色走 `ThemeResource`，深浅色令牌一一对应（见 §2，与 `App.xaml` 现有 ThemeDictionaries 完全一致）。
- 文字对比度：正文 ≥ 4.5:1，弱化文字 ≥ 4.5:1（`#53615A` on `#FFFFFF` = 7.0:1，达标）。
- 尊重系统动画开关：计数/淡入动画在"关闭动画"时直接呈现终态。
- 状态不只用颜色表达：状态标签同时有文字（已完成/现在处理/待开始、草稿/待确认/已投递…）。

## 6. 不改变的业务行为与 ViewModel 绑定契约

> 重设计只动视觉结构与控件组织；以下契约必须原样保留，实现时逐项核对。

### 6.1 路由与外壳 API

| 契约 | 说明 |
|---|---|
| 路由 Tag | `dashboard` / `browser` / `jobs` / `profiles` / `applications` / `insights` / `settings`，对应七个页面类型，顺序与 NavigationView MenuItems 一致 |
| `MainWindow.Navigate(route)` | 页面跳转唯一入口；设置页 title 映射"设置" |
| `MainWindow.OpenPosting(url)` | 跳浏览器并 `BossBrowserPage.OpenUrl(url)`；工作台/岗位记录/投递页的"打开岗位"都走它 |
| `App.MainWindow` | 页面通过 `((App)Application.Current).MainWindow` 访问外壳 |
| 页面取 VM | `App.Services.GetRequiredService<TViewModel>()`，构造函数注入保持不变 |
| 命令模式 | 页面用 `Click` 处理器 + `Tag` 传参（不引入 `ICommand`）；ViewModel 为 `ObservableObject` + `[ObservableProperty]` |
| 外壳常量 | 标题栏 48px；NavigationView `OpenPaneLength=232`、`CompactPaneLength=48`、`IsBackButtonVisible=Collapsed`、`IsSettingsVisible=False` |

### 6.2 各页绑定契约（名称与类型不变）

| 页面 | ViewModel | 必须保留的绑定（新增样式不得改名/改类型） |
|---|---|---|
| 工作台 | `DashboardViewModel` | `IsBusy`、`StatusMessage`、8 个计数字符串、`WorkflowProgress`、`NextStepTitle/Description/ActionLabel/Route/Url`、`EmptyJobsMessage`、`RecentPostings`、`WorkflowSteps`、`ReadinessItems`；行模板字段 `Number/Title/Description/StatusLabel/ActionLabel/Route`（WorkflowStep）、`Name/Status`（ReadinessItem） |
| Boss 浏览器 | `BossBrowserViewModel` | `CanGoBack/CanGoForward/IsLoading/ShowReloadIcon/ShowStopIcon/ZoomText`、`PageTitle/CurrentUrl`、8 个 `Page*` 元数据 + `PageTextLength/PageLinksCount`、`JobTitle/Company/City/Salary/Experience/Education/Benefits/Tags/Description`、`IsFavorite/FavoriteActionLabel/IsSummaryReady`、`CandidateName/SelectedCandidateProfile/CandidateProfiles`、`ResumeText/ResumeStatus/ResumeChunks`、`MatchStatus/Matches/IsBusy`、`ProfilePath/Status`、`ShowSummary/ShowSummaryHint` |
| 岗位记录 | `JobsViewModel` | `IsBusy/StatusMessage/EmptyMessage`、`SearchText`、`Filters/SelectedFilter`、`Postings/FilteredPostings/SelectedPosting`、`HasSelectedPosting/FavoriteActionLabel`、`CandidateProfiles/SelectedCandidateProfile`；行内字段 `Title/Company/City/Salary/IsFavorite/Url` |
| 求职者简历 | `CandidateProfilesViewModel` | `IsBusy/StatusMessage`、`SearchText/SearchResultMessage`、`Profiles/SelectedProfile`、`Name/Headline/Location/Phone/Email`、`CanDeleteProfile`、`Categories/SelectedFactCategory`、`FactContent/FactSourceText/FactConfidence/FactIsConfirmed`、`FactActionLabel/IsEditingFact`、`Facts`；内容行 `Category/Content/SourceText` |
| 投递与复核 | `ApplicationsViewModel` | `IsBusy/StatusMessage`、`Applications/SelectedApplication/HasSelectedApplication/CanMarkSubmitted/CanWithdraw`、`ReviewTasks/SelectedTask/CanStartReview/CanCompleteReview/CanCancelTask`、`EmptyApplicationsMessage/EmptyTasksMessage`；行 `Title/Company/CandidateName/StatusLabel/UpdatedAtLabel`、`Title/Company/CreatedAtLabel/StatusLabel` |
| 岗位分析 | `InsightsViewModel` | `IsBusy/StatusMessage/EmptyMessage`、`Postings/SelectedPosting/HasSelectedPosting`、`CandidateProfiles/SelectedCandidateProfile`、`Summary/Suggestions`、`MatchSummary/MatchedSkills/MissingSkills` |
| 设置 | `SettingsViewModel` | `ExportPath/IsBusy/Status` |

### 6.3 状态机与领域约束

- `JobApplicationStatus`：Draft / ReadyForReview / Submitted / Withdrawn / Failed（标签：草稿/待确认/已投递/已撤销/失败）。
- `JobAutomationTaskStatus`：Queued / InReview / Completed / Cancelled / Failed（标签：待复核/复核中/已完成/已取消/失败）。
- 事实置信度范围 0–1（`NumberBox` 校验）；事实类别使用 `CandidateFactCategory` 枚举；删除档案级联删除事实。
- 浏览器页面 `AdaptiveTrigger MinWindowWidth=900` 作为全局断点规范的参考实现。
- PDF 导出写入 `Documents\BossFind\岗位-yyyyMMdd-HHmmss.pdf`；数据文件与日志在 `%LOCALAPPDATA%\BossFind`；WebView2 使用独立 Boss Profile。

## 7. 后续实现建议（原型之后的落地方案，按优先级）

1. **反馈层统一**：把各页的 `StatusMessage` 文本升级为共享 `InfoBar` 宿主（可放在 `MainWindow` 外壳，页面通过消息发布），改动集中在外壳与少量页头，ViewModel 不变。
2. **工作台指标带**：两行四列改为"一行八列 + 细分隔线、宽度不足自动折行"（`ItemsRepeater` + `UniformGridLayout`），纯 XAML 层改动，绑定名不变。
3. **删除确认**：档案/事实/投递撤销接入 `ContentDialog` 确认，逐个页面小步提交。
4. **快捷键补齐**：`Ctrl+1..7` 导航、列表页 `Ctrl+F`/`Enter` 在 `MainWindow` 与对应页面加 `KeyboardAccelerator`。
5. **骨架与空态组件化**：抽一个 `EmptyStateView`（图标 + 文案 + 行动按钮）供七个页面复用，避免每页手写空态文本。
6. **浏览器面板分区折叠**：右侧面板各区块改为可折叠 `Expander`，默认只展开"岗位摘要"与"操作"，降低首屏信息压力。
