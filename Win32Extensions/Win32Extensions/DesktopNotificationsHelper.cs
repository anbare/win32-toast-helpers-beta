using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Win32Extensions
{
    public static class DesktopNotificationsHelper
    {
        /// <summary>
        /// Creates a Start shortcut and registers your app for notifications.
        /// </summary>
        /// <typeparam name="T">Your notification activator receiver. You must extend this class with your own, and specify your own class here.</typeparam>
        /// <param name="appDisplayName"></param>
        /// <param name="appUserModelId"></param>
        public static void CreateShortcutAndRegister(string appDisplayName, string appUserModelId)
        {
            String shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\Microsoft\\Windows\\Start Menu\\Programs\\{appDisplayName}.lnk";

            // Find the path to the current executable
            String exePath = Process.GetCurrentProcess().MainModule.FileName;
            InstallShortcut(shortcutPath, exePath, appUserModelId);
            RegisterComServer(exePath);

            NotificationActivator.Initialize();
        }

        private static void InstallShortcut(String shortcutPath, String exePath, string appUserModelId)
        {
            IShellLinkW newShortcut = (IShellLinkW)new CShellLink();

            // Create a shortcut to the exe
            newShortcut.SetPath(exePath);

            // Open the shortcut property store, set the AppUserModelId property
            IPropertyStore newShortcutProperties = (IPropertyStore)newShortcut;

            PropVariantHelper varAppId = new PropVariantHelper();
            varAppId.SetValue(appUserModelId);
            newShortcutProperties.SetValue(PROPERTYKEY.AppUserModel_ID, varAppId.Propvariant);

            PropVariantHelper varToastId = new PropVariantHelper();
            varToastId.VarType = VarEnum.VT_CLSID;
            varToastId.SetValue(typeof(NotificationActivator).GUID);

            newShortcutProperties.SetValue(PROPERTYKEY.AppUserModel_ToastActivatorCLSID, varToastId.Propvariant);

            // Commit the shortcut to disk
            IPersistFile newShortcutSave = (IPersistFile)newShortcut;

            newShortcutSave.Save(shortcutPath, true);
        }

        private static void RegisterComServer(String exePath)
        {
            // We register the app process itself to start up when the notification is activated, but
            // other options like launching a background process instead that then decides to launch
            // the UI as needed.
            string regString = String.Format("SOFTWARE\\Classes\\CLSID\\{{{0}}}\\LocalServer32", typeof(NotificationActivator).GUID);
            var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regString);
            key.SetValue(null, exePath);
        }
    }
}
