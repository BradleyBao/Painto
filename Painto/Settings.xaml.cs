using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Painto.Modules;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;
using Windows.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Painto
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Settings : Window
    {
        public ObservableCollection<DisplayInfo> Displays { get; } = new ObservableCollection<DisplayInfo>();
        private int selectedIndex = 0;
        private bool _isListening = false;
        private Button _listeningButton = null;
        private bool _isInitializing = false;
        public Settings()
        {
            this.InitializeComponent();

            this.Title = "Painto Setting";
            this.AppWindow.SetIcon("Assets/painto_logo.ico");

            // 获取窗口句柄
            IntPtr hwnd = WindowNative.GetWindowHandle(this);

            // 通过句柄获取 WindowId
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            // 获取 AppWindow 对象
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            Init();
            // 订阅 Closing 事件
            //appWindow.Closing += AppWindow_Closing;
        }

        //private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        //{
        //    // 处理关闭事件的逻辑
        //    this.Hide();
        //}

        public void Init()
        {
            // Setting 1: Monitor Related: Get All Monitors and Full Monitor
            GetAllDisplays();

            // Setting 2: UI related 
            SetupUI();

            // Setting 3: Language Related
            InitLanguageUI();

        }

        private string GetString(string key)
        {
            var resourceLoader = new ResourceLoader();
            return resourceLoader.GetString(key);
        }

        private void InitLanguageUI()
        {
            _isInitializing = true; // 【开始】锁定事件处理

            try
            {
                string currentLang = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;

                if (string.IsNullOrEmpty(currentLang))
                {
                    LanguageComboBox.SelectedIndex = 0;
                }
                else if (currentLang.StartsWith("en"))
                {
                    LanguageComboBox.SelectedIndex = 1;
                }
                else if (currentLang.StartsWith("zh"))
                {
                    LanguageComboBox.SelectedIndex = 2;
                }
                else
                {
                    LanguageComboBox.SelectedIndex = 0;
                }
            }
            finally
            {
                _isInitializing = false; // 【结束】无论如何都要解锁
            }
        }

        // 语言切换逻辑
        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 初始化时跳过
            if (_isInitializing) return;

            var combo = sender as ComboBox;
            if (combo == null || combo.SelectedItem == null) return;

            var item = combo.SelectedItem as ComboBoxItem;

            // 安全检查 Tag
            if (item.Tag == null) return;

            string langCode = item.Tag.ToString();
            string newLangCode = "";

            if (langCode == "Auto")
            {
                newLangCode = "";
            }
            else
            {
                newLangCode = langCode;
            }

            // 只有当语言真的改变了才提示 (避免重复点击相同选项触发)
            // 获取当前生效的语言设置，如果为空则默认为自动(空字符串)
            string currentSetting = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
            if (currentSetting == newLangCode) return;

            // 设置新语言
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = newLangCode;

            // 弹出重启提示
            ContentDialog dialog = new ContentDialog();

            // WinUI 3 必须设置 XamlRoot，通常使用当前窗口内容的 XamlRoot
            if (this.Content != null && this.Content.XamlRoot != null)
            {
                dialog.XamlRoot = this.Content.XamlRoot;
            }

            // 设置文本 (使用资源加载器)
            dialog.Title = GetString("RestartDialog/Title");
            dialog.Content = GetString("RestartDialog/Content");
            dialog.CloseButtonText = GetString("RestartDialog/CloseButtonText");

            // 设置样式 (可选，保持默认即可)
            dialog.DefaultButton = ContentDialogButton.Close;

            await dialog.ShowAsync();
        }

        public void GetAllDisplays()
        {
            // 获取所有连接的显示器
            var displays = DisplayArea.FindAll();
            // 获取应用程序的本地设置容器
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            // 从设置属性中获取MonitorIndex
            string MonitorIndex = localSettings.Values["Monitor"] as string;
            int monitorIndex = int.Parse(MonitorIndex);

            string MonitorFull = localSettings.Values["MonitorFull"] as string;
            int monitorFull = int.Parse(MonitorFull);
            bool _monitorFull = monitorFull != 0;

            for (int i = 0; i < displays.Count; i++) 
            {
                DisplayArea display = displays[i];
                // 获取显示器基本信息
                var isPrimary = display.IsPrimary;
                var workArea = display.WorkArea;
                bool isSelected = i == monitorIndex;
                //var dpi = display.GetDpi();

                SolidColorBrush bg = isSelected
                    ? new SolidColorBrush(Colors.LightBlue)
                    : new SolidColorBrush(Colors.LightGray);

                Displays.Add(new DisplayInfo
                {
                    ID = i+1,
                    DisplayId = display.DisplayId.Value,
                    IsPrimary = isPrimary,
                    WorkArea = workArea,
                    BackgroundColor = bg,
                });

                // 输出调试信息
                //Debug.WriteLine($"Display {display.DisplayId}");
                //Debug.WriteLine($"  Primary: {isPrimary}");
                //Debug.WriteLine($"  Resolution: {workArea.Width}x{workArea.Height}");
                //Debug.WriteLine($"  Position: ({workArea.X}, {workArea.Y})");
                //Debug.WriteLine($"  DPI: {dpi}");
            }
            
            DisplayGridView.SelectedIndex = monitorIndex;

            if (_monitorFull)
            {
                FullMonitorStatus.IsOn = true;
            }
        }

        public void SetupUI()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            string toolbarCollapse = localSettings.Values["IsToolBarCollapse"] as string;
            int istoolbarCollapse = int.Parse(toolbarCollapse);
            bool istoolbarCollapsed = istoolbarCollapse != 0;
            if (istoolbarCollapsed)
            {
                ToolBarCollapsed.IsOn = true;
            }

            // Hotkeys UI 
            string hotkeysEnabled = localSettings.Values["GlobalHotkeysEnabled"] as string;
            if (hotkeysEnabled == "1")
            {
                GlobalHotkeysSwitch.IsOn = true;
            } else
            {
                GlobalHotkeysSwitch.IsOn = false;
            }

            InitHotkeysUI();
        }

        private void InitHotkeysUI()
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            // 1. 初始化全局开关
            string hotkeysEnabled = localSettings.Values["GlobalHotkeysEnabled"] as string;
            GlobalHotkeysSwitch.IsOn = (hotkeysEnabled == "1");

            // 2. 初始化笔刷修饰键 (0=Ctrl, 1=Alt)
            string penMod = localSettings.Values["Hotkey_Pen_Mod"] as string;
            if (!string.IsNullOrEmpty(penMod))
                PenModifierCombo.SelectedIndex = int.Parse(penMod);
            else
                PenModifierCombo.SelectedIndex = 0; // 默认 Ctrl

            // 3. 初始化按钮文本
            UpdateShortcutButtonText(BtnSetDraw, "Draw", "Alt + B");
            UpdateShortcutButtonText(BtnSetEraser, "Eraser", "Alt + E");
            UpdateShortcutButtonText(BtnSetComputer, "Computer", "Alt + C");
            UpdateShortcutButtonText(BtnSetClear, "Clear", GetString("NoneText"));
        }

        private void UpdateShortcutButtonText(Button btn, string tag, string defaultText)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (settings.ContainsKey($"Hotkey_{tag}_Key"))
            {
                int vKey = (int)settings[$"Hotkey_{tag}_Key"];
                int mod = (int)settings[$"Hotkey_{tag}_Mod"];
                btn.Content = GetKeyString(mod, vKey);
            }
            else
            {
                // 如果 defaultText 是 "None"，则尝试加载翻译
                if (defaultText == "None")
                {
                    btn.Content = GetString("NoneText");
                }
                else
                {
                    btn.Content = defaultText;
                }
            }
        }

        // 点击按钮进入“监听模式”
        private void BtnSetShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (App.m_window != null)
            {
                App.m_window.UnregisterAppHotkeys();
            }

            var btn = sender as Button;
            if (_isListening)
            {
                // 如果点击了其他按钮，取消之前的监听
                CancelListening();
            }

            _isListening = true;
            _listeningButton = btn;
            btn.Content = GetString("ListeningText");

            // 注册 KeyDown 事件来捕获按键
            // 注意：WinUI 3 Window 级别的按键监听比较特殊，我们在 Grid 或者 Content 上监听
            // 这里我们利用 Button 获得焦点后的 KeyDown
            btn.KeyDown += ShortcutButton_KeyDown;
            btn.LostFocus += ShortcutButton_LostFocus;
        }

        private void ShortcutButton_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isListening)
            {
                CancelListening();
            }
        }

        private void CancelListening()
        {
            if (_listeningButton != null)
            {
                string tag = _listeningButton.Tag.ToString();
                UpdateShortcutButtonText(_listeningButton, tag, GetString("ErrorText"));

                _listeningButton.KeyDown -= ShortcutButton_KeyDown;
                _listeningButton.LostFocus -= ShortcutButton_LostFocus;
                _listeningButton = null;
            }
            _isListening = false;

            if (GlobalHotkeysSwitch.IsOn && App.m_window != null)
            {
                App.m_window.RegisterAppHotkeys();
            }
        }

        private void ShortcutButton_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_isListening || _listeningButton == null) return;

            e.Handled = true;
            var vKey = e.Key;

            // 忽略单纯的修饰键按下 (比如只按了 Ctrl 还没按字母)
            if (vKey == VirtualKey.Control || vKey == VirtualKey.Menu || vKey == VirtualKey.Shift || vKey == VirtualKey.LeftWindows || vKey == VirtualKey.RightWindows)
            {
                return;
            }

            if (vKey == VirtualKey.Escape)
            {
                CancelListening();
                return;
            }

            // 获取修饰键状态
            // WinUI 的 GetCurrentKeyState 可以检查
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
            var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

            bool isCtrl = (ctrl & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            bool isAlt = (alt & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            bool isShift = (shift & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            // 如果按下了 ESC，视为取消或清除（这里根据你的需求，设为清除或取消）
            if (vKey == VirtualKey.Escape)
            {
                CancelListening();
                return;
            }

            // 计算 Mod ID (Win32 RegisterHotKey format: Alt=1, Ctrl=2, Shift=4)
            int win32Mod = 0;
            if (isAlt) win32Mod |= 1;
            if (isCtrl) win32Mod |= 2;
            if (isShift) win32Mod |= 4;

            // 保存设置
            string tag = _listeningButton.Tag.ToString();
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[$"Hotkey_{tag}_Key"] = (int)vKey;
            localSettings.Values[$"Hotkey_{tag}_Mod"] = win32Mod;

            // 更新 UI
            _listeningButton.Content = GetKeyString(win32Mod, (int)vKey);

            // 结束监听
            _listeningButton.KeyDown -= ShortcutButton_KeyDown;
            _listeningButton.LostFocus -= ShortcutButton_LostFocus;
            _listeningButton = null;
            _isListening = false;

            if (App.m_window != null)
            {
                App.m_window.ReloadHotkeys();
            }
        }

        private string GetKeyString(int mod, int vKey)
        {
            string str = "";
            if ((mod & 2) != 0) str += "Ctrl + ";
            if ((mod & 1) != 0) str += "Alt + ";
            if ((mod & 4) != 0) str += "Shift + ";

            str += ((VirtualKey)vKey).ToString();
            return str;
        }

        private void PenModifierCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;

            var localSettings = ApplicationData.Current.LocalSettings;
            // 保存 index: 0 = Ctrl, 1 = Alt
            localSettings.Values["Hotkey_Pen_Mod"] = combo.SelectedIndex.ToString();

            if (App.m_window != null)
            {
                App.m_window.ReloadHotkeys();
            }
        }

        public DisplayArea GetCurrentWindowDisplay(Window window)
        {
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            return DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        }

        private void SetMonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = DisplayGridView.SelectedIndex;
            bool isFullMonitor = FullMonitorStatus.IsOn;
            App.m_window.MoveWindowFromMonitor(selectedIndex, isFullMonitor);
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Monitor"] = selectedIndex.ToString();

            if (isFullMonitor)
            {
                localSettings.Values["MonitorFull"] = "1";
            } else
            {
                localSettings.Values["MonitorFull"] = "0";
            }

            for (int i = 0; i < Displays.Count; i++)
            {
                if (Displays[i].ID - 1 == selectedIndex)
                {
                    Displays[i].BackgroundColor = new SolidColorBrush(Colors.LightBlue);
                }
                else
                {
                    Displays[i].BackgroundColor = new SolidColorBrush(Colors.LightGray);
                }
            }

            
        }

        private void ToolBarCollapsed_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            var toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                bool isOn = toggleSwitch.IsOn;
                if (isOn)
                {
                    App.m_window.IsToolBarCollapse = true;
                    localSettings.Values["IsToolBarCollapse"] = "1";
                    
                } else
                {
                    App.m_window.IsToolBarCollapse = false;
                    localSettings.Values["IsToolBarCollapse"] = "0";
                }

                App.m_window.SetCollapsed(isOn);
            }
        }

        // Global Shortcut Key Toggled Event 
        private void GlobalHotkeysSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            var toggleSwitch = sender as ToggleSwitch;

            if (toggleSwitch != null)
            {
                if (toggleSwitch.IsOn)
                {
                    localSettings.Values["GlobalHotkeysEnabled"] = "1";
                    App.m_window.RegisterAppHotkeys();
                }
                else
                {
                    localSettings.Values["GlobalHotkeysEnabled"] = "0";
                    App.m_window.UnregisterAppHotkeys();
                }
            }
        }

        // Test Method
        //private void FullMonitorStatus_Toggled(object sender, RoutedEventArgs e)
        //{
        //    var toggleSwitch = sender as ToggleSwitch;
        //    if (toggleSwitch != null)
        //    {
        //        bool isOn = toggleSwitch.IsOn;  // 获取开关的状态
        //                                        // 处理状态变化
        //        if (isOn)
        //        {
        //            // 开关被打开
        //            App.m_window.SetToolBarFullMonitor();
        //        }
        //        else
        //        {
        //            // 开关被关闭
        //            Console.WriteLine("Toggle is OFF");
        //        }
        //    }
        //}
    }
}
