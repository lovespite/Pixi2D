# Pixi2D DSL & Scripting 概览

> 本文档对应 `feat/dsl-xml-js` 分支的前期准备阶段 (v0.1)。

## 1. 设计目标

为 Pixi2D 引入两套**声明式 / 脚本式**的 UI 编写方式：

1. **PXML (Pixi2D XML)** — 用 XML 描述 UI 布局；
2. **JavaScript (规划中)** — 通过抽象的 `IScriptEngine` 接口实现交互逻辑（事件回调、数据绑定）。

最终用户的工作流类似：

```text
.pxml (布局) + .js (交互) → PxmlLoader → DisplayObject 树 → Stage.Render
```

本批次仅实现 **前期准备**：基础设施、控件无参构造支持、PXML 加载器、脚本引擎抽象、预览器骨架。**JS 引擎未选型**。

## 2. 架构

```
┌────────────────────────────┐
│  Pixi2D.Preview (WinForms) │   ← 预览器: 拖拽/打开/热重载
└───────────────┬────────────┘
                ▼
┌────────────────────────────┐
│  Pixi2D.Markup             │   ← DSL 库
│   ├── ElementRegistry      │     - kebab-case 元素 ↔ Type 映射
│   ├── ValueConverters      │     - 字符串 → 属性值
│   ├── PxmlLoader           │     - XDocument + 反射构建树
│   └── IScriptEngine        │     - JS/脚本桥接抽象
└───────────────┬────────────┘
                ▼
┌────────────────────────────┐
│  Pixi2D (核心)              │
│   ├── UIContext            │   ← 全局上下文 (DWriteFactory / Assets / 默认字体)
│   ├── IAssetLoader         │   ← 资源加载抽象
│   └── 各控件 + 无参构造      │
└────────────────────────────┘
```

## 3. 初始化样板

启动时务必先初始化 `UIContext`：

```csharp
using Pixi2D.Core;
using SharpDX.DirectWrite;

UIContext.Current.DWriteFactory   = new Factory();
UIContext.Current.Assets          = new MyAssetLoader(); // IAssetLoader 实现
UIContext.Current.DefaultFontFamily = "Microsoft YaHei UI";
UIContext.Current.DefaultFontSize   = 14f;
```

之后即可：

```csharp
var loader = new Pixi2D.Markup.PxmlLoader();
var root = loader.LoadFromFile("ui/main.pxml");
stage.AddChild(root);
```

## 4. 文件扩展名

约定使用 `.pxml`。预览器关联此扩展名，编辑器（如 VS Code）建议套用 XML 语法高亮 + `Schema/pixi2d.xsd`。

## 5. Schema

`Pixi2D.Markup/Schema/pixi2d.xsd` 提供基础校验。由于属性集合庞大且可扩展，schema 内大量使用 `anyAttribute processContents="lax"`，强校验留给运行期 (`PxmlLoader` 反射 + `ValueConverters`)。

## 6. 后续

- 选型并接入 `IScriptEngine` 实现 (推荐 Jint 或 ClearScript)。
- 数据绑定 (`{Binding xxx}` 语义)。
- 主题 / 样式表系统。
- VS Code 扩展提供 `.pxml` 智能感知。
- 预览器实现真正的 Direct2D 渲染视图。
