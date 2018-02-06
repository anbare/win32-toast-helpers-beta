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
        /// If this is true, that means your app is not running under the Desktop Bridge, and you must call <see cref="RegisterWithPlatformAsync{T}(string, string, string)"/>
        /// </summary>
        public static bool MustRegister => DesktopBridgeHelpers.IsRunningAsUwp();

        /// <summary>
        /// If not running under the Desktop Bridge, you must call this method to register your app information with the notification platform.
        /// Feel free to call this regardless (so you don't need to fork your code), and we will no-op if running under Desktop Bridge.
        /// </summary>
        /// <typeparam name="T">Your implementation of the notification activator.</typeparam>
        /// <param name="aumid">The AUMID to use if not running under Desktop Bridge. This should be unique and different from your Desktop Bridge app's AUMID.</param>
        /// <param name="appDisplayName">The display name to use if not running under Desktop Bridge.</param>
        /// <param name="appLogo">The app logo to use if not running under Desktop Bridge.</param>
        /// <returns></returns>
        public static async Task RegisterWithPlatformAsync<T>(string aumid, string appDisplayName, string appLogo)
            where T : NotificationActivator
        {
            if (typeof(T) == typeof(NotificationActivator))
            {
                throw new ArgumentException("You must provide an implementation of your NotificationActivator.");
            }

            if (string.IsNullOrWhiteSpace(aumid))
            {
                throw new ArgumentException("You must provide an AUMID.", nameof(aumid));
            }

            if (string.IsNullOrWhiteSpace(appDisplayName))
            {
                throw new ArgumentException("You must provide a display name.", nameof(appDisplayName));
            }

            if (string.IsNullOrWhiteSpace(appLogo))
            {
                throw new ArgumentException("You must provide an app logo.", nameof(appLogo));
            }

            // If running as Desktop Bridge
            if (DesktopBridgeHelpers.IsRunningAsUwp())
            {
                // Clear the AUMID since Desktop Bridge doesn't use it, and then we're done.
                // Desktop Bridge apps are registered with platform through their manifest.
                _aumid = null;
                return;
            }

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

        /// <summary>
        /// Registers COM CLSID and EXE in LocalServer32 registry, and registers the activator type as a COM server client.
        /// When your EXE isn't running and an activation comes in, you will be launched with command line flags of -ToastActivation and -Embedded.
        /// </summary>
        /// <typeparam name="T">Your implementation of NotificationActivator. Must have GUID and ComVisible attributes on class.</typeparam>
        public static void RegisterComServerAndActivator<T>()
            where T : NotificationActivator
        {
            String exePath = Process.GetCurrentProcess().MainModule.FileName;

            RegisterComServerAndActivator<T>(exePath);
        }

        /// <summary>
        /// Registers COM CLSID and EXE in LocalServer32 registry, and registers the activator type as a COM server client.
        /// When your EXE isn't running and an activation comes in, you will be launched with command line flags of -ToastActivation and -Embedded.
        /// </summary>
        /// <typeparam name="T">Your implementation of NotificationActivator. Must have GUID and ComVisible attributes on class.</typeparam>
        /// <param name="exePath">A custom EXE path.</param>
        public static void RegisterComServerAndActivator<T>(string exePath)
            where T : NotificationActivator
        {
            RegisterComServer<T>(exePath);

            // Register type
            var regService = new RegistrationServices();

            regService.RegisterTypeForComClients(
                typeof(T),
                RegistrationClassContext.LocalServer,
                RegistrationConnectionType.MultipleUse);
        }

        private static void RegisterAndInitializeComActivatorHelper<T>()
            where T : NotificationActivator
        {
        }

        /// <summary>
        /// Creates a toast notifier. You must have either called <see cref="RegisterWithPlatformAsync{T}(string, string, string)"/> or <see cref="RegisterAsDesktopBridge{T}"/> first, or this will throw an exception.
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

        private static void RegisterComServer<T>(String exePath)
            where T : NotificationActivator
        {
            // We register the EXE to start up when the notification is activated
            string regString = String.Format("SOFTWARE\\Classes\\CLSID\\{{{0}}}\\LocalServer32", typeof(T).GUID);
            var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regString);

            // Include a flag so we know this was a toast activation and should wait for COM to process
            // We also wrap EXE path in quotes for extra security
            key.SetValue(null, '"' + exePath + '"' + " -ToastActivation");
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





        #region DEPRECATED


        /// <summary>
        /// Creates a Start shortcut and registers your app for notifications.
        /// </summary>
        /// <typeparam name="T">Your notification activator receiver. You must extend this class with your own, and specify your own class here.</typeparam>
        /// <param name="appDisplayName"></param>
        /// <param name="appUserModelId"></param>
        internal static void CreateShortcutAndRegister<T>(string appDisplayName, string appUserModelId)
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

        #endregion
    }
}
