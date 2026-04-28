# Pixi2D Demos

> 一组开箱可跑的 PXML（含 PXML+JS）演示，用 [`Pixi2D.Host`](../Pixi2D.Host) 加载。
>
> 与 [`Pixi2D.Markup/Examples/`](../Pixi2D.Markup/Examples/)（最小内置样例）解耦：
> 这里聚焦于"完整、有趣、覆盖控件矩阵"，面向终端用户。

## 一键启动

```powershell
# 列出所有 demo
.\demos\run.ps1 -List

# 模糊匹配运行（任一写法均可）
.\demos\run.ps1 -Name counter
.\demos\run.ps1 -Name 02
.\demos\run.ps1 -Name 02-counter
```

启动器会：
1. 自动 `dotnet build Pixi2D.Host`（如尚未构建）
2. 启动 `Pixi2D.Host.exe demos\<demo>\main.pxml --watch`（编辑 PXML/JS 即时热重载）

> 首次运行需要先 `git submodule update --init --recursive`（拉取 `qjs.net` 与 `D2DWindow`）。

## Demo 矩阵

| # | 名称 | 说明 | 演示控件 / 概念 | JS | 依赖 Pump |
|---|---|---|---|---|---|
| 01 | hello | 最小 PXML 静态布局 | panel + fancy-text | — | — |
| 02 | counter | 点击计数 + 开关日志 | button × 2 + switch + fancy-text；`on-click` / `on('changed', fn)` | ✓ | — |
| 03 | login | 用户名/密码校验 | text-box × 2 + button + fancy-text；TextBox `value` 读取 | ✓ | — |
| 04 | calculator | 4 函数计算器 | button × 16 + fancy-text(display)；多按钮事件分发、状态机 | ✓ | — |
| 05 | form-validation | 邮箱格式实时校验 | text-box + fancy-text；正则 + 状态文案 | ✓ | — |
| 06 | theme-toggle | 主题切换 | switch + fancy-text × N；属性写入驱动多控件 | ✓ | — |
| 07 | modal-flow | 模态确认流程 | button + modal + fancy-text；`modal.show()/hide()` | ✓ | — |
| 08 | progress-stepper | 步进进度条 | progress-bar + button × 2 + fancy-text；数值边界 | ✓ | — |
| 09 | switch-grid | 多开关聚合 | switch × 4 + number；多事件源汇总 | ✓ | — |
| 10 | dashboard | 系统状态板 | progress-bar × 4 + fancy-text + button | (可选) | — |
| 11 | **stopwatch** | 秒表 start/stop/reset | fancy-text + button × 3；`setInterval` / `clearInterval` | ✓ | **是** |
| 12 | **progress-animate** | 进度条动画 | progress-bar + button × 2；`setInterval` 平滑变化 | ✓ | **是** |

★ 标 **依赖 Pump** 的 demo 需要 `Pixi2D.Host` 每帧 pump JS 事件循环（v0.3+ 已内置）。

## 编写新 demo

1. 新建 `demos/NN-name/`
2. 放入 `main.pxml`、可选 `main.js`、`README.md`
3. README 第一行（非 `#` 标题）会被 `run.ps1 -List` 抓为描述
4. 直接 `.\demos\run.ps1 -Name name` 即可

参考已存在的 demo（推荐 02 / 11）作为模板。详细 API：
- [`docs/scripting.md`](../docs/scripting.md) — JS 端控件 API
- [`docs/host.md`](../docs/host.md) — Host CLI / Pump / watch
- [`docs/element-mapping.md`](../docs/element-mapping.md) — PXML 元素 / 属性映射
