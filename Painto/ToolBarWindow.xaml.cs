using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using Windows.UI;
using System.Collections.Generic;
using System;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using System.Runtime.InteropServices;
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
        }

        private List<Stroke> _allStrokes = new List<Stroke>();
        private Stroke _currentStroke;

        // 状态变量
        public static bool _isEraserMode = false;
        public static bool _computerMode = false;

        // 笔刷属性 (静态变量供外部修改)
        public static Color penColor = Colors.Black;
        public static int penThickness = 5;

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
                TryErase(vecPt);
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
            var vecPt = new Vector2((float)pt.X, (float)pt.Y);

            if (e.GetCurrentPoint(MyCanvas).Properties.IsLeftButtonPressed)
            {
                if (_isEraserMode)
                {
                    TryErase(vecPt);
                }
                else if (_currentStroke != null)
                {
                    _currentStroke.Points.Add(vecPt);
                    MyCanvas.Invalidate();
                }
            }
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

        private void MyCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            using (var style = new Microsoft.Graphics.Canvas.Geometry.CanvasStrokeStyle())
            {
                style.StartCap = CanvasCapStyle.Round;
                style.EndCap = CanvasCapStyle.Round;
                style.LineJoin = CanvasLineJoin.Round;

                foreach (var stroke in _allStrokes)
                {
                    // 如果有缓存的几何体（之前的笔画），直接画！极快！不费内存！
                    if (stroke.CachedGeometry != null)
                    {
                        args.DrawingSession.DrawGeometry(stroke.CachedGeometry, stroke.Color, stroke.Size, style);
                    }
                    // 如果没有缓存（当前正在画的这一笔），才动态计算
                    else if (stroke.Points.Count > 1)
                    {
                        // 这里只计算当前正在画的一笔，负担极小
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
                                args.DrawingSession.DrawGeometry(geometry, stroke.Color, stroke.Size, style);
                            }
                        }
                    }
                    else if (stroke.Points.Count == 1)
                    {
                        args.DrawingSession.FillCircle(stroke.Points[0], stroke.Size / 2, stroke.Color);
                    }
                }
            }
        }

        private void TryErase(Vector2 eraserPos)
        {
            bool needsRedraw = false;
            for (int i = _allStrokes.Count - 1; i >= 0; i--)
            {
                var stroke = _allStrokes[i];
                foreach (var p in stroke.Points)
                {
                    if (Vector2.Distance(p, eraserPos) < 20)
                    {
                        _allStrokes.RemoveAt(i);
                        needsRedraw = true;
                        break;
                    }
                }
            }
            if (needsRedraw)
            {
        }
                MyCanvas.Invalidate();
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