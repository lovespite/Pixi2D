# Pixi2D 控件库

这个目录包含基于 Pixi2D 框架构建的常用 UI 控件。所有控件都继承自 `DisplayObject` 或 `Container`，支持事件处理和变换。

## 控件列表

### 1. List (列表控件)

支持动态添加、删除项目并自动调整布局的列表容器。

**主要特性：**
- 支持垂直和水平布局方向
- 自动项目间距管理
- 内边距支持
- 动态添加/删除项目时自动重新布局
- 获取内容尺寸

**使用示例：**
```csharp
var list = new List
{
    Direction = List.LayoutDirection.Vertical,
    ItemSpacing = 5f,
    X = 50,
    Y = 50
};

list.SetPadding(10f);

// 添加子项
list.AddChild(someDisplayObject);

// 批量添加
list.AddChildren(obj1, obj2, obj3);

// 移除项目
list.RemoveChild(someDisplayObject);

// 获取内容尺寸
var (width, height) = list.GetContentSize();
```

**属性：**
- `Direction` - 布局方向 (Vertical/Horizontal)
- `ItemSpacing` - 项目间距
- `PaddingLeft/Top/Right/Bottom` - 内边距
- `ItemCount` - 项目数量

**方法：**
- `AddChild(DisplayObject)` - 添加子项
- `RemoveChild(DisplayObject)` - 移除子项
- `ClearChildren()` - 清除所有子项
- `UpdateLayout()` - 手动更新布局
- `GetItemAt(int)` - 通过索引获取项目
- `GetContentSize()` - 获取内容尺寸

---

### 2. ListItem (列表项)

带有背景和交互状态的列表项容器。

**主要特性：**
- 自动背景渲染
- 悬停、选中状态支持
- 可自定义颜色
- 内容容器分离
- 选中事件

**使用示例：**
```csharp
var item = new ListItem(200, 40);

// 设置颜色
item.NormalColor = new RawColor4(0.2f, 0.2f, 0.2f, 1.0f);
item.HoverColor = new RawColor4(0.3f, 0.3f, 0.3f, 1.0f);
item.SelectedColor = new RawColor4(0.4f, 0.5f, 0.7f, 1.0f);

// 添加内容
var text = new Text(dwFactory, "列表项文本", "Arial", 14f, 
                    FontStyle.Normal, FontWeight.Normal, 
                    new RawColor4(1f, 1f, 1f, 1f));
text.X = 10;
text.Y = 10;
item.AddContent(text);

// 监听选中事件
item.OnSelected += (listItem) => {
    Console.WriteLine("项目被选中");
};

item.OnDeselected += (listItem) => {
    Console.WriteLine("项目取消选中");
};
```

**属性：**
- `Width/Height` - 尺寸
- `IsSelected` - 是否选中
- `NormalColor` - 普通状态颜色
- `HoverColor` - 悬停状态颜色
- `SelectedColor` - 选中状态颜色

**事件：**
- `OnSelected` - 选中时触发
- `OnDeselected` - 取消选中时触发

---

### 3. Button (按钮控件)

可点击的按钮控件，带有文本标签和状态效果。

**主要特性：**
- 圆角背景
- 悬停、按下状态
- 可自定义文本和颜色
- 点击事件

**使用示例：**
```csharp
var button = new Button(dwFactory, "点击我", 100, 30)
{
    X = 50,
    Y = 50,
    CornerRadius = 5f,
    NormalColor = new RawColor4(0.3f, 0.3f, 0.3f, 1.0f),
    HoverColor = new RawColor4(0.4f, 0.4f, 0.4f, 1.0f),
    PressedColor = new RawColor4(0.2f, 0.2f, 0.2f, 1.0f)
};

// 监听点击事件
button.OnButtonClick += (btn) => {
    Console.WriteLine($"按钮被点击: {btn.Text}");
};

// 修改文本
button.Text = "新文本";
button.FontSize = 16f;
```

**属性：**
- `Width/Height` - 尺寸
- `Text` - 按钮文本
- `CornerRadius` - 圆角半径
- `NormalColor` - 普通状态颜色
- `HoverColor` - 悬停状态颜色
- `PressedColor` - 按下状态颜色
- `TextColor` - 文本颜色
- `FontSize` - 字体大小

**事件：**
- `OnButtonClick` - 点击时触发

---

### 4. Panel (面板控件)

带有背景和边框的容器面板。

**主要特性：**
- 可自定义背景和边框
- 圆角支持
- 内边距管理
- 内容容器分离

**使用示例：**
```csharp
var panel = new Panel(400, 300)
{
    X = 50,
    Y = 50,
    CornerRadius = 10f,
    BackgroundColor = new RawColor4(0.15f, 0.15f, 0.15f, 1.0f),
    BorderColor = new RawColor4(0.5f, 0.5f, 0.5f, 1.0f),
    BorderWidth = 2f
};

panel.SetPadding(20f);

// 添加内容
var title = new Text(dwFactory, "面板标题", "Arial", 18f, 
                     FontStyle.Normal, FontWeight.Bold, 
                     new RawColor4(1f, 1f, 1f, 1f));
panel.AddContent(title);

// 访问内容容器进行高级布局
var contentContainer = panel.ContentContainer;
```

**属性：**
- `Width/Height` - 尺寸
- `BackgroundColor` - 背景颜色
- `BorderColor` - 边框颜色
- `BorderWidth` - 边框宽度
- `CornerRadius` - 圆角半径
- `PaddingLeft/Top/Right/Bottom` - 内边距
- `ContentContainer` - 内容容器 (只读)

**方法：**
- `AddContent(DisplayObject)` - 添加内容
- `RemoveContent(DisplayObject)` - 移除内容
- `ClearContent()` - 清除所有内容
- `SetPadding(...)` - 设置内边距

---

### 5. ScrollableList (可滚动列表)

带有滚动条的列表控件，当内容超出可视区域时自动显示滚动条。

**主要特性：**
- 自动显示/隐藏滚动条
- 可拖动滚动条滑块
- 支持编程滚动
- 继承自 Panel，支持所有面板特性
- 基于 List 进行内容管理

**使用示例：**
```csharp
var scrollList = new ScrollableList(300, 400)
{
    X = 50,
    Y = 50
};

scrollList.SetPadding(10f);

// 添加大量项目
for (int i = 0; i < 20; i++)
{
    var item = new ListItem(260, 40);
    var text = new Text(dwFactory, $"项目 {i + 1}", "Arial", 14f, 
                        FontStyle.Normal, FontWeight.Normal, 
                        new RawColor4(1f, 1f, 1f, 1f));
    text.X = 10;
    text.Y = 10;
    item.AddContent(text);
    scrollList.AddItem(item);
}

// 编程控制滚动
scrollList.ScrollDown(50f);  // 向下滚动 50 像素
scrollList.ScrollToTop();     // 滚动到顶部
scrollList.ScrollToBottom();  // 滚动到底部

// 访问内部列表进行配置
scrollList.InnerList.ItemSpacing = 5f;
```

**属性：**
- `InnerList` - 内部列表 (只读)
- `ViewportHeight` - 可视区域高度
- `ScrollPosition` - 当前滚动位置
- `MaxScrollPosition` - 最大滚动位置 (只读)

**方法：**
- `AddItem(DisplayObject)` - 添加项目
- `AddItems(params DisplayObject[])` - 批量添加项目
- `RemoveItem(DisplayObject)` - 移除项目
- `ClearItems()` - 清除所有项目
- `ScrollUp(float)` - 向上滚动
- `ScrollDown(float)` - 向下滚动
- `ScrollToTop()` - 滚动到顶部
- `ScrollToBottom()` - 滚动到底部

---

### 6. VirtualScrollList (虚拟滚动列表)

高性能虚拟滚动列表，只渲染可见区域内的项目，适用于大数据量列表（数千到数万条数据）。

**主要特性：**
- 虚拟化渲染 - 只创建可见项的 UI 元素
- 自动资源回收 - 不可见项会被移除和销毁
- 数据驱动 - 基于数据源和自定义渲染器
- 高性能 - 支持数万条数据流畅滚动
- 完整的滚动条支持
- 滚动到指定索引
- 动态数据操作

**使用示例：**
```csharp
// 1. 创建虚拟滚动列表
var virtualList = new VirtualScrollList<string>(
    width: 300f,
    height: 400f,
    itemHeight: 50f
)
{
    X = 50,
    Y = 50
};

// 2. 设置项目渲染器（定义如何从数据创建 UI）
virtualList.ItemRenderer = (data, index) =>
{
    var item = new ListItem(280f, 50f)
    {
        NormalColor = new RawColor4(0.1f, 0.1f, 0.1f, 1.0f),
        HoverColor = new RawColor4(0.2f, 0.2f, 0.3f, 1.0f),
        SelectedColor = new RawColor4(0.3f, 0.4f, 0.6f, 1.0f)
    };

    var text = TextFactory.Create($"Item #{index}: {data}", 16, Color.White);
    text.X = 10;
    text.Y = 15;
    item.AddContent(text);

    return item;
};

// 3. 设置数据（可以是数千条）
var testData = Enumerable.Range(0, 10000)
    .Select(i => $"Data item {i}")
    .ToList();
virtualList.SetData(testData);

// 4. 监听项目点击
virtualList.OnItemClick += (data, index) =>
{
    Console.WriteLine($"Clicked: {data} at index {index}");
};

// 5. 编程控制滚动
virtualList.ScrollToIndex(500);  // 滚动到第 500 项
virtualList.ScrollToTop();        // 滚动到顶部
virtualList.ScrollToBottom();     // 滚动到底部
```

**属性：**
- `ItemHeight` - 每个列表项的高度（必须统一）
- `ViewportHeight` - 可视区域高度
- `ScrollPosition` - 当前滚动位置
- `ItemCount` - 数据项数量（只读）
- `ItemRenderer` - 项目渲染器委托

**方法：**
- `SetData(IEnumerable<T>)` - 设置数据源
- `AddItem(T)` - 添加单个数据项
- `AddItems(IEnumerable<T>)` - 批量添加数据项
- `InsertItem(int, T)` - 在指定索引插入数据项
- `RemoveAt(int)` - 移除指定索引的数据项
- `RemoveItem(T)` - 移除指定的数据项
- `UpdateItem(int, T)` - 更新指定索引的数据项
- `Clear()` - 清除所有数据项
- `GetItem(int)` - 获取指定索引的数据项
- `ScrollToIndex(int)` - 滚动到指定索引
- `ScrollToTop()` - 滚动到顶部
- `ScrollToBottom()` - 滚动到底部
- `Refresh()` - 刷新可见项的渲染

**事件：**
- `OnItemClick` - 项目被点击时触发

**性能优势：**

传统列表 vs 虚拟滚动列表对比：

| 数据量 | 传统列表 | 虚拟滚动列表 |
|--------|----------|--------------|
| 100 项 | 100 个 UI 元素 | ~10 个 UI 元素（仅可见项） |
| 1,000 项 | 1,000 个 UI 元素 | ~10 个 UI 元素 |
| 10,000 项 | 10,000 个 UI 元素（卡顿） | ~10 个 UI 元素（流畅） |

**使用建议：**
1. 当数据量超过 50-100 项时，建议使用虚拟滚动列表
2. 所有列表项必须具有相同的高度
3. ItemRenderer 应该快速创建 UI，避免复杂计算
4. 数据对象应该是不可变的，或在修改后调用 UpdateItem
5. 避免在 ItemRenderer 中存储状态，应该完全基于数据

---

## 完整使用示例

### 示例 1: 简单垂直列表

```csharp
var list = new List
{
    Direction = List.LayoutDirection.Vertical,
    ItemSpacing = 5f,
    X = 50,
    Y = 50
};

list.SetPadding(10f);

for (int i = 0; i < 5; i++)
{
    var item = new ListItem(200, 40);
    var text = new Text(dwFactory, $"列表项 {i + 1}", "Arial", 16f, 
                        FontStyle.Normal, FontWeight.Normal, 
                        new RawColor4(1f, 1f, 1f, 1f));
    text.X = 10;
    text.Y = 10;
    item.AddContent(text);
    list.AddChild(item);
}

stage.AddChild(list);
```

### 示例 2: 带删除按钮的动态列表

```csharp
var list = new List
{
    Direction = List.LayoutDirection.Vertical,
    ItemSpacing = 5f
};

var addButton = new Button(dwFactory, "添加项目", 120, 30);
int counter = 1;

addButton.OnButtonClick += (btn) =>
{
    var newItem = new ListItem(350, 50);
    
    var text = new Text(dwFactory, $"项目 {counter++}", "Arial", 16f, 
                        FontStyle.Normal, FontWeight.Normal, 
                        new RawColor4(1f, 1f, 1f, 1f));
    text.X = 10;
    text.Y = 15;
    newItem.AddContent(text);
    
    var deleteBtn = new Button(dwFactory, "删除", 60, 30)
    {
        X = 280,
        Y = 10,
        NormalColor = new RawColor4(0.8f, 0.2f, 0.2f, 1.0f)
    };
    
    deleteBtn.OnButtonClick += (delBtn) =>
    {
        list.RemoveChild(newItem);
        newItem.Dispose();
    };
    
    newItem.AddContent(deleteBtn);
    list.AddChild(newItem);
};
```

### 示例 3: 单选列表

```csharp
var list = new List
{
    Direction = List.LayoutDirection.Vertical,
    ItemSpacing = 3f
};

ListItem? selectedItem = null;

for (int i = 0; i < 8; i++)
{
    var item = new ListItem(260, 35)
    {
        SelectedColor = new RawColor4(0.2f, 0.5f, 0.8f, 1.0f)
    };
    
    var text = new Text(dwFactory, $"选项 {i + 1}", "Arial", 14f, 
                        FontStyle.Normal, FontWeight.Normal, 
                        new RawColor4(1f, 1f, 1f, 1f));
    text.X = 10;
    text.Y = 10;
    item.AddContent(text);
    
    // 单选行为
    item.OnSelected += (currentItem) =>
    {
        if (selectedItem is not null && selectedItem != currentItem)
        {
            selectedItem.IsSelected = false;
        }
        selectedItem = currentItem;
    };
    
    list.AddChild(item);
}
```

### 示例 4: 复杂嵌套布局

```csharp
var mainContainer = new Container();

// 左侧菜单面板
var leftPanel = new Panel(150, 400)
{
    X = 0,
    Y = 0,
    CornerRadius = 5f
};
leftPanel.SetPadding(10f);

var menuList = new List
{
    Direction = List.LayoutDirection.Vertical,
    ItemSpacing = 5f
};

for (int i = 0; i < 5; i++)
{
    var btn = new Button(dwFactory, $"菜单 {i + 1}", 120, 35);
    menuList.AddChild(btn);
}

leftPanel.AddContent(menuList);

// 右侧内容面板
var rightPanel = new Panel(400, 400)
{
    X = 170,
    Y = 0,
    CornerRadius = 5f
};
rightPanel.SetPadding(20f);

var contentList = new List
{
    Direction = List.LayoutDirection.Vertical,
    ItemSpacing = 8f
};

for (int i = 0; i < 6; i++)
{
    var item = new ListItem(350, 45);
    var text = new Text(dwFactory, $"内容项 {i + 1}", "Arial", 14f, 
                        FontStyle.Normal, FontWeight.Normal, 
                        new RawColor4(0.9f, 0.9f, 0.9f, 1f));
    text.X = 10;
    text.Y = 12;
    item.AddContent(text);
    contentList.AddChild(item);
}

rightPanel.AddContent(contentList);

mainContainer.AddChildren(leftPanel, rightPanel);
stage.AddChild(mainContainer);
```

## 设计说明

### 架构
- 所有控件继承自 `Container`，可以包含子对象
- 使用 `Graphics` 绘制背景和边框
- 使用事件系统处理交互
- 支持完整的变换 (位置、缩放、旋转、透明度)

### 布局系统
- `List` 提供自动布局功能
- 支持内边距和项目间距
- 布局在添加/删除子项时自动更新
- 可以手动调用 `UpdateLayout()` 强制更新

### 状态管理
- `ListItem` 和 `Button` 管理自己的视觉状态
- 使用颜色变化表示不同状态
- 事件驱动的状态转换

### 内存管理
- 所有控件实现 `IDisposable`
- 移除控件后应调用 `Dispose()` 释放资源
- 父容器被销毁时会自动清理子项

### 示例 5: 虚拟滚动列表 - 大数据量展示

```csharp
// 创建带有复杂数据的虚拟滚动列表
var virtualList = new VirtualScrollList<PersonData>(
    width: 400f,
    height: 500f,
    itemHeight: 60f
)
{
    X = 50,
    Y = 50
};

// 设置自定义渲染器
virtualList.ItemRenderer = (person, index) =>
{
    var item = new ListItem(380f, 60f)
    {
        NormalColor = new RawColor4(0.05f, 0.05f, 0.08f, 1.0f),
        HoverColor = new RawColor4(0.15f, 0.15f, 0.2f, 1.0f),
        SelectedColor = new RawColor4(0.25f, 0.35f, 0.55f, 1.0f)
    };

    // 名称
    var nameText = TextFactory.Create(person.Name, 18, Color.White);
    nameText.X = 10;
    nameText.Y = 8;
    item.AddContent(nameText);

    // 详细信息
    var detailText = TextFactory.Create($"Age: {person.Age} | Email: {person.Email}", 12, Color.LightGray);
    detailText.X = 10;
    detailText.Y = 32;
    item.AddContent(detailText);

    // 索引标签
    var indexBadge = new Graphics { X = 350, Y = 15 };
    indexBadge.FillColor = new RawColor4(0.4f, 0.4f, 0.5f, 0.8f);
    indexBadge.DrawRoundedRectangle(0, 0, 30, 30, 5, 5);
    item.AddContent(indexBadge);

    var indexText = TextFactory.Create($"{index}", 14, Color.White);
    indexText.X = 357;
    indexText.Y = 23;
    item.AddContent(indexText);

    return item;
};

// 生成 5000 条测试数据
var testData = Enumerable.Range(0, 5000)
    .Select(i => new PersonData
    {
        Name = $"Person {i}",
        Age = 20 + (i % 60),
        Email = $"user{i}@example.com"
    })
    .ToList();

virtualList.SetData(testData);

// 添加点击事件
virtualList.OnItemClick += (person, index) =>
{
    Console.WriteLine($"Clicked: {person.Name} at index {index}");
};

// 添加搜索功能
var searchBox = new TextInput(dwFactory, 300, 30) { X = 50, Y = 10 };
searchBox.OnTextChanged += (newText) =>
{
    var filtered = testData.Where(p => 
        p.Name.Contains(newText, StringComparison.OrdinalIgnoreCase)
    ).ToList();
    virtualList.SetData(filtered);
};

stage.AddChild(searchBox);
stage.AddChild(virtualList);
```

## 扩展建议

基于这些基础控件，你可以轻松创建更多控件：

1. **ComboBox** - 下拉选择框
2. **CheckBox** - 复选框
3. **RadioButton** - 单选按钮
4. **Slider** - 滑块控件
5. **TextInput** - 文本输入框
6. **ProgressBar** - 进度条
7. **TabControl** - 选项卡控件
8. **TreeView** - 树形视图
9. **Grid** - 网格布局容器
10. **VirtualGrid** - 虚拟化网格（二维虚拟化）

---

有关更多示例，请参阅 `ControlsExample.cs` 文件。
