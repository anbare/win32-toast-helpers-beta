using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DesktopNotifCompat;

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
            // Register with notification platform
            await DesktopNotificationManagerCompat.RegisterWithPlatformAsync<MyNotificationActivator>(
                aumid: "Microsoft.WpfDesktopToasts",
                displayName: "WPF Desktop Toasts",
                logo: "C:\\logo.png",
                logoBackgroundColor: "transparent");

            // And then register COM server and activator type
            DesktopNotificationManagerCompat.RegisterComServerAndActivator<MyNotificationActivator>();

            // If launched from a toast
            if (e.Args.Contains("-ToastActivated"))
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
