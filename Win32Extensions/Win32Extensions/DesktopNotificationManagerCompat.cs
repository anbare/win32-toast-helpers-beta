// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;

namespace DesktopNotifCompat
{
    public class DesktopNotificationManagerCompat
    {
        private static bool _registered;
        private static string _aumid;

        /// <summary>
        /// If this is true, that means your app is not running under the Desktop Bridge, and you must call <see cref="RegisterWithPlatformAsync{T}(string, string, string)"/>.
        /// </summary>
        public static bool MustRegisterWithPlatform => DesktopBridgeHelpers.IsRunningAsUwp();

        /// <summary>
        /// If not running under the Desktop Bridge, you must call this method to register your app information with the notification platform.
        /// Feel free to call this regardless, and we will no-op if running under Desktop Bridge. Call this upon application startup, before sending toasts.
        /// </summary>
        /// <typeparam name="T">Your implementation of the notification activator.</typeparam>
        /// <param name="aumid">The AUMID to use if not running under Desktop Bridge. This should be unique and different from your Desktop Bridge app's AUMID.</param>
        /// <param name="displayName">The display name to use if not running under Desktop Bridge.</param>
        /// <param name="logo">The app logo to use if not running under Desktop Bridge.</param>
        /// <param name="logoBackgroundColor">The background color to use with your logo if not running under Desktop Bridge. This should be in hex #AARRGGBB format, like "#FF0063B1", or "transparent" if you want the system accent color to be used.</param>
        /// <returns></returns>
        public static Task RegisterWithPlatformAsync<T>(string aumid, string displayName, string logo, string logoBackgroundColor)
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

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("You must provide a display name.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(logo))
            {
                throw new ArgumentException("You must provide an app logo.", nameof(logo));
            }

            if (string.IsNullOrWhiteSpace(logoBackgroundColor))
            {
                throw new ArgumentException("You must provide a logo background color.", nameof(logoBackgroundColor));
            }

            // If running as Desktop Bridge
            if (DesktopBridgeHelpers.IsRunningAsUwp())
            {
                // Clear the AUMID since Desktop Bridge doesn't use it, and then we're done.
                // Desktop Bridge apps are registered with platform through their manifest.
                _aumid = null;
                _registered = true;
                return Task.CompletedTask;
            }

            // Cache their AUMID
            _aumid = aumid;

            // In the future, there'll be a new platform API that we can call to register, which will likely be async,
            // which is why this method is flagged async.
            String shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\Microsoft\\Windows\\Start Menu\\Programs\\{displayName}.lnk";

            // Find the path to the current executable
            String exePath = Process.GetCurrentProcess().MainModule.FileName;
            InstallShortcut<T>(shortcutPath, exePath, aumid);

            _registered = true;
            return Task.CompletedTask;
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

        /// <summary>
        /// Registers COM CLSID and EXE in LocalServer32 registry, and registers the activator type as a COM server client.
        /// We recommend calling this upon application startup. You must call this in order to receive notification activations.
        /// When your EXE isn't running and an activation comes in, you will be launched with command line flags of -ToastActivated and -Embedded.
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

        private static void RegisterComServer<T>(String exePath)
            where T : NotificationActivator
        {
            // We register the EXE to start up when the notification is activated
            string regString = String.Format("SOFTWARE\\Classes\\CLSID\\{{{0}}}\\LocalServer32", typeof(T).GUID);
            var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regString);

            // Include a flag so we know this was a toast activation and should wait for COM to process
            // We also wrap EXE path in quotes for extra security
            key.SetValue(null, '"' + exePath + '"' + " -ToastActivated");
        }

        /// <summary>
        /// Creates a toast notifier. If you're a classic Win32 app, you must have called <see cref="RegisterWithPlatformAsync{T}(string, string, string)"/> first, or this will throw an exception.
        /// </summary>
        /// <returns></returns>
        public static ToastNotifier CreateToastNotifier()
        {
            EnsureRegistered();

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

        public static DesktopNotificationHistoryCompat History
        {
            get
            {
                EnsureRegistered();

                return new DesktopNotificationHistoryCompat(_aumid);
            }
        }

        private static void EnsureRegistered()
        {
            // If not registered
            if (!_registered)
            {
                // Check if Desktop Bridge
                if (DesktopBridgeHelpers.IsRunningAsUwp())
                {
                    // Implicitly registered, all good!
                    _registered = true;
                }

                else
                {
                    // Otherwise, incorrect usage
                    throw new Exception("You must call RegisterWithPlatformAsync first.");
                }
            }
        }

        /// <summary>
        /// Code from https://github.com/qmatteoq/DesktopBridgeHelpers/edit/master/DesktopBridge.Helpers/Helpers.cs
        /// </summary>
        private class DesktopBridgeHelpers
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
