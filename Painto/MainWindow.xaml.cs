using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using System.Collections.ObjectModel;
using Painto.Modules;
using Windows.System;
using Windows.Storage;
using Newtonsoft.Json;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Display;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Painto
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WinUIEx.WindowEx
    {
        // Child Window - Toolbar 
        private ToolBarWindow _toolbarWindow;
        private int _currentWindowWidth;
        private uint dpiWindow;

        // Child Window - Setting
        private static Settings _settings;
        public int monitorIndex = 0;
        public bool monitorFull = false;

        // Transparent + Click Through 
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;

        private const int WS_CAPTION = 0x00C00000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_DLGFRAME = 0x00400000;

        private const int DWMWA_NCRENDERING_POLICY = 2;
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        private const int DWMNCRP_DISABLED = 1;

        private const int PEN_HOTKEY_BASE_ID = 2000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("dwmapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);


        public ObservableCollection<PenData> PenItems { get; set; }

        //public IntPtr hwnd;
        public bool EnterInAnimation = false;
        public bool ExitInAnimation = false;
        public bool IsToolBarCollapse = false;

        public MainWindow()
        {
            this.InitializeComponent();
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinUIEx.HwndExtensions.SetAlwaysOnTop(hwnd, true);
            dpiWindow = GetDpiForWindow(hwnd);
            int extendedStyle = GetWindowLong(hwnd, GWL_STYLE);

            // ! IMPORTANT Click Through
            //_ = SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            //_ = SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            this.Title = "Painto";
            this.AppWindow.SetIcon("Assets/painto_logo.ico");

            RemoveTitleBarAndBorder(hwnd);
            RemoveWindowShadow(hwnd);
            //SetWindowTopMost(hwnd, true);

            //EnterFullScreenMode();

            Init();

            // Create Toolbar 
        }

        private void Init()
        {
            MajorFunctionControl.SelectedIndex = 1;
            var selectedItem = (GridViewItem)MajorFunctionControl.ContainerFromIndex(1);
            selectedItem?.Focus(FocusState.Programmatic);

            penControl.DisableWindowControl += DisableToolBarControl;
            penControl.SaveData += PenControl_SaveData;
            penControl.SwitchBackDrawControl += PenControl_SwitchBackDrawControl;
            ControlPanel.LayoutUpdated += ControlPanel_LayoutUpdated;

            LoadSetting();
            InitWindow();
            SourceInitialized();
            InitPens();
            InitHotkeys();
            //LoadSetting();
            //AdaptWindowLocation();
        }

        private void LoadSetting()
        {
            // 获取应用程序的本地设置容器
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            // 从设置属性中获取MonitorIndex
            string MonitorIndex = localSettings.Values["Monitor"] as string;

            // 如果 JSON 字符串存在，则反序列化为 ObservableCollection<PenData> 对象
            if (!string.IsNullOrEmpty(MonitorIndex))
            {
                monitorIndex = int.Parse(MonitorIndex); 
            }
            else
            {
                monitorIndex = 0;
                localSettings.Values["Monitor"] = "0";
            }

            // 设置FullMonitor
            string IsMonitorFull = localSettings.Values["MonitorFull"] as string;
            if (!string.IsNullOrEmpty(IsMonitorFull))
            {
                monitorFull = Convert.ToBoolean(int.Parse(IsMonitorFull));
            }
            else
            {
                monitorFull = false;
                localSettings.Values["MonitorFull"] = "0";
            }

            // 设置ToolBar是否自动Collapse
            string isToolBarCollapsed = localSettings.Values["IsToolBarCollapse"] as string;
            if (!string.IsNullOrEmpty(isToolBarCollapsed))
            {
                IsToolBarCollapse = Convert.ToBoolean(int.Parse(isToolBarCollapsed));
            } else
            {
                IsToolBarCollapse = false;
                localSettings.Values["IsToolBarCollapse"] = "0";
            }

        }

        private void PenControl_SwitchBackDrawControl(object sender, EventArgs e)
        {
            MajorFunctionControl.SelectedIndex = 1;
            var selectedItem = (GridViewItem)MajorFunctionControl.ContainerFromIndex(1);
            selectedItem?.Focus(FocusState.Programmatic);
            ToolBarWindow._computerMode = false;
            ToolBarWindow._isEraserMode = false;
            ToolBarWindow.LockScreen();
        }

        [DllImport("User32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);


        private void PenControl_SaveData(object sender, EventArgs e)
        {
            SavePenItems(penControl.ItemsSource);

            // Reload Hotkey 
            ReloadHotkeys();
        }

        private void InitWindow()
        {
            // Toolbar Window
            _toolbarWindow = new ToolBarWindow();
            IntPtr mainHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            IntPtr toolbarHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_toolbarWindow);


            // 将 ToolBarWindow 设置为 MainWindow 的子窗体
            //SetOwner(toolbarHwnd, mainHwnd);
            _toolbarWindow.Activate();

            // Setting Window
            //_settings = new Settings();
            _toolbarWindow.MoveViaMonitor(monitorIndex);
            if (this.monitorFull)
            {
                _toolbarWindow.SetFullscreenAcrossAllDisplays();
            }

            SetCollapsed(this.IsToolBarCollapse);
        }

        public void SetCollapsed(bool status)
        {
            if (status)
            {
                penControl.Visibility = Visibility.Collapsed;
                SubFunctionControl.Visibility = Visibility.Collapsed;
                
            } else
            {
                penControl.Visibility = Visibility.Visible;
                SubFunctionControl.Visibility = Visibility.Visible;
                penControl.Opacity = 1;
                SubFunctionControl.Opacity = 1;
            }
        }

        private void DisableToolBarControl(object sender, EventArgs e)
        {
            // 强制切换至电脑模式
            MajorFunctionControl.SelectedIndex = 0;
            var selectedItem = (GridViewItem)MajorFunctionControl.ContainerFromIndex(0);
            selectedItem?.Focus(FocusState.Programmatic);
            ToolBarWindow._computerMode = true;
            ToolBarWindow.UnlockScreen();
            
        }

        public static void SetWindowTopMost(IntPtr hwnd, bool topMost)
        {
            SetWindowPos(hwnd, topMost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
        }

        private void RequestRestoreWindow(object sender, EventArgs e)
        {
            AdaptWindowLocation();
        }

        private void ControlPanel_LayoutUpdated(object sender, object e)
        {
            if (ControlPanel.ActualWidth > 0 && ControlPanel.ActualHeight > 0)
            {
                AdaptWindowLocation();
            }
        }

        public void AdaptWindowLocation()
        {
            //IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            //var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            //var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            //int screenHeight = displayArea.WorkArea.Height;

            var displays = DisplayArea.FindAll();
            DisplayArea display = displays[monitorIndex];
            var displayArea = display.WorkArea;
            int screenHeight = displayArea.Height;
            int screenX = displayArea.X;
            int screenY = displayArea.Y;
            int taskbarHeight = GetTaskbarHeight();

            // 根据 DPI 更改窗口大小
            int controlPanelWidth = (int)(ControlPanel.ActualWidth * dpiWindow / 96.0);
            int controlPanelHeight = (int)(ControlPanel.ActualHeight * dpiWindow / 96.0); 
            //this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(5 + PenItems.Count, screenHeight, controlPanelWidth, controlPanelHeight));
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(5 + screenX, screenHeight - taskbarHeight - 5, controlPanelWidth, controlPanelHeight));
        }

        public void MoveWindowFromMonitor(int indexMonitor, bool isFullMonitor)
        {
            this.monitorFull = isFullMonitor;
            var displays = DisplayArea.FindAll();
            if (indexMonitor < displays.Count)
            {
                this.monitorIndex = indexMonitor;
                DisplayArea display = displays[indexMonitor];
                var displayArea = display.WorkArea;
                int screenHeight = displayArea.Height;
                int screenX = displayArea.X;
                int taskbarHeight = GetTaskbarHeight();

                // 根据 DPI 更改窗口大小
                int controlPanelWidth = (int)(ControlPanel.ActualWidth * dpiWindow / 96.0);
                int controlPanelHeight = (int)(ControlPanel.ActualHeight * dpiWindow / 96.0);
                //this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(5 + PenItems.Count, screenHeight, controlPanelWidth, controlPanelHeight));
                this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(screenX + 5, screenHeight - taskbarHeight - 5, controlPanelWidth, controlPanelHeight));
                if (this.monitorFull)
                {
                    _toolbarWindow.SetFullscreenAcrossAllDisplays();
                } else
                {
                    _toolbarWindow.MoveViaMonitor(indexMonitor);
                }
            }
        }

        public void SetToolBarFullMonitor()
        {
            _toolbarWindow.SetFullscreenAcrossAllDisplays();
        }

        public void AdaptWindowLocation(int height)
        {
            var displays = DisplayArea.FindAll();
            DisplayArea display = displays[monitorIndex];
            var displayArea = display.WorkArea;
            int screenHeight = displayArea.Height;
            int screenX = displayArea.X;
            int screenY = displayArea.Y;


            //IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            //var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            //var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            //int screenHeight = displayArea.WorkArea.Height;
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(5 + PenItems.Count + screenX, screenHeight - height, 300 + PenItems.Count * 15, height));
        }

        private void SourceInitialized()
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            IntPtr toolbarHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_toolbarWindow);

            // Set Ownership
            SetWindowLong(hwnd, WindowLongFlags.GWL_HWNDPARENT, toolbarHwnd);
        }

        private Microsoft.UI.Windowing.AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);
        }

        // Fullscreen 
        private void EnterFullScreenMode()
        {
            var m_appWindow = GetAppWindowForCurrentWindow();
            m_appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        //[DllImport("user32.dll", SetLastError = false)]
        //private static extern IntPtr SetParent(IntPtr child, IntPtr parent);


        // Set Ownership

        //[DllImport("user32.dll", SetLastError = true)]
        //private static extern IntPtr SetWindowLong(IntPtr hWnd, WindowLongFlags nIndex, IntPtr dwNewLong);
        // P/Invoke declaration for FindWindow
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        // P/Invoke declaration for GetWindowRect
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        // Structure to hold the rectangle coordinates
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Find the taskbar and get its height
        public static int GetTaskbarHeight()
        {
            // Find the taskbar window (ClassName "Shell_TrayWnd")
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);

            // If taskbar is found, get its rectangle
            if (taskbarHandle != IntPtr.Zero)
            {
                RECT rect;
                if (GetWindowRect(taskbarHandle, out rect))
                {
                    // The height of the taskbar is the difference between Top and Bottom coordinates
                    return rect.Bottom - rect.Top;
                }
            }

            return 0; // If taskbar is not found, return 0
        }

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, WindowLongFlags nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, WindowLongFlags nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLong(IntPtr hWnd, WindowLongFlags nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) // Check if we're running in a 64-bit environment
            {
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }
            else // 32-bit environment
            {
                return SetWindowLong32(hWnd, nIndex, dwNewLong);
            }
        }

        private enum WindowLongFlags
        {
            GWL_HWNDPARENT = -8
        }

        internal void InitPens()
        {
            PenItems = LoadPenItems();

            // If not empty 
            if (PenItems.Count <= 0)
            {
                // Default Value
                PenItems = new ObservableCollection<PenData>
                {
                    new PenData { PenColor = Colors.Black, Thickness = 5, penType = "Normal", Icon = "\uEE56", PenColorString = Colors.Black.ToString()},
                    new PenData { PenColor = Color.FromArgb(255,30,115,199), Thickness = 5, penType = "Normal", Icon = "\uEE56", PenColorString = Color.FromArgb(255,30,115,199).ToString()},
                    new PenData { PenColor = Color.FromArgb(255,235,59,74), Thickness = 5, penType = "Normal", Icon = "\uEE56", PenColorString = Color.FromArgb(255,235,59,74).ToString()}
                };
            }

            penControl.ItemsSource = PenItems;
            ToolBarWindow.penColor = PenItems[0].PenColor;
            ToolBarWindow.penThickness = PenItems[0].Thickness;
            SavePenItems(PenItems);
        }

        public void SavePenItems(ObservableCollection<PenData> penItems)
        {
            // 序列化 ObservableCollection<PenData> 对象为 JSON 字符串
            string penItemsJson = JsonConvert.SerializeObject(penItems);

            // 获取应用程序的本地设置容器
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            // 存储 JSON 字符串到设置属性中
            localSettings.Values["PenItems"] = penItemsJson;
        }

        public ObservableCollection<PenData> LoadPenItems()
        {
            // 获取应用程序的本地设置容器
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            // 从设置属性中获取 JSON 字符串
            string penItemsJson = localSettings.Values["PenItems"] as string;

            // 如果 JSON 字符串存在，则反序列化为 ObservableCollection<PenData> 对象
            if (!string.IsNullOrEmpty(penItemsJson))
            {
                var penItems = JsonConvert.DeserializeObject<ObservableCollection<PenData>>(penItemsJson);
                return penItems;
            }

            // 如果没有存储的 PenItems 数据，则返回一个空的集合或 null
            return new ObservableCollection<PenData>();
        }

        private void RemoveTitleBarAndBorder(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_BORDER | WS_DLGFRAME);
            SetWindowLong(hwnd, GWL_STYLE, style);
        }

        private void RemoveWindowShadow(IntPtr hwnd)
        {
            int value = DWMNCRP_DISABLED;
            DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref value, sizeof(int));

            value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, sizeof(int));
        }

        private void AddNewPen()
        {
            PenData new_penData = new PenData
            {
                PenColor = Colors.Black,
                Thickness = 5,
                penType = "Normal",
                Icon = "\uEE56",
                PenColorString = Colors.Black.ToString()
            };

            PenItems.Add(new_penData);
            penControl.ItemsSource = PenItems;
            SavePenItems(PenItems); 

            ReloadHotkeys();
        }

        private void DeletePen()
        {
            var item = penControl.globalClickedItem;
            PenItems.Remove(item);
            penControl.ItemsSource = PenItems;
            SavePenItems(PenItems);
            
        }

        private void DeleteAllInks()
        {
            _toolbarWindow.DeleteAllInk();
        }

        // Win32 API 常量
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOREDRAW = 0x0080;
        private const int SWP_NOZORDER = 0x0004;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private void RootTool_SelectItem(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FontIcon clickedItem)
            {
                // 处理点击事件，并访问 tappedItem 对象
                //var deviceName = tappedItem.deviceName;
                //var deviceIP = tappedItem.deviceIP;
                // 执行其他操作
                switch (clickedItem.Tag)
                {
                    case "Eraser":
                        ToolBarWindow._isEraserMode = true;
                        if (ToolBarWindow._computerMode)
                        {
                            ToolBarWindow.LockScreen();
                            ToolBarWindow._computerMode = false;
                        }
                        break;

                    case "DrawMode":
                        ToolBarWindow._isEraserMode = false;
                        if (ToolBarWindow._computerMode)
                        {
                            ToolBarWindow.LockScreen();
                            ToolBarWindow._computerMode = false;
                        }
                        
                        break;

                    case "ComputerMode":
                        if (!ToolBarWindow._computerMode)
                        {
                            ToolBarWindow._computerMode = true;
                            ToolBarWindow.UnlockScreen();
                        }
                        
                        break;

                    default:
                        ToolBarWindow._isEraserMode = false;
                        ToolBarWindow._computerMode = false;
                        break;
                }
                
            }
        }
        private delegate void CloseAppDelegate();

        private void SubFunctionControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FontIcon clickedItem)
            {
                switch (clickedItem.Tag)
                {
                    case "AddPen":
                        AddNewPen();
                        break;

                    case "RemovePen":
                        DeletePen();
                        break;

                    case "CloseApp":
                        APPShutDown();
                        break;

                    case "EraseAll":
                        DeleteAllInks();
                        break;

                    case "Settings":
                        OpenSetting();
                        break;

                    default:
                        
                        break;
                }
            }
        }

        private void OpenSetting()
        {
            // 强制切换至电脑模式
            MajorFunctionControl.SelectedIndex = 0;
            var selectedItem = (GridViewItem)MajorFunctionControl.ContainerFromIndex(0);
            selectedItem?.Focus(FocusState.Programmatic);
            ToolBarWindow._computerMode = true;
            ToolBarWindow.UnlockScreen();

            if (_settings != null)
            {
                // 窗口已存在，激活窗口
                _settings.Activate();
                return;
            }
            
            // 创建新窗口实例
            _settings = new Settings();
            // 订阅窗口的 Closed 事件
            _settings.Closed += Settings_Closed;
            // 激活窗口
            _settings.Activate();
        }

        private void MajorFunctionControl_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!IsToolBarCollapse) { return; }

            penControl.Visibility = Visibility.Visible;
            SubFunctionControl.Visibility = Visibility.Visible;
            ExpandCollapseAnimation.Begin();
            this.EnterInAnimation = true;

            ExpandCollapseAnimation.Completed += (s, args) =>
            {
                this.EnterInAnimation = false;
            };
        }

        private void MajorFunctionControl_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!IsToolBarCollapse) { return; }
            // 开始反向动画，注意这里没有立即隐藏控件
            //ExpandCollapseAnimation.Seek(TimeSpan.Zero);  // 如果需要立即反向播放
            CollapseCollapseAnimation.Begin();
            this.ExitInAnimation = true;

            // 使用 Storyboard 的 Completed 事件延迟改变 Visibility
            CollapseCollapseAnimation.Completed += (s, args) =>
            {
                this.ExitInAnimation = false;
                if (!EnterInAnimation) {
                    // 动画完成后再改变 Visibility
                    penControl.Visibility = Visibility.Collapsed;
                    SubFunctionControl.Visibility = Visibility.Collapsed;
                }
            };
        }

        #region Global Hotkeys Logic 

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // 32-bit 
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private const int GWLP_WNDPROC = -4;
        private const int WM_HOTKEY = 0x0312;

        private const int HOTKEY_ID_PEN1 = 1001;
        private const int HOTKEY_ID_PEN2 = 1002;
        private const int HOTKEY_ID_PEN3 = 1003;
        private const int HOTKEY_ID_DRAW = 1004; // 'B'
        private const int HOTKEY_ID_ERASER = 1005; // 'E'
        private const int HOTKEY_ID_COMPUTER = 1006; // 'C' or 'Esc'

        // 存储旧的窗口处理函数指针
        private IntPtr _oldWndProc = IntPtr.Zero;

        // 委托保持引用，防止被 GC 回收
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate _newWndProc;

        public bool IsGlobalHotkeysEnabled { get; private set; } = false;

        private void InitHotkeys()
        {
            // 注册全局热键的逻辑
            // 读取设置
            var localSettings = ApplicationData.Current.LocalSettings;
            string enabled = localSettings.Values["GlobalHotkeysEnabled"] as string;

            // 挂钩消息循环 (只需做一次)
            if (_oldWndProc == IntPtr.Zero)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                _newWndProc = new WndProcDelegate(WindowProcess);
                // 根据位数选择调用
                if (IntPtr.Size == 8)
                    _oldWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
                else
                    _oldWndProc = SetWindowLong32(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
            }

            // 如果设置开启，则注册
            if (enabled == "1")
            {
                RegisterAppHotkeys();
            }
        }

        public void RegisterAppHotkeys()
        {
            // 检查开关
            var localSettings = ApplicationData.Current.LocalSettings;
            string enabled = localSettings.Values["GlobalHotkeysEnabled"] as string;
            if (enabled != "1") return;

            if (IsGlobalHotkeysEnabled) return;

            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // 注册功能键 (读取设置，如果不存在则使用默认值)
            RegisterSingleHotkey(hwnd, "Draw", HOTKEY_ID_DRAW, 0x0001, (uint)Windows.System.VirtualKey.B); // Default: Alt + B
            RegisterSingleHotkey(hwnd, "Eraser", HOTKEY_ID_ERASER, 0x0001, (uint)Windows.System.VirtualKey.E); // Default: Alt + E
            RegisterSingleHotkey(hwnd, "Computer", HOTKEY_ID_COMPUTER, 0x0001, (uint)Windows.System.VirtualKey.C); // Default: Alt + C
            RegisterSingleHotkey(hwnd, "Clear", HOTKEY_ID_CLEAR, 0, 0); // Default: None

            // 动态注册笔刷键
            // 获取笔刷修饰键设置 (0=Ctrl, 1=Alt)
            string penModStr = localSettings.Values["Hotkey_Pen_Mod"] as string;
            int penModIndex = 0;
            if (!string.IsNullOrEmpty(penModStr)) penModIndex = int.Parse(penModStr);

            uint penMod = (penModIndex == 1) ? 0x0001u : 0x0002u; // 1=Alt, 2=Ctrl

            // 遍历当前拥有的笔刷数量 (PenItems)
            if (PenItems != null)
            {
                for (int i = 0; i < PenItems.Count; i++)
                {
                    // 限制一下，因为键盘数字键通常只有 0-9。
                    // VirtualKey.Number1 是 49
                    if (i > 9) break;

                    uint key = (uint)Windows.System.VirtualKey.Number1 + (uint)i;
                    // ID 从 1000 开始 + index
                    RegisterHotKey(hwnd, PEN_HOTKEY_BASE_ID + i, penMod, key);
                }
            }

            IsGlobalHotkeysEnabled = true;
        }

        // 辅助方法：注册单个热键，优先读取设置
        private void RegisterSingleHotkey(IntPtr hwnd, string tag, int id, uint defaultMod, uint defaultKey)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            uint mod = defaultMod;
            uint key = defaultKey;

            if (settings.ContainsKey($"Hotkey_{tag}_Key"))
            {
                key = (uint)(int)settings[$"Hotkey_{tag}_Key"];
                mod = (uint)(int)settings[$"Hotkey_{tag}_Mod"];
            }

            // 如果 key 为 0，说明未设置
            if (key != 0)
            {
                RegisterHotKey(hwnd, id, mod, key);
            }
        }

        public void UnregisterAppHotkeys()
        {
            if (!IsGlobalHotkeysEnabled) return;

            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // 注销功能键
            UnregisterHotKey(hwnd, HOTKEY_ID_DRAW);
            UnregisterHotKey(hwnd, HOTKEY_ID_ERASER);
            UnregisterHotKey(hwnd, HOTKEY_ID_COMPUTER);
            UnregisterHotKey(hwnd, HOTKEY_ID_CLEAR);

            // 注销笔刷键 (循环注销 0-9 号笔刷的 ID)
            for (int i = 0; i <= 9; i++)
            {
                UnregisterHotKey(hwnd, PEN_HOTKEY_BASE_ID + i);
            }

            // 清理旧的固定 ID (如果你之前的代码里有遗留)
            UnregisterHotKey(hwnd, HOTKEY_ID_PEN1);
            UnregisterHotKey(hwnd, HOTKEY_ID_PEN2);
            UnregisterHotKey(hwnd, HOTKEY_ID_PEN3);

            IsGlobalHotkeysEnabled = false;
        }

        // 自定义消息处理函数
        private IntPtr WindowProcess(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                HandleHotkey(id);
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private void HandleHotkey(int id)
        {
            // 处理笔刷 (ID 1000 - 1009)
            if (id >= PEN_HOTKEY_BASE_ID && id <= PEN_HOTKEY_BASE_ID + 9)
            {
                int penIndex = id - PEN_HOTKEY_BASE_ID;
                // 增加安全检查，防止索引越界
                if (penControl != null && penControl.ItemsSource != null && penIndex < penControl.ItemsSource.Count)
                {
                    penControl.SelectPen(penIndex);
                }
                return;
            }

            switch (id)
            {
                case HOTKEY_ID_DRAW:
                    SwitchToMode("DrawMode");
                    break;
                case HOTKEY_ID_ERASER:
                    SwitchToMode("Eraser");
                    break;
                case HOTKEY_ID_COMPUTER:
                    SwitchToMode("ComputerMode");
                    break;
                case HOTKEY_ID_CLEAR:
                    // 执行 Clear All
                    // 需要确保在 UI 线程
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        DeleteAllInks();
                    });
                    break;
            }
        }

        // 辅助方法：切换模式（从你之前的代码整理）
        public void SwitchToMode(string tag)
        {
            // 需要在 UI 线程执行，因为 WM_HOTKEY 可能来自系统调用
            this.DispatcherQueue.TryEnqueue(() =>
            {
                int index = 0;
                switch (tag)
                {
                    case "ComputerMode": index = 0; break;
                    case "DrawMode": index = 1; break;
                    case "Eraser": index = 2; break;
                }

                MajorFunctionControl.SelectedIndex = index;

                // 执行之前的 ItemClick 逻辑
                switch (tag)
                {
                    case "Eraser":
                        ToolBarWindow._isEraserMode = true;
                        if (ToolBarWindow._computerMode)
                        {
                            ToolBarWindow.LockScreen();
                            ToolBarWindow._computerMode = false;
                        }
                        break;

                    case "DrawMode":
                        ToolBarWindow._isEraserMode = false;
                        if (ToolBarWindow._computerMode)
                        {
                            ToolBarWindow.LockScreen();
                            ToolBarWindow._computerMode = false;
                        }
                        break;

                    case "ComputerMode":
                        if (!ToolBarWindow._computerMode)
                        {
                            ToolBarWindow._computerMode = true;
                            ToolBarWindow.UnlockScreen();
                        }
                        break;
                }
            });
        }

        private const int HOTKEY_ID_CLEAR = 1099;

        public void ReloadHotkeys()
        {
            UnregisterAppHotkeys();
            RegisterAppHotkeys();
        }

        #endregion


        private void Settings_Closed(object sender, WindowEventArgs args)
        {
            // 窗口关闭时，将引用置为空
            _settings = null;
        }

        // ShutDown: Close first then shutdown
        private void APPShutDown()
        {
            this.Close();
            App.Current.Exit();
        }
    }
}
