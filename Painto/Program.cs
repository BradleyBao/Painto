using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Painto
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            var instance = AppInstance.FindOrRegisterForKey("Painto.MainInstance");
            if (!instance.IsCurrent)
            {
                // Let the existing process take foreground focus on our behalf.
                AllowSetForegroundWindow(instance.ProcessId);

                // Redirect on an MTA thread so the STA entry thread does not
                // block the COM calls used by activation redirection.
                Task.Run(async () => await instance.RedirectActivationToAsync(activation))
                    .GetAwaiter().GetResult();
                return;
            }

            Application.Start(_ =>
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(dispatcher));
                var app = new App();
            });
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(uint processId);
    }
}
