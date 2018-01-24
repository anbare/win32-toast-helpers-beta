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

        protected override void OnStartup(StartupEventArgs e)
        {
            DesktopNotificationsHelper.CreateShortcutAndRegister(
                appDisplayName: "WPF Desktop Toasts",
                appUserModelId: AUMID);

            base.OnStartup(e);
        }
    }
}
