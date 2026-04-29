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
