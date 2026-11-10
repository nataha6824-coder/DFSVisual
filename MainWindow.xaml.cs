using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DFS_Visualizer
{
    public partial class MainWindow : Window
    {
        private bool isAnimating = false;

        private TreeNode _root;

        private List<Border> _nodeBorders = new();
        private Dictionary<TreeNode, Border> _nodeToBorder = new();
        private Dictionary<TreeNode, List<Line>> _edges = new();

        // ==== Камера ====
        private double _currentScale = 1.0;
        private Point _lastMousePos;
        private bool _isDragging = false;

        public MainWindow()
        {
            InitializeComponent();
            GenerateTreeFromText();
        }

        // ================= МОДЕЛЬ =================
        private class TreeNode
        {
            public string Label;
            public TreeNode Left, Right;
            public double X, Y;

            public TreeNode(string label) { Label = label; }
            public IEnumerable<TreeNode> Children
            {
                get
                {
                    if (Left != null) yield return Left;
                    if (Right != null) yield return Right;
                }
            }
        }

        // ============== КАМЕРА ==============
        private void ResetCamera()
        {
            _currentScale = 1.0;
            TreeScaleTransform.ScaleX = TreeScaleTransform.ScaleY = 1.0;
            TreeTranslateTransform.X = 0;
            TreeTranslateTransform.Y = 0;
        }

        private void MainCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;

            double zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            double newScale = _currentScale * zoomFactor;
            newScale = Math.Max(0.2, Math.Min(5.0, newScale));

            Point mousePos = e.GetPosition(MainCanvas);

            TreeScaleTransform.ScaleX = newScale;
            TreeScaleTransform.ScaleY = newScale;

            double scaleChange = newScale - _currentScale;
            TreeTranslateTransform.X -= mousePos.X * scaleChange;
            TreeTranslateTransform.Y -= mousePos.Y * scaleChange;

            _currentScale = newScale;
            e.Handled = true;
        }

        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePos = e.GetPosition(MainCanvas);
            _isDragging = true;
            MainCanvas.CaptureMouse();
            MainCanvas.Cursor = Cursors.SizeAll;
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            Point current = e.GetPosition(MainCanvas);
            TreeTranslateTransform.X += current.X - _lastMousePos.X;
            TreeTranslateTransform.Y += current.Y - _lastMousePos.Y;
            _lastMousePos = current;
        }

        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            MainCanvas.ReleaseMouseCapture();
            MainCanvas.Cursor = Cursors.Arrow;
        }

        private void MainCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            e.ManipulationContainer = MainCanvas;
            e.Mode = ManipulationModes.TranslateX | ManipulationModes.TranslateY | ManipulationModes.Scale;
        }

        private void MainCanvas_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            // Сдвиг
            TreeTranslateTransform.X += e.DeltaManipulation.Translation.X;
            TreeTranslateTransform.Y += e.DeltaManipulation.Translation.Y;

            // Масштаб
            double newScale = _currentScale * e.DeltaManipulation.Scale.X;
            newScale = Math.Max(0.2, Math.Min(5.0, newScale));

            Point origin = e.ManipulationOrigin;
            double scaleChange = newScale - _currentScale;

            TreeScaleTransform.ScaleX = newScale;
            TreeScaleTransform.ScaleY = newScale;

            TreeTranslateTransform.X -= origin.X * scaleChange;
            TreeTranslateTransform.Y -= origin.Y * scaleChange;

            _currentScale = newScale;
            e.Handled = true;
        }

        // ============== ГЕНЕРАЦИЯ ==============
        private void GenButton_Click(object sender, RoutedEventArgs e) => GenerateTreeFromText();

        private void GenerateTreeFromText()
        {
            TreeCanvas.Children.Clear();
            _nodeBorders.Clear();
            _nodeToBorder.Clear();
            _edges.Clear();
            LogPanel.Children.Clear();
            StackPanelView.Children.Clear();
            VisitedOrderTextBlock.Text = "—";
            StatusTextBlock.Text = "Готово к запуску";
            StepTextBlock.Text = "  Шаг: 0";

            ResetCamera();

            var parts = TreeInputBox.Text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                MessageBox.Show("Введите узлы дерева (например, 1,2,3,4,5,6,7).", "Внимание");
                return;
            }

            var queue = new Queue<TreeNode>();
            _root = new TreeNode(parts[0]);
            queue.Enqueue(_root);
            int idx = 1;
            while (queue.Count > 0 && idx < parts.Length)
            {
                var cur = queue.Dequeue();
                if (idx < parts.Length) { cur.Left = new TreeNode(parts[idx++]); queue.Enqueue(cur.Left); }
                if (idx < parts.Length) { cur.Right = new TreeNode(parts[idx++]); queue.Enqueue(cur.Right); }
            }

            double leafCounter = 0;
            AssignPositions(_root, 0, ref leafCounter);

            double maxX = MaxX(_root), maxY = MaxY(_root);
            TreeCanvas.Width = Math.Max(1000, maxX * 140 + 200);
            TreeCanvas.Height = Math.Max(700, maxY * 100 + 200);

            DrawTree(_root);

            DfsRecButton.IsEnabled = true;
            DfsStackBtn.IsEnabled = true;
        }

        private void AssignPositions(TreeNode node, double depth, ref double leafCounter)
        {
            node.Y = depth;
            if (node.Left == null && node.Right == null)
            {
                node.X = leafCounter++;
                return;
            }
            if (node.Left != null) AssignPositions(node.Left, depth + 1, ref leafCounter);
            if (node.Right != null) AssignPositions(node.Right, depth + 1, ref leafCounter);

            double sum = 0; int n = 0;
            if (node.Left != null) { sum += node.Left.X; n++; }
            if (node.Right != null) { sum += node.Right.X; n++; }
            node.X = sum / n;
        }

        private double MaxX(TreeNode node) => node.Children.Count() == 0 ? node.X : Math.Max(node.X, node.Children.Max(MaxX));
        private double MaxY(TreeNode node) => node.Children.Count() == 0 ? node.Y : Math.Max(node.Y, node.Children.Max(MaxY));

        private void DrawTree(TreeNode node)
        {
            double sx = 140, sy = 100;
            double px = node.X * sx + 50, py = node.Y * sy + 50;

            foreach (var child in node.Children)
            {
                double cx = child.X * sx + 50, cy = child.Y * sy + 50;
                var line = new Line
                {
                    X1 = px + 25,
                    Y1 = py + 50,
                    X2 = cx + 25,
                    Y2 = cy,
                    Stroke = Brushes.SlateGray,
                    StrokeThickness = 2
                };
                TreeCanvas.Children.Add(line);
                if (!_edges.ContainsKey(node)) _edges[node] = new List<Line>();
                _edges[node].Add(line);
            }

            var border = new Border
            {
                Width = 50,
                Height = 50,
                Background = Brushes.LightSteelBlue,
                BorderBrush = Brushes.DarkSlateGray,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(25),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Gray, BlurRadius = 5, ShadowDepth = 2, Opacity = 0.3 }
            };
            border.Child = new TextBlock
            {
                Text = node.Label,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(border, px);
            Canvas.SetTop(border, py);
            TreeCanvas.Children.Add(border);

            _nodeBorders.Add(border);
            _nodeToBorder[node] = border;

            foreach (var child in node.Children) DrawTree(child);
        }

        // ============== DFS РЕКУРСИЯ ==============
        private async void DfsRecButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAnimating || _root == null) return;
            PrepareForTraversal("DFS через рекурсию (call stack)");

            var visited = new List<string>();
            int step = 0;
            int delay = GetDelay();

            await RunDfsRecursive(_root, visited, delay, () => ++step);

            VisitedOrderTextBlock.Text = string.Join(" → ", visited);
            StatusTextBlock.Text = "Обход завершён";
            FinishTraversal();
        }

        private async Task RunDfsRecursive(TreeNode node, List<string> visited, int delay, Func<int> stepIncrement)
        {
            HighlightNode(node, Brushes.LightGreen);
            visited.Add(node.Label);
            AddLog($"Visit: узел {node.Label}", Brushes.LightGreen);
            PushToStackView(node.Label);
            StepTextBlock.Text = $"  Шаг: {stepIncrement()}";
            await Task.Delay(delay);

            foreach (var child in node.Children)
            {
                HighlightEdge(node, child, Brushes.DarkGreen);
                await Task.Delay(delay / 2);
                await RunDfsRecursive(child, visited, delay, stepIncrement);
            }

            PopFromStackView();
            AddLog($"Return: узел {node.Label}", Brushes.LightBlue);
            HighlightNode(node, Brushes.PaleGreen);
            await Task.Delay(delay / 2);
        }

        // ============== DFS СТЕК ==============
        private async void DfsStackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (isAnimating || _root == null) return;
            PrepareForTraversal("DFS через явный Stack<T>");

            var visited = new List<string>();
            int step = 0;
            int delay = GetDelay();

            await RunDfsStack(_root, visited, delay, () => ++step);

            VisitedOrderTextBlock.Text = string.Join(" → ", visited);
            StatusTextBlock.Text = "Обход завершён";
            FinishTraversal();
        }

        private async Task RunDfsStack(TreeNode root, List<string> visited, int delay, Func<int> stepIncrement)
        {
            var stack = new Stack<TreeNode>();
            stack.Push(root);
            AddLog($"Push: {root.Label}", Brushes.Khaki);
            PushToStackView(root.Label);
            await Task.Delay(delay);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                PopFromStackView();
                AddLog($"Pop: {node.Label}", Brushes.Orange);

                if (visited.Contains(node.Label)) continue;

                HighlightNode(node, Brushes.LightGreen);
                visited.Add(node.Label);
                AddLog($"Visit: {node.Label}", Brushes.LightGreen);
                StepTextBlock.Text = $"  Шаг: {stepIncrement()}";
                await Task.Delay(delay);

                var children = node.Children.ToList();
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    var child = children[i];
                    HighlightEdge(node, child, Brushes.DarkGreen);
                    stack.Push(child);
                    PushToStackView(child.Label);
                    AddLog($"Push: {child.Label}", Brushes.Khaki);
                    await Task.Delay(delay / 3);
                }
            }
        }

        // ============== ВИЗУАЛ ==============
        private void PrepareForTraversal(string title)
        {
            isAnimating = true;
            DfsRecButton.IsEnabled = false;
            DfsStackBtn.IsEnabled = false;
            GenButton.IsEnabled = false;
            LogPanel.Children.Clear();
            StackPanelView.Children.Clear();
            VisitedOrderTextBlock.Text = "—";
            StatusTextBlock.Text = title;

            foreach (var border in _nodeToBorder.Values)
            {
                border.Background = Brushes.LightSteelBlue;
                border.BorderBrush = Brushes.DarkSlateGray;
                border.BorderThickness = new Thickness(2);
            }
            foreach (var edgeList in _edges.Values)
                foreach (var line in edgeList)
                    line.Stroke = Brushes.SlateGray;
        }

        private void FinishTraversal()
        {
            isAnimating = false;
            DfsRecButton.IsEnabled = true;
            DfsStackBtn.IsEnabled = true;
            GenButton.IsEnabled = true;
        }

        private void HighlightNode(TreeNode node, Brush color)
        {
            if (_nodeToBorder.TryGetValue(node, out var b))
            {
                b.Background = color;
                b.BorderBrush = Brushes.Black;
                b.BorderThickness = new Thickness(3);
            }
        }

        private void HighlightEdge(TreeNode parent, TreeNode child, Brush color)
        {
            if (_edges.TryGetValue(parent, out var list))
            {
                int idx = parent.Children.ToList().IndexOf(child);
                if (idx >= 0 && idx < list.Count)
                    list[idx].Stroke = color;
            }
        }

        private void AddLog(string msg, Brush bg)
        {
            var border = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(6, 3, 6, 3)
            };
            border.Child = new TextBlock { Text = msg, FontSize = 12 };
            LogPanel.Children.Add(border);
        }

        private void PushToStackView(string label)
        {
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Khaki,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 2)
            };
            StackPanelView.Children.Insert(0, tb);
        }

        private void PopFromStackView()
        {
            if (StackPanelView.Children.Count > 0)
                StackPanelView.Children.RemoveAt(0);
        }

        private int GetDelay() => SpeedComboBox.SelectedIndex switch
        {
            0 => 800,
            1 => 400,
            2 => 120,
            _ => 400
        };

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAnimating) return;
            TreeInputBox.Text = "";
            GenerateTreeFromText();
        }
    }
}