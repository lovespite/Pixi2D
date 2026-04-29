# Pixi2D Debugger 协议

> 适用版本：v0.7+ ｜ 端口默认 **127.0.0.1:9229**

## 启动

```powershell
Pixi2D.Host.exe foo.pxml --debug            # 监听 9229
Pixi2D.Host.exe foo.pxml --debug 9320       # 自定义端口
Pixi2D.Host.exe foo.pxml --debug-wait       # 启动后阻塞等待 Debugger 接入
```

## 帧格式

每帧为一行 UTF-8 JSON，**以 `\n` 结束**：

```json
{ "id"?: 123, "type": "console", "payload": { ... } }
```

- `type`：消息类型（详见下表）
- `id`：可选；用于关联请求 / 响应
- `payload`：业务数据 (object)
- 仅监听 127.0.0.1；同一时刻只接受 1 个客户端，第 2 个连接会被立即关闭

## Host → Debugger

| type           | payload 字段                                                                                | 说明                                |
|----------------|---------------------------------------------------------------------------------------------|-----------------------------------|
| `hello`        | `host`, `version`, `pxmlPath`, `pid`                                                        | 客户端连接后立即下发                  |
| `tree.update`  | `root: { id, kind, name, x, y, w, h, visible, children[] }`                                 | 元素树（默认 1Hz）                   |
| `console`      | `level: 'log'/'warn'/'error'`, `text`, `ts`                                                 | `console.log/warn/error` 输出       |
| `network`      | `phase: 'start'/'end'/'error'`, `url`, `method?`, `status?`, `bytes?`, `headers?`, `ms?`, `error?`, `ts` | HTTP 资源请求生命周期                |
| `file`         | `path`, `kind: 'pxml'/'js'/'asset'`, `size`, `mtime`                                        | 已打开/加载的文件                    |
| `evalResult`   | `ok: bool`, `value?`, `error?`                                                              | `eval` 请求的响应                   |
| `error`        | `message`                                                                                   | 协议层错误（如 frame parse failure） |

## Debugger → Host

| type            | payload                | 响应                                       |
|-----------------|------------------------|------------------------------------------|
| `eval`          | `code: string`         | 异步 → `evalResult` 帧                     |
| `tree.refresh`  | `{}`                   | 立即触发一次 `tree.update`，并 `tree.refresh.reply` 回 `{ok:true}` |

## 节流与一致性

- `tree.update` 默认 1Hz；可通过 `tree.refresh` 强制全量重推
- 当前未实现增量树（每次推送都是完整结构）— v0.8 候选
- `network` 帧的 `headers` 是 `{name: value}` 对象（多值合并为逗号分隔字符串）

## 错误与重连

- Host 关闭连接后客户端应 1s/2s/4s/... 指数退避重连
- 协议解析失败：Host 发送 `error` 帧并保持连接
- 客户端发送非法 JSON：Host 静默丢弃当前行

---

## Pixi2D.Debugger (UI 客户端)

独立 WinUI 3 程序，位于 `Pixi2D.Debugger/`。运行方式：

`powershell
# 1. 启动 Host 并打开调试桥
.\Pixi2D.Host\bin\Debug\net10.0-windows\win-x64\Pixi2D.Host.exe demos\12-assets\main.pxml --debug

# 2. 启动调试器（默认连 127.0.0.1:9229）
.\Pixi2D.Debugger\bin\Debug\net10.0-windows10.0.19041.0\Pixi2D.Debugger.exe
`

5 个面板：
- **Tree** — Stage 元素树（缩进文本，1 Hz 刷新；可手动 Refresh）
- **Console** — JS `console.log/warn/error` 输出（带时间戳）
- **Network** — HTTP 请求生命周期（status / 字节 / 耗时）
- **Files** — 已加载的本地资源（.pxml / .js / AssetLoader 触发的本地文件）
- **Eval** — 输入 JS 表达式，`Enter` 提交，结果显示 `= value` 或 `! error`

注意：调试桥仅监听 `127.0.0.1`，不绑定 `0.0.0.0`，无 token；仅本机使用。
