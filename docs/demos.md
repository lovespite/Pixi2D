# PXML 可运行 Demo 索引

> 仓库根目录 [`demos/`](../demos) 下集中的 12 个可运行 PXML / PXML+JS 演示。
> 用 [`Pixi2D.Host`](../Pixi2D.Host) 加载渲染，配合 [`demos/run.ps1`](../demos/run.ps1) 一键启动。

## 快速开始

```powershell
git submodule update --init --recursive   # 首次拉取 qjs.net + D2DWindow
.\demos\run.ps1 -List                     # 列出全部 demo
.\demos\run.ps1 -Name counter             # 模糊匹配运行 (counter / 02 / 02-counter 都行)
```

启动器会：自动 `dotnet build Pixi2D.Host` → 启动 `Pixi2D.Host.exe demos\<demo>\main.pxml --watch`（编辑文件即时热重载）。

## Demo 矩阵

| # | 名称 | 说明 | 主要控件 | JS | 依赖 Pump |
|---|---|---|---|---|---|
| 01 | hello             | 最小 PXML 静态布局            | panel + fancy-text                | — | — |
| 02 | counter           | 点击计数 + 开关日志           | button × 2 + switch + fancy-text  | ✓ | — |
| 03 | login             | 用户名/密码校验               | text-box × 2 + button + fancy-text| ✓ | — |
| 04 | calculator        | 4 函数计算器                  | button × 16 + fancy-text          | ✓ | — |
| 05 | form-validation   | 必填 + 邮箱正则校验           | text-box × 2 + button × 2 + fancy-text | ✓ | — |
| 06 | theme-toggle      | 主题/密度切换                 | switch × 2 + fancy-text × N       | ✓ | — |
| 07 | modal-flow        | 模态确认流程                  | button + modal + fancy-text       | ✓ | — |
| 08 | progress-stepper  | 进度条步进 +/-                | progress-bar + button × 3         | ✓ | — |
| 09 | switch-grid       | 开关聚合到 number             | switch × 4 + number               | ✓ | — |
| 10 | dashboard         | 系统状态板（静态）            | progress-bar × 4 + fancy-text     | — | — |
| 11 | **stopwatch**     | 秒表 start/stop/reset         | button × 3 + fancy-text + `setInterval` / `clearInterval` | ✓ | **是** |
| 12 | **progress-animate** | 进度条动画                 | progress-bar + button × 3 + `setInterval` | ✓ | **是** |

★ "依赖 Pump" 的 demo 需要 `Pixi2D.Host` 每帧 pump JS 事件循环（v0.3+ 已内置）。

## 演示要点 / 关键 API

- **on-click 绑定**：PXML 写 `on-click="handler"`，JS 中定义同名函数即可（PxmlLoader → ScriptBootstrap 通过零反射的字符串拼接 `id.on('click', handler)` 完成绑定）。
- **事件订阅**：所有控件事件统一 `obj.on('camelCase', fn)` / `obj.off(...)`。详见 [`docs/scripting.md`](scripting.md)。
- **属性命名**：C# `PascalCase` → JS `camelCase`（`txt.value`、`bar.value`、`lblPct.content`、`sw.isOn` 等）。
- **PXML 属性命名**：kebab-case → PascalCase（`placeholder-text` → `PlaceholderText`，`background-color` → `BackgroundColor`）。
- **setTimeout / setInterval**：由 qjs.net 自带，由 Host 每帧 pump 触发回调；最小有效间隔 ≈ 渲染 fps（~16ms）。

## 如何加 demo

1. 新建 `demos/NN-name/`
2. 放 `main.pxml`、可选 `main.js`、`README.md`
3. README 第一行（非 `#` 标题）会被 `run.ps1 -List` 抓为描述
4. 直接 `.\demos\run.ps1 -Name name`

参考已有 demo（推荐 02 / 11）作模板。

## 已知限制

- 资源型控件（`sprite` / `icon-label` / `fancy-button`）尚未在 demo 范围内 —— 需 `IAssetLoader` + 真实图片资源（v0.5+）
- 真正的 DOM-like 动态节点 API（`document.getElementById` / `createElement`）暂未提供
- 高频 `setInterval(fn, <16ms)` 会被裁剪到一帧周期
