using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;

namespace Win32Extensions
{
    public class DesktopNotificationManagerCompat
    {
        private static bool _registered;
        private static string _aumid;

        /// <summary>
        /// If this is true, that means your app is not running under the Desktop Bridge, and you must call <see cref="RegisterAsync{T}(string, string, string)"/>
        /// </summary>
        public static bool MustRegister => DesktopBridgeHelpers.IsRunningAsUwp();

        /// <summary>
        /// Registers and initalizes the COM activator for receiving notification activations. If you're exclusively using Desktop Bridge, you can use this method. Otherwise, you should call <see cref="RegisterAsync{T}(string, string, string)"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static void RegisterAsDesktopBridge<T>()
            where T : NotificationActivator
        {
            if (!DesktopBridgeHelpers.IsRunningAsUwp())
            {
                throw new InvalidOperationException("You are not using Desktop Bridge. You must call the RegisterAsync overload that takes in an AUMID.");
            }

            _aumid = null;

            RegisterAndInitializeComActivatorHelper<T>();

            _registered = true;
        }

        /// <summary>
        /// Registers your app information with the notification platform (if not running under Desktop Bridge), and registers/initializes the COM activator for receiving notification activations. If you're exclusively using the Desktop Bridge, you can instead call <see cref="RegisterAsDesktopBridge{T}"/>.
        /// </summary>
        /// <typeparam name="T">Your implementation of the notification activator.</typeparam>
        /// <param name="aumid">The AUMID to use if not running under Desktop Bridge. This should be unique and different from your Desktop Bridge app's AUMID.</param>
        /// <param name="appDisplayName">The display name to use if not running under Desktop Bridge.</param>
        /// <param name="appLogo">The app logo to use if not running under Desktop Bridge.</param>
        /// <returns></returns>
        public static async Task RegisterAsync<T>(string aumid, string appDisplayName, string appLogo)
            where T : NotificationActivator
        {
            Type activatorType = typeof(T);

            if (activatorType == typeof(NotificationActivator))
            {
                throw new ArgumentException("You must provide an implementation of your NotificationActivator.");
            }

            // If not running Desktop Bridge, we need to register them
            if (!DesktopBridgeHelpers.IsRunningAsUwp())
            {
                // Cache their AUMID
                _aumid = aumid;

                // If new API available
                if (ApiInformation.IsTypePresent("Windows.UI.Notifications.Win32.NewDesktopApiClass"))
                {
                    // We can call that registration
                    // await NewDesktopApiClass.RegisterAsync(aumid, appDisplayName, appLogo, typeof(T).GUID);
                }

                else
                {
                    // Otherwise we fall back to registering in the registry
                    // TODO: Make this use hidden registry way
                    //CreateHiddenShortcutAndRegister<T>(appDisplayName, aumid);
                    String shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\Microsoft\\Windows\\Start Menu\\Programs\\{appDisplayName}.lnk";

                    // Find the path to the current executable
                    String exePath = Process.GetCurrentProcess().MainModule.FileName;
                    InstallShortcut<T>(shortcutPath, exePath, aumid);
                }
            }

            else
            {
                // Clear the AUMID since Desktop Bridge doesn't use it
                _aumid = null;
            }

            // Register and initialize their COM activator
            RegisterAndInitializeComActivatorHelper<T>();

            _registered = true;
        }

        private static void RegisterAndInitializeComActivatorHelper<T>()
            where T : NotificationActivator
        {
            String exePath = Process.GetCurrentProcess().MainModule.FileName;
            RegisterComServer<T>(exePath);

            NotificationActivator.Initialize<T>();
        }

        /// <summary>
        /// Creates a toast notifier. You must have either called <see cref="RegisterAsync{T}(string, string, string)"/> or <see cref="RegisterAsDesktopBridge{T}"/> first, or this will throw an exception.
        /// </summary>
        /// <returns></returns>
        public static ToastNotifier CreateToastNotifier()
        {
            if (!_registered)
            {
                throw new Exception("You must call RegisterAsync first.");
            }

            if (_aumid != null)
            {
                // Non-Desktop Bridge
                return ToastNotificationManager.CreateToastNotifier(_aumid);
            }
            else
            {
                // Desktop Bridge
                return ToastNotificationManager.CreateToastNotifier();
            }
        }

        //public static DesktopNotificationManagerCompat GetForUser(User)

        /// <summary>
        /// Creates a Start shortcut and registers your app for notifications.
        /// </summary>
        /// <typeparam name="T">Your notification activator receiver. You must extend this class with your own, and specify your own class here.</typeparam>
        /// <param name="appDisplayName"></param>
        /// <param name="appUserModelId"></param>
        public static void CreateShortcutAndRegister<T>(string appDisplayName, string appUserModelId)
            where T : NotificationActivator
        {
            Type activatorType = typeof(T);

            if (activatorType == typeof(NotificationActivator))
            {
                throw new ArgumentException("You must provide an implementation of your NotificationActivator.");
            }

            // TODO: Validate attributes are set

            String shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\Microsoft\\Windows\\Start Menu\\Programs\\{appDisplayName}.lnk";

            // Find the path to the current executable
            String exePath = Process.GetCurrentProcess().MainModule.FileName;
            InstallShortcut<T>(shortcutPath, exePath, appUserModelId);
            RegisterComServer<T>(exePath);

            NotificationActivator.Initialize<T>();
        }

        public static void CreateHiddenShortcutAndRegister<T>(string appDisplayName, string appUserModelId)
            where T : NotificationActivator
        {

        }

        private static void InstallShortcut<T>(String shortcutPath, String exePath, string appUserModelId)
            where T : NotificationActivator
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
            varToastId.SetValue(typeof(T).GUID);

            newShortcutProperties.SetValue(PROPERTYKEY.AppUserModel_ToastActivatorCLSID, varToastId.Propvariant);

            // Commit the shortcut to disk
            IPersistFile newShortcutSave = (IPersistFile)newShortcut;

            newShortcutSave.Save(shortcutPath, true);
        }

        private static void RegisterComServer<T>(String exePath)
            where T : NotificationActivator
        {
            // We register the app process itself to start up when the notification is activated, but
            // other options like launching a background process instead that then decides to launch
            // the UI as needed.
            string regString = String.Format("SOFTWARE\\Classes\\CLSID\\{{{0}}}\\LocalServer32", typeof(T).GUID);
            var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regString);

            // Include a flag so we know this was a toast launch and should wait for COM to process
            // We also wrap EXE path in quotes for extra security
            key.SetValue(null, '"' + exePath + '"' + " -ToastComActivation");
        }

        /// <summary>
        /// Code from https://github.com/qmatteoq/DesktopBridgeHelpers/edit/master/DesktopBridge.Helpers/Helpers.cs
        /// </summary>
        public class DesktopBridgeHelpers
        {
            const long APPMODEL_ERROR_NO_PACKAGE = 15700L;

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

            private static bool? _isRunningAsUwp;
            public static bool IsRunningAsUwp()
            {
                if (_isRunningAsUwp == null)
                {
                    if (IsWindows7OrLower)
                    {
                        _isRunningAsUwp = false;
                    }
                    else
                    {
                        int length = 0;
                        StringBuilder sb = new StringBuilder(0);
                        int result = GetCurrentPackageFullName(ref length, sb);

                        sb = new StringBuilder(length);
                        result = GetCurrentPackageFullName(ref length, sb);

                        _isRunningAsUwp = result != APPMODEL_ERROR_NO_PACKAGE;
                    }
                }

                return _isRunningAsUwp.Value;
            }

            private static bool IsWindows7OrLower
            {
                get
                {
                    int versionMajor = Environment.OSVersion.Version.Major;
                    int versionMinor = Environment.OSVersion.Version.Minor;
                    double version = versionMajor + (double)versionMinor / 10;
                    return version <= 6.1;
                }
            }
        }
    }
}
