using Pixi2D.Core;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 虚拟滚动列表的使用示例。
/// 演示如何使用 VirtualScrollList 来高效渲染大量数据。
/// </summary>
public class VirtualScrollListExample
{
    private static readonly Text.Factory TextFactory = new()
    {
        FontFamily = "Arial",
        FontSize = 16,
        FontWeight = FontWeight.Regular,
        FontStyle = FontStyle.Normal
    };

    /// <summary>
    /// 创建一个简单的虚拟滚动列表示例。
    /// </summary>
    public static VirtualScrollList<string> CreateSimpleExample()
    {
        // 创建虚拟滚动列表
        var list = new VirtualScrollList<string>(
            width: 300f,
            height: 400f,
            itemHeight: 50f
        )
        {
            X = 50,
            Y = 50
        };

        // 设置项目渲染器
        list.ItemRenderer = (data, index) =>
        {
            // 创建列表项
            var item = new ListItem(280f, 50f)
            {
                NormalStyle = new(new(0.1f, 0.1f, 0.1f, 1.0f)),
                HoverStyle = new(new(0.2f, 0.2f, 0.3f, 1.0f)),
            };

            // 添加文本内容
            var text = TextFactory.Create($"Item #{index}: {data}", 16, Color.White);
            text.X = 10;
            text.Y = 15;
            item.AddContent(text);

            return item;
        };

        // 生成测试数据（模拟大量数据）
        var testData = Enumerable.Range(0, 10000)
            .Select(i => $"Data item {i}")
            .ToList();

        list.SetData(testData);

        // 添加项目点击事件
        list.OnItemClick += (data, index) =>
        {
            Console.WriteLine($"Clicked: {data} at index {index}");
        };

        return list;
    }

    /// <summary>
    /// 创建一个带有复杂数据的虚拟滚动列表示例。
    /// </summary>
    public static VirtualScrollList<PersonData> CreateComplexExample()
    {
        // 创建虚拟滚动列表
        var list = new VirtualScrollList<PersonData>(
            width: 400f,
            height: 500f,
            itemHeight: 60f
        )
        {
            X = 50,
            Y = 50
        };

        // 设置项目渲染器
        list.ItemRenderer = (person, index) =>
        {
            var item = new ListItem(380f, 60f)
            {
                NormalStyle = new(new(0.05f, 0.05f, 0.08f, 1.0f)),
                HoverStyle = new(new(0.15f, 0.15f, 0.2f, 1.0f)),
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
            var indexBadge = new Graphics
            {
                X = 350,
                Y = 15
            };
            indexBadge.FillColor = new RawColor4(0.4f, 0.4f, 0.5f, 0.8f);
            indexBadge.DrawRoundedRectangle(0, 0, 30, 30, 5, 5);
            item.AddContent(indexBadge);

            var indexText = TextFactory.Create($"{index}", 14, Color.White);
            indexText.X = 357;
            indexText.Y = 23;
            item.AddContent(indexText);

            return item;
        };

        // 生成测试数据
        var random = new Random(42);
        var firstNames = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry", "Ivy", "Jack" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Martinez", "Hernandez" };

        var testData = Enumerable.Range(0, 5000)
            .Select(i => new PersonData
            {
                Name = $"{firstNames[random.Next(firstNames.Length)]} {lastNames[random.Next(lastNames.Length)]}",
                Age = random.Next(18, 80),
                Email = $"user{i}@example.com"
            })
            .ToList();

        list.SetData(testData);

        // 添加项目点击事件
        list.OnItemClick += (person, index) =>
        {
            Console.WriteLine($"Clicked: {person.Name} (Age: {person.Age}, Email: {person.Email}) at index {index}");
        };

        return list;
    }

    /// <summary>
    /// 演示动态操作虚拟滚动列表。
    /// </summary>
    public static void DemonstrateDynamicOperations(VirtualScrollList<string> list)
    {
        Console.WriteLine("=== 虚拟滚动列表动态操作演示 ===");

        // 添加新项
        Console.WriteLine("添加新项...");
        list.AddItem("New Item");

        // 批量添加
        Console.WriteLine("批量添加项...");
        list.AddItems(new[] { "Batch Item 1", "Batch Item 2", "Batch Item 3" });

        // 插入项
        Console.WriteLine("在索引 5 处插入项...");
        list.InsertItem(5, "Inserted Item");

        // 移除项
        Console.WriteLine("移除索引 10 的项...");
        list.RemoveAt(10);

        // 更新项
        Console.WriteLine("更新索引 15 的项...");
        list.UpdateItem(15, "Updated Item");

        // 滚动到指定位置
        Console.WriteLine("滚动到索引 100...");
        list.ScrollToIndex(100);

        // 滚动到顶部
        Console.WriteLine("滚动到顶部...");
        list.ScrollToTop();

        // 滚动到底部
        Console.WriteLine("滚动到底部...");
        list.ScrollToBottom();

        // 刷新列表
        Console.WriteLine("刷新列表显示...");
        list.Refresh();

        Console.WriteLine($"当前列表项数量: {list.ItemCount}");
    }
}

/// <summary>
/// 示例数据类 - 人员信息。
/// </summary>
public class PersonData
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Email { get; set; } = "";
}
