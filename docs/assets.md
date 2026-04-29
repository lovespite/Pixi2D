# Assets 资源加载

> 适用版本：v0.7+

`Pixi2D.Host` 内置 **AssetLoader**，负责：

- 路径规范化（相对路径、`file://`、`http(s)://`）
- 分级缓存（L1 内存 LRU + L2 磁盘 tmp）
- 并发去重（同一 URL 进行中的请求只执行一次）
- 网络/进度事件（供调试器采集）

C# 入口位于 `Pixi2D.Host/Assets/`，脚本入口为 `globalThis.assets`（详见 [scripting.md](scripting.md#assets-代理)）。

---

## 路径规范化

| 输入                              | 解析为                                                       |
|-----------------------------------|--------------------------------------------------------------|
| `sample.json`                     | `file:///<PXML 所在目录>/sample.json`                        |
| `./sub/a.txt`                     | `file:///<PXML 所在目录>/sub/a.txt`                          |
| `C:\data\foo.txt`                 | `file:///C:/data/foo.txt`                                    |
| `file:///C:/data/foo.txt`         | 原样                                                         |
| `https://example.com/x.json`      | 原样                                                         |

> 相对路径基底：当前 PXML 文件所在目录（由 `Path.GetDirectoryName(_pxmlPath)` 决定）。

---

## 分级缓存

```
┌──────────────┐    miss   ┌──────────────┐    miss   ┌────────────────┐
│  L1 LRU mem  ├──────────▶│  L2 disk tmp ├──────────▶│ provider fetch │
│  <1MB only   │           │  hash + meta │           │  (file / http) │
└──────────────┘           └──────────────┘           └────────────────┘
```

| 项                  | 默认值                                                     |
|---------------------|------------------------------------------------------------|
| `MemoryThresholdBytes` | **1 MiB**（超出不入 L1）                                |
| `MemoryMaxEntries`     | 256 条                                                  |
| `MemoryMaxBytes`       | 32 MiB                                                  |
| `DiskCacheDir`         | `%TEMP%\Pixi2D\AssetCache`                              |
| `DiskMaxBytes`         | 512 MiB（超出按 LRU 淘汰最老 `.bin` + `.meta`）         |
| `HttpTimeout`          | 30s                                                     |
| `HttpUserAgent`        | `Pixi2D-Host/0.7`                                       |

### L1 行为

- **小 HTTP 资源**：完整字节驻留内存（key = URL）。
- **本地文件**：内存中**只记录入口**（key = `file://...` → DiskPath），命中时按需 `File.ReadAllBytes`。这避免了把整个工程图片塞进堆，同时保留了「热点路径」信息。
- LRU 触达后晋升到链表头；超过 `MemoryMaxEntries` 或 `MemoryMaxBytes` 时淘汰链表尾。

### L2 行为

- 仅 HTTP（或 ≥1MB）资源会落盘。
- 文件名：`SHA1(uri)` 截 16 字节十六进制 + `.bin`；同名 `.bin.meta` 存 `{source, contentType, statusCode, fetchedAt, sizeBytes, headers}`。
- 命中后调 `File.SetLastAccessTimeUtc` 更新 NTFS 时间，作为 LRU 排序依据（**注意**：若系统禁用 `LastAccessTime` 更新，磁盘 LRU 准确度会下降）。
- 超过 `DiskMaxBytes` 时按 lastAccess 升序淘汰。

---

## 并发去重

`AssetLoader` 内部维护 `ConcurrentDictionary<string, Task<AssetData>>`：

- 同一 URL 多次 `LoadAsync` 共享同一个底层 `Task`；
- 完成后 (`finally`) 从 in-flight 表移除；
- 这意味着脚本可以无脑并发请求，不需要自己去重。

---

## 错误与诊断

- `Failed` 事件：`(int requestId, Uri uri, string message)`。
- HTTP 4xx/5xx：抛出 `HttpRequestException`，message 形如 `HTTP 404 Not Found`。
- 文件不存在：`FileNotFoundException`。
- 不支持的 scheme：`NotSupportedException("No provider for scheme: ...")`。

---

## 调试事件挂钩 (Phase B 预告)

- `AssetLoader.Started/Loaded/Failed/Progress` → DebugBridge 转发为 `network` 帧。
- `AssetLoader.LocalFileTouched` → FileTracker 登记到「已打开文件」面板。
