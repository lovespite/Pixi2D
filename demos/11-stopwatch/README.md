秒表 start / stop / reset：演示 setInterval / clearInterval。

# 11-stopwatch

★ 依赖 Host Pump（每帧滴答 JS 事件循环）。

控件：`fancy-text × 2`、`button × 3`。
JS 用 100ms 间隔的 `setInterval` 维护计时器。

启动：

```powershell
.\demos\run.ps1 -Name stopwatch
```
