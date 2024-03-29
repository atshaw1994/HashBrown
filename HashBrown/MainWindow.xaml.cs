﻿using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HashBrown
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker MD5worker = new();
        private readonly BackgroundWorker SHA1worker = new();
        private readonly BackgroundWorker SHA256worker = new();
        private readonly DoubleAnimation OpacityAnimation = new();
        private string FilePath;
        private string MD5Hash = "";
        private string SHA1Hash = "";
        private string SHA256Hash = "";
        private int ThemeSelection = 2;  // Set Chocolate.xaml as default theme

        public MainWindow()
        {
            InitializeComponent();
            FilePath = "";
            MD5worker.DoWork += MD5worker_DoWork!;
            MD5worker.RunWorkerCompleted += MD5worker_RunWorkerCompleted!;
            SHA1worker.DoWork += SHA1worker_DoWork!;
            SHA1worker.RunWorkerCompleted += SHA1worker_RunWorkerCompleted!;
            SHA256worker.DoWork += SHA256worker_DoWork!;
            SHA256worker.RunWorkerCompleted += SHA256worker_RunWorkerCompleted!;
            // Set Chocolate.xaml as current theme
            Application.Current.Resources.MergedDictionaries[0].Source = new Uri($"/Themes/Chocolate.xaml", UriKind.Relative);
            // Update icon to reflect changes
            ThemeButton_Glyph.Data = (FindResource("ThemeButton_Chocolate") as Geometry);
            // Update the Version display on AboutOverlay
            VersionDisplay.Text = Environment.Is64BitOperatingSystem
                ? $"Version: {Assembly.GetExecutingAssembly().GetName().Version!.ToString(2)}_{File.GetCreationTime(Assembly.GetExecutingAssembly().Location):yyyyMMdd}_x64"
                : $"Version: {Assembly.GetExecutingAssembly().GetName().Version!.ToString(2)}_{File.GetCreationTime(Assembly.GetExecutingAssembly().Location):yyyyMMdd}_x86";
        }

        #region Borderless
        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnCloseButtonClick(object sender, RoutedEventArgs e) => Close();

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Maximized:
                    CaptionBar_Border.CornerRadius = new CornerRadius(0);
                    maximizeButton.Visibility = Visibility.Collapsed;
                    restoreButton.Visibility = Visibility.Visible;
                    break;
                default:
                    CaptionBar_Border.CornerRadius = new CornerRadius(7, 7, 0, 0);
                    maximizeButton.Visibility = Visibility.Visible;
                    restoreButton.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e) => ((HwndSource)PresentationSource.FromVisual(this)).AddHook(HookProc);

        public static IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    MONITORINFO monitorInfo = new()
                    {
                        cbSize = Marshal.SizeOf(typeof(MONITORINFO))
                    };
                    GetMonitorInfo(monitor, ref monitorInfo);
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                    mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                }

                Marshal.StructureToPtr(mmi, lParam, true);
            }
            return IntPtr.Zero;
        }

        private const int WM_GETMINMAXINFO = 0x0024;

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                this.Left = left;
                this.Top = top;
                this.Right = right;
                this.Bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }
        #endregion

        private void MD5worker_DoWork(object sender, DoWorkEventArgs e) => MD5Hash = CalculateMD5(FilePath).ToUpper();

        private void MD5worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CalculatedMD5.Text = MD5Hash;
            HideLoadingOverlay();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void SHA1worker_DoWork(object sender, DoWorkEventArgs e) => SHA1Hash = CalculateSHA1(FilePath).ToUpper();

        private void SHA1worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CalculatedSHA1.Text = SHA1Hash;
            HideLoadingOverlay();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void SHA256worker_DoWork(object sender, DoWorkEventArgs e) => SHA256Hash = CalculateSHA256(FilePath).ToUpper();

        private void SHA256worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CalculatedSHA256.Text = SHA256Hash;
            HideLoadingOverlay();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new();
            if (ofd.ShowDialog() == true)
            {
                FilePath = ofd.FileName;
                OpenFileButton.Content = System.IO.Path.GetFileName(FilePath);
                PerformMD5.IsEnabled = true;
                PerformSHA1.IsEnabled = true;
                PerformSHA256.IsEnabled = true;
            }
        }

        static string CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }

        static string CalculateSHA1(string filename)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filename);
            var hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }

        static string CalculateSHA256(string filePath)
        {
            using SHA256 SHA256 = System.Security.Cryptography.SHA256.Create();
            using FileStream fileStream = File.OpenRead(filePath);
            return Convert.ToBase64String(SHA256.ComputeHash(fileStream));
        }

        private void ShowLoadingOverlay()
        {
            CalculatedMD5_Border.Visibility = Visibility.Visible;
            LoadingOverlay.Visibility = Visibility.Visible;
            baseTabControl.IsEnabled = false;
            OpacityAnimation.To = 0.8;
            OpacityAnimation.Duration = TimeSpan.FromSeconds(.25);
            OpacityAnimation.Completed += (s, a) =>
            {
                LoadingOverlay.Opacity = 0.8;
                PerformMD5.Visibility = Visibility.Collapsed;
                PerformSHA1.Visibility = Visibility.Collapsed;
                PerformSHA256.Visibility = Visibility.Collapsed;
            };
            LoadingOverlay.BeginAnimation(OpacityProperty, OpacityAnimation);
        }

        private void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            PerformMD5.Visibility = Visibility.Visible;
            PerformSHA1.Visibility = Visibility.Visible;
            PerformSHA256.Visibility = Visibility.Visible;
            baseTabControl.IsEnabled = true;
            OpacityAnimation.To = 0.0;
            OpacityAnimation.Duration = TimeSpan.FromSeconds(.25);
            OpacityAnimation.Completed += (s, a) => LoadingOverlay.Opacity = 0.0;
            LoadingOverlay.BeginAnimation(OpacityProperty, OpacityAnimation);
        }

        private void PerformMD5_Click(object sender, RoutedEventArgs e)
        {
            ShowLoadingOverlay();
            MD5worker.RunWorkerAsync();
        }

        private void PerformSHA1_Click(object sender, RoutedEventArgs e)
        {
            ShowLoadingOverlay();
            SHA1worker.RunWorkerAsync();
        }

        private void PerformSHA256_Click(object sender, RoutedEventArgs e)
        {
            ShowLoadingOverlay();
            SHA256worker.RunWorkerAsync();
        }

        private void CheckMD5_Click(object sender, RoutedEventArgs e)
        {
            if (KnownMD5Input.Text is not null)
            {
                MD5Result_Border.Visibility = Visibility.Visible;
                CheckMD5Result.Visibility = Visibility.Visible;
                CheckMD5.Visibility = Visibility.Collapsed;
                CheckMD5Result.Text = KnownMD5Input.Text.ToUpper().Equals(CalculatedMD5.Text.ToUpper()) ? "Pass!" : "Fail!";
            }
        }

        private void CheckSHA1_Click(object sender, RoutedEventArgs e)
        {
            if (KnownSHA1Input.Text is not null)
            {
                SHA1Result_Border.Visibility = Visibility.Visible;
                CheckSHA1Result.Visibility = Visibility.Visible;
                CheckSHA1.Visibility = Visibility.Collapsed;
                CheckSHA1Result.Text = KnownSHA1Input.Text.ToUpper().Equals(CalculatedSHA1.Text.ToUpper()) ? "Pass!" : "Fail!";
            }
        }

        private void CheckSHA256_Click(object sender, RoutedEventArgs e)
        {
            if (KnownSHA256Input.Text is not null)
            {
                SHA256Result_Border.Visibility = Visibility.Visible;
                CheckSHA256Result.Visibility = Visibility.Visible;
                CheckSHA256.Visibility = Visibility.Collapsed;
                CheckSHA256Result.Text = KnownSHA256Input.Text.ToUpper().Equals(CalculatedSHA256.Text.ToUpper()) ? "Pass!" : "Fail!";
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // Make ThemeSelection variable roll around
            if (ThemeSelection < 2) ThemeSelection++;
            else ThemeSelection = 0;
            // Dark
            if (ThemeSelection == 0) 
            {
                // Set Dark.xaml as current theme
                Application.Current.Resources.MergedDictionaries[0].Source = new Uri($"/Themes/Dark.xaml", UriKind.Relative);
                // Update icon to reflect changes
                ThemeButton_Glyph.Data = (FindResource("ThemeButton_Dark") as Geometry);
            }
            // Light
            else if (ThemeSelection == 1) 
            {
                // Set Light.xaml as current theme
                Application.Current.Resources.MergedDictionaries[0].Source = new Uri($"/Themes/Light.xaml", UriKind.Relative);
                // Update icon to reflect changes
                ThemeButton_Glyph.Data = (FindResource("ThemeButton_Light") as Geometry);
            }
            // Chocolate
            else
            {
                // Set Chocolate.xaml as current theme
                Application.Current.Resources.MergedDictionaries[0].Source = new Uri($"/Themes/Chocolate.xaml", UriKind.Relative);
                // Update icon to reflect changes
                ThemeButton_Glyph.Data = (FindResource("ThemeButton_Chocolate") as Geometry);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e) => AboutOverlay.Visibility = Visibility.Visible;

        private void AboutOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => AboutOverlay.Visibility = Visibility.Collapsed;

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            using Process Navigate = new();
            Navigate.StartInfo = new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            e.Handled = true;
        }

        private void GPVL3_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
        }

        private void Calculated_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Clipboard.SetText((sender as TextBlock)!.Text.ToString());
            MessageBox.Show("Copied to clipboard!");
        }

        private void CloseAboutOverlay_Click(object sender, RoutedEventArgs e) => AboutOverlay.Visibility = Visibility.Collapsed;
    }
}
