using Microsoft.UI.Windowing;
using System;
using System.Runtime.InteropServices;

namespace Painto.Modules
{
    // WinUI3 can only apply a single DPI scale to a window's content, so a
    // window stretched across monitors with different scales renders content
    // sized for only one of them (e.g. only 2/3 of a monitor running at 150%
    // when the window's scale is picked from a 100% neighbor). There is no
    // supported way to make one window render correctly across mixed scales;
    // callers use this to warn the user rather than silently override them.
    public static class DisplayScaleHelper
    {
        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        private const int MDT_EFFECTIVE_DPI = 0;

        public static double GetScale(DisplayArea display)
        {
            IntPtr hMonitor = Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(display.DisplayId);
            if (hMonitor != IntPtr.Zero && GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
            {
                return dpiX / 96.0;
            }
            return 1.0;
        }

        // True if the connected displays don't all share the same DPI scale.
        public static bool HasMixedScales()
        {
            var displays = DisplayArea.FindAll();
            if (displays.Count <= 1) return false;

            double minScale = double.MaxValue;
            double maxScale = double.MinValue;

            for (int i = 0; i < displays.Count; i++)
            {
                double scale = GetScale(displays[i]);
                if (scale < minScale) minScale = scale;
                if (scale > maxScale) maxScale = scale;
            }

            return maxScale - minScale > 0.01;
        }

        // DisplayArea.FindAll() index of the display with the lowest DPI scale.
        // Used only to pick the default monitor on first run, before the user
        // has ever chosen one themselves.
        public static int GetLowestScaleMonitorIndex()
        {
            var displays = DisplayArea.FindAll();
            int lowestIndex = 0;
            double minScale = double.MaxValue;

            for (int i = 0; i < displays.Count; i++)
            {
                double scale = GetScale(displays[i]);
                if (scale < minScale)
                {
                    minScale = scale;
                    lowestIndex = i;
                }
            }

            return lowestIndex;
        }
    }
}
