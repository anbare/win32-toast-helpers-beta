using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Win32Extensions;

namespace WpfDesktopToastsApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public const string AUMID = "Microsoft.WpfDesktopToasts";

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Must always register for notifications
            await DesktopNotificationManagerCompat.RegisterAsync<MyNotificationActivator>(
                aumid: "Microsoft.WpfDesktopToasts",
                appDisplayName: "WPF Desktop Toasts",
                appLogo: "C:\\logo.png");

            // If launched from a toast
            if (e.Args.Length > 0 && e.Args[0] == "-Embedding")
            {
                // Our NotificationActivator will handle showing windows if necessary
                base.OnStartup(e);
                return;
            }

            // Show the window
            new MainWindow().Show();

            base.OnStartup(e);
        }
    }
}
