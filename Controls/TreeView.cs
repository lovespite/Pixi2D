using Pixi2D.Components;
using Pixi2D.Core;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls
{
    /// <summary>
    /// 树形节点控件。
    /// 包含头部（箭头+文本）和子节点容器。
    /// </summary>
    public class TreeNode : Container
    {
        // --- 核心依赖 ---
        private readonly Text.Factory _textFactory;

        // --- UI 组件 ---
        private readonly Container _headerContainer;   // 头部容器 (用于响应点击)
        private readonly Graphics _headerBackground;   // 头部背景 (用于高亮)
        private readonly Graphics _arrow;              // 矢量箭头
        private readonly Text _label;                  // 文本标签
        private readonly FlowLayout _childrenContainer;// 子节点容器

        // --- 状态 ---
        private bool _isExpanded = false;
        private bool _isSelected = false;
        private readonly float _indent = 20f; // 缩进量

        // --- 样式 ---
        private RawColor4 _colorNormal = new(0, 0, 0, 0); // 透明
        private RawColor4 _colorHover = new(1f, 1f, 1f, 0.1f);
        private RawColor4 _colorSelected = new(0.2f, 0.6f, 1.0f, 0.3f);
        private RawColor4 _arrowColor = new(0.8f, 0.8f, 0.8f, 1f);

        // --- 事件 ---
        public event Action<TreeNode>? OnNodeSelected;
        public event Action<TreeNode>? OnLayoutChanged; // 通知父级布局更新

        /// <summary>
        /// 节点的唯一标识键。
        /// </summary>
        public string Key { get; set; } = "";

        public TreeNode(Text.Factory textFactory, string text)
        {
            _textFactory = textFactory;

            // 1. 初始化头部容器
            _headerContainer = new Container
            {
                Height = 24f, // 头部高度
                Interactive = true
            };

            // 2. 头部背景 (Selection/Hover)
            _headerBackground = new Graphics();
            UpdateHeaderBackground();
            _headerContainer.AddChild(_headerBackground);

            // 3. 矢量箭头 (使用 Graphics 绘制三角形)
            _arrow = new Graphics
            {
                X = 8, // 箭头中心位置
                Y = 9,
                FillColor = _arrowColor,
                Interactive = true, // 箭头单独可点
                Anchor = 0.5f, 
            };
            // 绘制一个指向右侧的三角形
            _arrow.DrawPolygon([
                new PointF(0, 0),
                new PointF(8, 5),
                new PointF(0, 10)
            ]);
            // 设置锚点在中心，方便旋转 
            _headerContainer.AddChild(_arrow);

            // 4. 文本标签
            _label = textFactory.Create(text, 14, Color.White);
            _label.X = 24; // 箭头右侧
            _label.Y = (_headerContainer.Height - _label.FontSize) / 2f - 2;
            _headerContainer.AddChild(_label);

            AddChild(_headerContainer);

            // 5. 子节点容器 (默认隐藏)
            _childrenContainer = new FlowLayout
            {
                Direction = FlowLayout.LayoutDirection.Vertical,
                Visible = false, // 初始折叠
                X = _indent,     // 缩进
                Y = _headerContainer.Height,
                Gap = 2f
            };
            AddChild(_childrenContainer);

            // --- 事件绑定 ---

            // 头部点击 -> 选中节点
            _headerContainer.OnClick += (e) => Select();

            // 箭头点击 -> 展开/折叠 (阻止冒泡)
            _arrow.OnClick += (e) =>
            {
                Toggle();
                e.StopPropagation();
            };

            // 悬停效果
            _headerContainer.OnMouseOver += (e) =>
            {
                if (!_isSelected) _headerBackground.FillColor = _colorHover;
            };
            _headerContainer.OnMouseOut += (e) =>
            {
                if (!_isSelected) _headerBackground.FillColor = _colorNormal;
            };
        }

        /// <summary>
        /// 获取子节点列表。
        /// </summary>
        public IReadOnlyList<TreeNode> Nodes => [.. _childrenContainer.OfType<TreeNode>()];

        public string Text
        {
            get => _label.Content;
            set => _label.Content = value;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    _childrenContainer.Visible = value;

                    // 旋转箭头: 0度指向右，90度(PI/2)指向下
                    _arrow.Rotation = _isExpanded ? (float)(Math.PI / 2) : 0f;

                    RequestLayoutUpdate();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                UpdateHeaderBackground();
            }
        }

        /// <summary>
        /// 添加现有的 TreeNode 实例。
        /// </summary>
        public void AddNode(TreeNode node)
        {
            _childrenContainer.AddChild(node);
            // 监听子节点的布局变化和选中事件
            node.OnLayoutChanged += RequestLayoutUpdate;
            node.OnNodeSelected += (n) => OnNodeSelected?.Invoke(n); // 冒泡选中事件

            RequestLayoutUpdate();
        }

        /// <summary>
        /// (快捷方法) 创建并添加一个新的子节点。
        /// </summary>
        /// <param name="text">节点显示的文本。</param>
        /// <param name="key">节点的可选键值。</param>
        /// <returns>新创建的 TreeNode 实例。</returns>
        public TreeNode AddNode(string text, string key = "")
        {
            var node = new TreeNode(_textFactory, text)
            {
                Key = key
            };
            AddNode(node);
            return node;
        }

        /// <summary>
        /// 切换展开/折叠状态。
        /// </summary>
        public void Toggle()
        {
            IsExpanded = !IsExpanded;
        }

        /// <summary>
        /// 选中当前节点。
        /// </summary>
        public void Select()
        {
            if (!IsSelected)
            {
                IsSelected = true;
                OnNodeSelected?.Invoke(this);
            }
        }

        /// <summary>
        /// 递归更新布局并通知父级。
        /// </summary>
        private void RequestLayoutUpdate(TreeNode? sender = null)
        {
            // 1. 更新子容器布局
            _childrenContainer.UpdateLayout();

            // 2. 计算当前节点总高度 (头部 + 展开的子容器)
            float contentHeight = _headerContainer.Height;
            if (_isExpanded)
            {
                // 简单估算子容器高度
                var (_, childH) = GetFlowContentSize(_childrenContainer);
                contentHeight += childH;
            }

            // 3. 更新自身高度
            this.Height = contentHeight;

            // 4. 通知父级继续更新
            OnLayoutChanged?.Invoke(this);
        }

        // 辅助计算 FlowLayout 内容大小
        private (float w, float h) GetFlowContentSize(FlowLayout layout)
        {
            float h = 0;
            float w = 0;
            foreach (var child in layout.ToArray())
            {
                if (child.Visible)
                {
                    h += child.Height + layout.Gap;
                    w = Math.Max(w, child.Width);
                }
            }
            return (w, h);
        }

        private void UpdateHeaderBackground()
        {
            _headerBackground.Clear();
            _headerBackground.FillColor = _isSelected ? _colorSelected : _colorNormal;
            // 绘制一个覆盖头部的矩形，宽度设为一个较大值 (例如 2000)，依靠 TreeView 的 Clip 裁剪
            _headerBackground.DrawRectangle(0, 0, 2000, _headerContainer.Height);
        }
    }

    /// <summary>
    /// 树形视图控件。
    /// 管理根节点和滚动。
    /// </summary>
    public class TreeView : Panel
    {
        private readonly Text.Factory _textFactory;
        private readonly FlowLayout _rootLayout;
        private TreeNode? _selectedNode;

        public event Action<TreeNode>? OnSelectionChanged;

        public TreeView(Text.Factory textFactory, float width = 200f, float height = 300f) : base(width, height)
        {
            _textFactory = textFactory;
            BackgroundColor = new RawColor4(0.1f, 0.1f, 0.12f, 1f); // 深色背景
            BorderColor = new RawColor4(0.3f, 0.3f, 0.3f, 1f);
            BorderWidth = 1f;
            ClipContent = true; // 确保裁剪超出的内容

            // 使用 FlowLayout 作为主内容容器
            _rootLayout = new FlowLayout
            {
                Direction = FlowLayout.LayoutDirection.Vertical,
                Gap = 2f,
                Padding = 5f,
                Width = width - 10f // 减去 Panel Padding
            };

            AddContent(_rootLayout);
        }

        /// <summary>
        /// 添加根节点。
        /// </summary>
        public void AddNode(TreeNode node)
        {
            _rootLayout.AddChild(node);
            node.OnLayoutChanged += HandleLayoutChange;
            node.OnNodeSelected += HandleNodeSelected;

            HandleLayoutChange(node);
        }

        /// <summary>
        /// (快捷方法) 创建并添加一个根节点。
        /// </summary>
        public TreeNode AddNode(string text, string key = "")
        {
            var node = new TreeNode(_textFactory, text) { Key = key };
            AddNode(node);
            return node;
        }

        /// <summary>
        /// 获取当前选中的节点。
        /// </summary>
        public TreeNode? SelectedNode => _selectedNode;

        private void HandleLayoutChange(TreeNode sender)
        {
            // 当任何子节点展开/折叠导致高度变化时，重新计算根布局
            _rootLayout.UpdateLayout();
        }

        private void HandleNodeSelected(TreeNode node)
        {
            if (_selectedNode != node)
            {
                // 取消选中旧节点
                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = false;
                }

                _selectedNode = node;

                // 触发事件
                OnSelectionChanged?.Invoke(node);
            }
        }
    }
}