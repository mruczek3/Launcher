using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Launcher
{
    public partial class MainWindow : Window
    {
        private readonly string installPath = Path.Combine(Environment.CurrentDirectory, "Versions");
        private CancellationTokenSource cts;
        private readonly HttpClient client;
        private readonly string logFile = Path.Combine(Environment.CurrentDirectory, "launcher.log");

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(installPath);
            client = new HttpClient();
            ShowDownloadPage(null, null);
            LoadInstalledVersions();
            Log("Launcher uruchomiony.");

            // ANIMACJA STARTOWA LOGO
            AnimateLogo();
        }

        private void AnimateLogo()
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var scaleUpX = new DoubleAnimation
            {
                From = 0.6,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
            };

            var scaleUpY = scaleUpX.Clone();

            LogoImage.BeginAnimation(OpacityProperty, fadeIn);
            ((ScaleTransform)LogoImage.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpX);
            ((ScaleTransform)LogoImage.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleUpY);

            // Można też animować tekst
            LauncherTitle.BeginAnimation(OpacityProperty, fadeIn);
        }


        /* 📜 LOGI DO PLIKU */
        private void Log(string message)
        {
            try
            {
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* Ignorujemy błędy logowania */ }
        }

        /* 📂 Wczytywanie zainstalowanych wersji (tylko te z SCPSL.exe) */
        private void LoadInstalledVersions()
        {
            InstalledVersions.Items.Clear();
            if (!Directory.Exists(installPath)) return;

            foreach (var dir in Directory.GetDirectories(installPath))
            {
                if (FindExeInFolder(dir) != null)
                    InstalledVersions.Items.Add(Path.GetFileName(dir));
            }

            if (InstalledVersions.Items.Count > 0 && InstalledVersions.SelectedIndex < 0)
                InstalledVersions.SelectedIndex = 0;
        }

        /* ▶ Przełączanie stron */
        private void ShowDownloadPage(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 0;
        private void ShowPlayPage(object sender, RoutedEventArgs e) { MainTabs.SelectedIndex = 1; LoadInstalledVersions(); }

        /* ❌ Zamknięcie aplikacji */
        private void CloseApp(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        /* 📥 Pobieranie wersji */
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;

            var parts = tag.Split('|');
            if (parts.Length != 2) return;

            string version = parts[0];
            string url = parts[1];
            string zipPath = Path.Combine(installPath, $"{version}.zip");
            string extractPath = Path.Combine(installPath, version);

            // jeśli wersja już istnieje
            if (Directory.Exists(extractPath))
            {
                ShowToast($"Wersja {version} jest już zainstalowana", "info");
                return;
            }

            cts?.Cancel(); // przerwij ewentualne poprzednie
            cts = new CancellationTokenSource();

            try
            {
                Directory.CreateDirectory(installPath);
                DownloadStatus.Text = $"Pobieranie {version}...";
                DownloadProgress.Value = 0;

                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var stream = await response.Content.ReadAsStreamAsync(cts.Token))
                    using (var file = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[8192];
                        long totalRead = 0;
                        int read;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                        {
                            await file.WriteAsync(buffer, 0, read, cts.Token);
                            totalRead += read;

                            if (totalBytes > 0)
                            {
                                int pct = (int)((totalRead * 100) / totalBytes);
                                Dispatcher.Invoke(() =>
                                {
                                    DownloadProgress.Value = pct;
                                    DownloadStatus.Text = $"Pobrano {totalRead / 1024 / 1024} MB z {totalBytes / 1024 / 1024} MB ({pct}%)";
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() => DownloadStatus.Text = $"Pobrano {totalRead / 1024 / 1024} MB...");
                            }
                        }
                    }
                }

                DownloadStatus.Text = "Rozpakowywanie...";
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(zipPath, extractPath);
                if (File.Exists(zipPath)) File.Delete(zipPath);

                ShowToast($"Zainstalowano wersję {version}", "success");
                Log($"Pobrano i zainstalowano {version}");
                LoadInstalledVersions();
            }
            catch (OperationCanceledException)
            {
                ShowToast("Pobieranie anulowane", "info");
                Log("Pobieranie anulowane.");
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
            catch (Exception ex)
            {
                ShowToast("Błąd pobierania!", "error");
                Log("Błąd pobierania: " + ex);
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }

        /* ⏹ Anulowanie pobierania */
        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        /* ▶ Uruchamianie gry */
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (InstalledVersions.SelectedItem == null)
            {
                ShowToast("Wybierz wersję!", "info");
                return;
            }

            string version = InstalledVersions.SelectedItem.ToString();
            string versionFolder = Path.Combine(installPath, version);
            string exe = FindExeInFolder(versionFolder);

            if (exe == null)
            {
                ShowToast("Nie znaleziono SCPSL.exe", "error");
                return;
            }

            // Sprawdź czy gra już działa
            if (Process.GetProcessesByName("SCPSL").Length > 0)
            {
                ShowToast("Gra już działa!", "info");
                return;
            }

            try
            {
                Process.Start(exe);
                ShowToast($"Uruchomiono {version}", "success");
                Log($"Uruchomiono {version}");
            }
            catch (Exception ex)
            {
                ShowToast("Błąd uruchamiania!", "error");
                Log("Błąd uruchamiania: " + ex);
            }
        }

        /* 🗑 Odinstalowywanie */
        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (InstalledVersions.SelectedItem == null)
            {
                ShowToast("Wybierz wersję!", "info");
                return;
            }

            string version = InstalledVersions.SelectedItem.ToString();
            string path = Path.Combine(installPath, version);

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    ShowToast($"Odinstalowano {version}", "success");
                    Log($"Odinstalowano {version}");
                    LoadInstalledVersions();
                }
                else
                {
                    ShowToast("Nie znaleziono folderu wersji", "error");
                }
            }
            catch (Exception ex)
            {
                ShowToast("Błąd odinstalowania!", "error");
                Log("Błąd odinstalowania: " + ex);
            }
        }

        /* 🔎 Szukanie SCPSL.exe rekurencyjnie */
        private string FindExeInFolder(string folder)
        {
            try
            {
                foreach (var file in Directory.GetFiles(folder, "SCPSL.exe", SearchOption.AllDirectories))
                    return file;
            }
            catch { }
            return null;
        }

        /* 🔔 Toast z animacją */
        private async void ShowToast(string message, string type)
        {
            ToastText.Text = message;

            switch (type)
            {
                case "success": Toast.Background = System.Windows.Media.Brushes.SeaGreen; break;
                case "error": Toast.Background = System.Windows.Media.Brushes.DarkRed; break;
                default: Toast.Background = System.Windows.Media.Brushes.DimGray; break;
            }

            Toast.Visibility = Visibility.Visible;
            Toast.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            Toast.BeginAnimation(OpacityProperty, fadeIn);

            await Task.Delay(2500);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, e) => Toast.Visibility = Visibility.Collapsed;
            Toast.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
