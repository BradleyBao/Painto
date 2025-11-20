using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.UI;
using WinRT.Interop;

namespace Painto
{
    public sealed partial class ToolBarWindow : Window
    {
        // 笔画数据结构
        private class Stroke
        {
            public List<Vector2> Points { get; set; } = new List<Vector2>();
            public Color Color { get; set; }
            public float Size { get; set; }
            public CanvasGeometry CachedGeometry { get; set; }
            public bool IsEraserStroke { get; set; } = false;
        }

        private List<Stroke> _allStrokes = new List<Stroke>();
        private Stroke _currentStroke;

        // 状态变量
        public static bool _isEraserMode = false;
        public static bool _computerMode = false;

        // 笔刷属性 (静态变量供外部修改)
        public static Color penColor = Colors.Black;
        public static int penThickness = 5;

        // 控制橡皮擦模式
        // true = 真实擦除 (Pixel Eraser), false = 整根擦除 (Object Eraser)
        public static bool IsPixelEraserMode = false;
        public static double EraserSize = 30.0;
        private Vector2 _currentCursorPos = Vector2.Zero;
        private bool _isHovering = false; // 鼠标是否在窗口内

        // 窗口穿透相关常量
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000; // 确保窗口分层，穿透必须
        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_DLGFRAME = 0x00400000;

        // SetWindowPos Flags
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        // --- P/Invoke 定义 (修复 64位 兼容性) ---
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) return GetWindowLongPtr64(hWnd, nIndex);
            else return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        internal static IntPtr hwnd;

        // 保存 Canvas 引用
        private static Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl _canvasRef;

        public ToolBarWindow()
        {
            this.InitializeComponent();
            hwnd = WindowNative.GetWindowHandle(this);
            _canvasRef = MyCanvas; // 获取引用

            // 初始化窗口样式
            long style = GetWindowLong(hwnd, GWL_STYLE).ToInt64();
            style &= ~(WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_BORDER | WS_DLGFRAME);
            SetWindowLong(hwnd, GWL_STYLE, (IntPtr)style);

            WinUIEx.HwndExtensions.SetAlwaysOnTop(hwnd, true);

            // 初始为穿透模式
            LockScreen();

            // 全屏
            //EnterFullScreenMode();
        }

        // 窗口辅助方法

        public static void UnlockScreen()
        {
            // ! 画图模式
            // 移除 WS_EX_TRANSPARENT -> 鼠标拦截 (画图)
            long extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLong(hwnd, GWL_EXSTYLE, (IntPtr)(extendedStyle & ~WS_EX_TRANSPARENT));

            // 强制刷新窗口框架，确保样式立即生效
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // 开启 Canvas 命中测试
            if (_canvasRef != null) _canvasRef.IsHitTestVisible = true;
        }

        public static void LockScreen()
        {
            // ! 桌面模式
            // 添加 WS_EX_TRANSPARENT -> 鼠标穿透 (桌面)
            // 确保 WS_EX_LAYERED 存在
            long extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLong(hwnd, GWL_EXSTYLE, (IntPtr)(extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED));

            // 强制刷新窗口框架
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // 禁用 Canvas 命中测试，防止 Win2D 抢占输入
            if (_canvasRef != null) _canvasRef.IsHitTestVisible = false;
        }

        // Win2D 绘图逻辑

        private void MyCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_computerMode) return;

            MyCanvas.CapturePointer(e.Pointer);

            var pt = e.GetCurrentPoint(MyCanvas).Position;
            var vecPt = new Vector2((float)pt.X, (float)pt.Y);

            if (_isEraserMode)
            {
                if (IsPixelEraserMode) // 模式 A：真实橡皮擦
                {
                    // 创建一条“橡皮擦轨迹”
                    _currentStroke = new Stroke
                    {
                        Color = Colors.White, // 混合模式会忽略颜色
                        Size = (float)EraserSize,
                        IsEraserStroke = true // 标记为橡皮
                    };
                    _currentStroke.Points.Add(vecPt);
                    _allStrokes.Add(_currentStroke);
                    MyCanvas.Invalidate();
                }
                else // 模式 B：整根删除 
                {
                    TryErase(vecPt);
                }
            }
            else
            {
                _currentStroke = new Stroke
                {
                    Color = penColor,
                    Size = (float)penThickness
                };
                _currentStroke.Points.Add(vecPt);
                _allStrokes.Add(_currentStroke);
                MyCanvas.Invalidate();
            }
        }

        private void MyCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_computerMode) return;

            var pt = e.GetCurrentPoint(MyCanvas).Position;
            _currentCursorPos = new Vector2((float)pt.X, (float)pt.Y);
            _isHovering = true;

            if (_isEraserMode && IsPixelEraserMode)
            {
                MyCanvas.Invalidate();
            }


            var vecPt = new Vector2((float)pt.X, (float)pt.Y);

            if (e.GetCurrentPoint(MyCanvas).Properties.IsLeftButtonPressed)
            {
                bool hasUpdates = false;
                if (_isEraserMode)
                {
                    if (IsPixelEraserMode)
                    {
                        // 模式 A：真实橡皮擦 -> 像画笔一样记录轨迹
                        if (_currentStroke != null)
                        {
                            _currentStroke.Points.Add(vecPt);
                            hasUpdates = true;
                        }
                    }
                    else
                    {
                        // 模式 B：整根删除 -> 检测碰撞
                        if (TryErase(vecPt)) hasUpdates = true;
                    }
                }
                else if (_currentStroke != null)
                {
                    _currentStroke.Points.Add(vecPt);
                    hasUpdates = true;
                    MyCanvas.Invalidate();
                }

                if (hasUpdates) MyCanvas.Invalidate();
            }

            
        }

        private void MyCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _isHovering = false;
            MyCanvas.Invalidate();
        }

        private void MyCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_currentStroke != null && _currentStroke.Points.Count > 1)
            {
                // 画完一笔后，立即生成几何体并缓存
                using (var builder = new CanvasPathBuilder(MyCanvas))
                {
                    builder.BeginFigure(_currentStroke.Points[0]);
                    for (int i = 1; i < _currentStroke.Points.Count; i++)
                    {
                        builder.AddLine(_currentStroke.Points[i]);
                    }
                    builder.EndFigure(CanvasFigureLoop.Open);

                    // 保存到 Stroke 对象中
                    _currentStroke.CachedGeometry = CanvasGeometry.CreatePath(builder);
                }
            }
            MyCanvas.ReleasePointerCapture(e.Pointer);
            _currentStroke = null;
        }

        // ToolBarWindow.xaml.cs

        // ToolBarWindow.xaml.cs

        private void MyCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            // 性能检查
            if (_allStrokes.Count == 0 && _currentStroke == null) return;

            // 获取当前画布大小
            var currentSize = sender.Size;
            var canvasRect = new Windows.Foundation.Rect(0, 0, currentSize.Width, currentSize.Height);

            using (var style = new Microsoft.Graphics.Canvas.Geometry.CanvasStrokeStyle())
            {
                style.StartCap = CanvasCapStyle.Round;
                style.EndCap = CanvasCapStyle.Round;
                style.LineJoin = CanvasLineJoin.Round;

                // 创建一个主图层 (用于隔离混合模式，防止橡皮擦把背景擦黑)
                using (var layer = args.DrawingSession.CreateLayer(1.0f))
                {
                    // 绘制所有历史笔画
                    foreach (var stroke in _allStrokes)
                    {
                        if (stroke.IsEraserStroke)
                        {
                            // 使用 CanvasCommandList 来实现橡皮擦
                            using (var cl = new CanvasCommandList(sender))
                            {
                                // 笔画先画在命令列表里
                                using (var clds = cl.CreateDrawingSession())
                                {
                                    DrawSingleStroke(sender, clds, stroke, style);
                                }

                                // 把命令列表以 DestinationOut (扣除) 模式画到屏幕上
                                // 参数：图像(cl), 目标区域(rect), 源区域(rect), 透明度(1), 插值(Linear), 混合模式(DestinationOut)
                                args.DrawingSession.DrawImage(cl, canvasRect, canvasRect, 1.0f, CanvasImageInterpolation.Linear, CanvasComposite.DestinationOut);
                            }
                        }
                        else
                        {
                            // 普通笔画直接画
                            DrawSingleStroke(sender, args.DrawingSession, stroke, style);
                        }
                    }

                    // 绘制当前正在画的
                    if (_currentStroke != null && _currentStroke.Points.Count > 0)
                    {
                        if (_currentStroke.IsEraserStroke)
                        {
                            // 同样处理当前橡皮擦
                            using (var cl = new CanvasCommandList(sender))
                            {
                                using (var clds = cl.CreateDrawingSession())
                                {
                                    DrawSingleStroke(sender, clds, _currentStroke, style);
                                }
                                args.DrawingSession.DrawImage(cl, canvasRect, canvasRect, 1.0f, CanvasImageInterpolation.Linear, CanvasComposite.DestinationOut);
                            }
                        }
                        else
                        {
                            DrawSingleStroke(sender, args.DrawingSession, _currentStroke, style);
                        }
                    }
                }

                if (_isEraserMode && IsPixelEraserMode && _isHovering)
                {
                    // 定义颜色
                    Color innerColor = Colors.Black;
                    Color outerColor = Colors.White;

                    // 变色逻辑: 如果当前正在进行擦除操作 (鼠标按下且产生了 Stroke)
                    if (_currentStroke != null)
                    {
                        // 按下时变色：变成红色
                        innerColor = Colors.Red;
                        outerColor = Colors.White;
                    }

                    // 绘制光标
                    float radius = (float)EraserSize / 2.0f;

                    // 画一个内圈 (颜色变化)
                    args.DrawingSession.DrawCircle(_currentCursorPos, radius, innerColor, 1.0f);

                    // 画一个外圈 (保持白色，确保在深色背景可见)
                    // 半径稍微大一点点，形成描边效果
                    args.DrawingSession.DrawCircle(_currentCursorPos, radius + 1.0f, outerColor, 1.0f);
                }
            }
        }

        private void DrawSingleStroke(CanvasControl sender, CanvasDrawingSession ds, Stroke stroke, CanvasStrokeStyle style)
        {
            if (stroke.CachedGeometry != null)
            {
                ds.DrawGeometry(stroke.CachedGeometry, stroke.Color, stroke.Size, style);
            }
            else if (stroke.Points.Count > 1)
            {
                using (var builder = new CanvasPathBuilder(sender))
                {
                    builder.BeginFigure(stroke.Points[0]);
                    for (int i = 1; i < stroke.Points.Count; i++)
                    {
                        builder.AddLine(stroke.Points[i]);
                    }
                    builder.EndFigure(CanvasFigureLoop.Open);

                    using (var geometry = CanvasGeometry.CreatePath(builder))
                    {
                        ds.DrawGeometry(geometry, stroke.Color, stroke.Size, style);
                    }
                }
            }
            else if (stroke.Points.Count == 1)
            {
                ds.FillCircle(stroke.Points[0], stroke.Size / 2, stroke.Color);
            }
        }

        // 返回 bool 表示是否发生了擦除
        private bool TryErase(Vector2 eraserPos)
        {
            bool erased = false;
            // 使用设置的大小 (半径 = 直径 / 2)
            float eraserRadius = (float)EraserSize / 2.0f;

            // 倒序遍历
            for (int i = _allStrokes.Count - 1; i >= 0; i--)
            {
                var stroke = _allStrokes[i];
                // 不要删除橡皮擦自己的轨迹
                if (stroke.IsEraserStroke) continue;
                bool hit = false;

                // 如果点太少，直接回退到点检测
                if (stroke.Points.Count < 2)
                {
                    if (stroke.Points.Count == 1 && Vector2.Distance(stroke.Points[0], eraserPos) < eraserRadius)
                    {
                        hit = true;
                    }
                }
                else
                {
                    // 检测橡皮擦是否碰到任何一段“线段”
                    for (int j = 0; j < stroke.Points.Count - 1; j++)
                    {
                        var p1 = stroke.Points[j];
                        var p2 = stroke.Points[j + 1];

                        // 计算点到线段的距离
                        if (GetDistanceToSegment(eraserPos, p1, p2) < eraserRadius)
                        {
                            hit = true;
                            break; // 只要碰到一段，整根线就删掉
                        }
                    }
                }

                if (hit)
                {
                    // 记得释放资源
                    stroke.CachedGeometry?.Dispose();
                    _allStrokes.RemoveAt(i);
                    erased = true;
                    // 如果只想擦一根，这里可以 break；如果想一次擦多根，就继续
                    break;
                }
            }
            return erased;
        }

        // 计算点 p 到线段 ab 的最短距离
        private float GetDistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;

            // 计算投影比例 h，并限制在 [0, 1] 范围内（确保垂足在线段上）
            float h = Math.Clamp(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba), 0, 1);

            // 计算距离向量长度
            return (pa - ba * h).Length();
        }

        public void DeleteAllInk()
        {
            // 释放所有缓存
            foreach (var stroke in _allStrokes)
            {
                stroke.CachedGeometry?.Dispose();
            }
            _allStrokes.Clear();
            MyCanvas.Invalidate();
        }

        public void MoveViaMonitor(int indexMonitor)
        {
            var displays = DisplayArea.FindAll();
            if (indexMonitor >= displays.Count) indexMonitor = 0;

            DisplayArea display = displays[indexMonitor];
            var area = display.WorkArea;
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X, area.Y, area.Width, area.Height));
        }

        public void SetFullscreenAcrossAllDisplays()
        {
            var displays = DisplayArea.FindAll();

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            for (int i =0; i < displays.Count; i++)
            {
                var display = displays[i];
                // 改用 OuterBounds (包含任务栏区域)
                var bounds = display.OuterBounds;

                if (bounds.X < minX) minX = bounds.X;
                if (bounds.Y < minY) minY = bounds.Y;
                if (bounds.X + bounds.Width > maxX) maxX = bounds.X + bounds.Width;
                if (bounds.Y + bounds.Height > maxY) maxY = bounds.Y + bounds.Height;
            }

            // 窗口会变成一个跨越所有屏幕的巨大矩形
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(minX, minY, maxX - minX, maxY - minY));
        }

        private void EnterFullScreenMode()
        {
            var id = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWin = AppWindow.GetFromWindowId(id);
            appWin.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
    }
}