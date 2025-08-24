using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace Launcher
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += SplashScreen_Loaded;
        }

        private async void SplashScreen_Loaded(object sender, RoutedEventArgs e)
        {
            // Animacja logo (wejście z powiększeniem)
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));
            var scaleIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1))
            {
                EasingFunction = new BackEase { Amplitude = 0.6 }
            };
            Logo.BeginAnimation(OpacityProperty, fadeIn);
            ((ScaleTransform)Logo.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            ((ScaleTransform)Logo.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);

            // Tekst fade-in
            await Task.Delay(700);
            var textFade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));
            TitleText.BeginAnimation(OpacityProperty, textFade);

            // Puls logo (neon efekt)
            var pulse = new DoubleAnimation(1, 1.1, TimeSpan.FromSeconds(1.2))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            ((ScaleTransform)Logo.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            ((ScaleTransform)Logo.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, pulse);

            // Poczekaj 4s i fade-out
            await Task.Delay(4000);
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));
            fadeOut.Completed += (s, _) =>
            {
                var main = new MainWindow();
                main.Show();
                Close();
            };
            this.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}


