# 12 — assets

演示 `globalThis.assets`：

- `assets.loadJson(url)` 异步加载本地 JSON，结果通过 `assets.on('loadedJson', ...)` 派发；
- `assets.loadText(url)` 演示 HTTP 加载（默认请求 https://example.com/）；
- `assets.clearCache()` 清空 L1 内存 + L2 磁盘缓存；
- `assets.cacheStats()` 返回缓存统计（JSON 字符串）。

运行：

```powershell
cd demos
.\run.ps1 -Name assets
```
