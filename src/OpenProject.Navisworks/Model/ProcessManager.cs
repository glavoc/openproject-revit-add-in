﻿using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OpenProjectNavisworks.Model
{
    public static class ProcessManager
    {
       
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void SetNavisAsWindowOwner(this Window window)
        {
            if (null == window) { return; }
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowInteropHelper helper = new WindowInteropHelper(window);
            helper.Owner = GetActivateWindow();
            window.Closing += SetActivateWindow;
        }

        private static void SetActivateWindow(object sender, CancelEventArgs e)
        {
            SetActivateWindow();
        }

        /// <summary>
        /// Set process revert use revit
        /// </summary>
        /// <returns></returns>
        public static bool SetActivateWindow()
        {
            IntPtr ptr = GetActivateWindow();
            if (ptr != IntPtr.Zero)
            {
                return SetForegroundWindow(ptr);
            }
            return false;
        }

        /// <summary>
        /// return active windows is active
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetActivateWindow()
        {
            return Process.GetCurrentProcess().MainWindowHandle;
        }
    }
}