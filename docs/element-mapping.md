# PXML 元素 / 属性映射表

## 1. 元素 ↔ 控件类型

> kebab-case 元素名映射到 `Pixi2D.Controls` / `Pixi2D.Components` / `Pixi2D.Core` 下的具体类型。
> 未列出（或自定义）的元素请通过 `ElementRegistry.Register("my-name", typeof(MyControl))` 注册。

| XML 元素 | .NET 类型 | 备注 |
|---|---|---|
| `text` | `Pixi2D.Core.Text` | 内文写入 `Content` |
| `graphics` | `Pixi2D.Core.Graphics` | |
| `container` | `Pixi2D.Core.Container` | |
| `panel` | `Pixi2D.Controls.Panel` | 子节点用 `AddContent` |
| `button` | `Pixi2D.Controls.Button` | |
| `fancy-text` | `Pixi2D.Controls.FancyText` | |
| `text-box` | `Pixi2D.Controls.TextBox` | |
| `combo-box` | `Pixi2D.Controls.ComboBox` | `combo-box-item` 为 `internal`，需自定义 |
| `switch` | `Pixi2D.Controls.Switch` | |
| `number` | `Pixi2D.Controls.Number` | |
| `progress-bar` | `Pixi2D.Controls.ProgressBar` | |
| `list` | `Pixi2D.Controls.List` | `[Obsolete]` 兼容 |
| `list-item` | `Pixi2D.Controls.ListItem` | 子节点用 `AddContent` |
| `scrollable-list` | `Pixi2D.Controls.ScrollableList` | `[Obsolete]` 兼容 |
| `tree-view` | `Pixi2D.Controls.TreeView` | |
| `tree-node` | `Pixi2D.Controls.TreeNode` | |
| `table` | `Pixi2D.Controls.Table` | |
| `table-cell` | `Pixi2D.Controls.TableCell` | |
| `graphics-spin-loading` | `Pixi2D.Controls.GraphicsSpinLoading` | |
| `modal` | `Pixi2D.Components.Modal` | |
| `flow-layout` | `Pixi2D.Components.FlowLayout` | |
| `auto-flow-layout` | `Pixi2D.Components.AutoFlowLayout` | |
| `soft-keyboard` | `Pixi2D.Components.Utils.SoftKeyboard` | |

### 资源依赖型控件（不内置注册）

下列控件构造时需要 `D2DBitmap` / `Sprite` 等运行期资源，未提供默认无参构造。
请通过 `RegisterFactory` 注入自定义工厂，从 `UIContext.Current.Assets` 解析资源：

| XML 元素 | .NET 类型 | 推荐工厂 |
|---|---|---|
| `icon-label` | `Pixi2D.Controls.IconLabel` | 解析 `icon="<key>"` 属性 |
| `fancy-button` | `Pixi2D.Controls.FancyButton` | 解析 `texture="<key>"` |
| `spin-loading` | `Pixi2D.Controls.SpinLoading` | 解析 `sprite="<key>"` |
| `message-box` | `Pixi2D.Components.MessageBox` | 通常使用 `Builder` API |
| `sprite` | `Pixi2D.Core.Sprite` | 解析 `texture="<key>"` |
| `virtual-scroll-list` | `VirtualScrollList<T>` | 必须为具体 T 注册 |

示例：

```csharp
ElementRegistry.RegisterFactory("icon-label", () =>
{
    var icon = UIContext.Current.Assets!.LoadBitmap("default")!;
    return new IconLabel(UIContext.Current.DefaultTextFactory, "", icon);
});
```

## 2. 属性映射规则

| 规则 | 说明 |
|---|---|
| 名称 | kebab-case → PascalCase 自动反射；例 `background-color` → `BackgroundColor` |
| 未知属性 | 静默忽略（向前兼容） |
| 空值 / 失败 | 抛 `InvalidOperationException` 并提示行号 |

## 3. 内置类型转换器

| 目标类型 | 输入示例 |
|---|---|
| `string` | `"hello"` |
| `bool` | `"true"` / `"false"` |
| `int` / `long` / `byte` | `"42"` |
| `float` / `double` / `decimal` | `"3.14"` (始终 InvariantCulture) |
| `enum` | 名称匹配 (大小写不敏感) |
| `Nullable<T>` | 空字符串 → `null`，否则递归 |
| `RawColor4` | `#RGB` / `#RGBA` / `#RRGGBB` / `#RRGGBBAA` / `rgb(r,g,b)` / `rgba(r,g,b,a)` / 颜色名 |
| `SizeF` / `PointF` | `"w,h"` / `"w h"` / `"w x h"` |
| `System.Drawing.Color` | 同 `RawColor4` |

扩展：`ValueConverters.Register(typeof(MyType), s => ...)` 注入新类型。

## 4. 特殊属性

| 属性 | 含义 |
|---|---|
| `id` / `name` | 设置 `DisplayObject.Name` 并加入 `ScriptHost.NamedObjects[id]` |
| `on-*` (如 `on-click`) | 收集到 `ScriptHost.PendingHandlers`，等待脚本引擎绑定 |
| `x` `y` `width` `height` `visible` `alpha` `rotation` `scale` `anchor-x` `anchor-y` | 直接映射到 `DisplayObject` 同名属性 |

## 5. 子节点规则

| 父节点类型 | 添加方法 |
|---|---|
| `Panel` | `AddContent(child)` |
| `ListItem` | `AddContent(child)` |
| 其他 `Container` | `AddChild(child)` |
| 非 `Container` | 抛错 |

## 6. 焦点机制陷阱（来自代码 memory）

`Panel` 用 `new` 隐藏了 `AcceptFocus / Interactive / FocusTarget` 并重定向到内部 `_background`。
继承 `Panel` 的控件若需要让 `Stage.FindFirstFocusableTarget` 走到自身，**必须** 写到基类字段：

```csharp
((DisplayObject)this).FocusTarget = ...;
```

未来 PXML 反序列化器对 `focus-target` 属性的处理需要走该路径，请勿直接 `panel.FocusTarget = ...`。
