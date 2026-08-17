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
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Painto
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // 设置 DPI 感知
            var dpiAwarenessContext = DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2;
            SetThreadDpiAwarenessContext(dpiAwarenessContext);

            // Set the thread context before creating any WinUI windows. Window
            // coordinates and XamlRoot scaling otherwise use the previous DPI
            // context when the primary monitor uses a non-100% scale.
            m_window = new MainWindow();
            m_window.Activate();
        }

        [DllImport("User32.dll")]
        static extern DPI_AWARENESS_CONTEXT SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext);

        public enum DPI_AWARENESS_CONTEXT
        {
            DPI_AWARENESS_CONTEXT_UNAWARE = 16,
            DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = 17,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = 18,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = 34,
        }

        public static MainWindow m_window;
    }
}
