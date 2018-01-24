using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Win32Extensions;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace WpfDesktopToastsApp
{
    public class MyNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string invokedArgs, NotificationUserInput userInput, string appUserModelId)
        {
            MessageBox.Show(invokedArgs);

            //// Construct the visuals of the toast
            //ToastContent toastContent = new ToastContent()
            //{
            //    // Arguments when the user taps body of toast
            //    Launch = "action=ok",

            //    Visual = new ToastVisual()
            //    {
            //        BindingGeneric = new ToastBindingGeneric()
            //        {
            //            Children =
            //            {
            //                new AdaptiveText()
            //                {
            //                    Text = "Toast was clicked"
            //                },

            //                new AdaptiveText()
            //                {
            //                    Text = invokedArgs
            //                }
            //            }
            //        }
            //    }
            //};

            //var doc = new XmlDocument();
            //doc.LoadXml(toastContent.GetContent());

            //// And create the toast notification
            //var toast = new ToastNotification(doc);

            //// And then show it
            //// If non-Desktop Bridge app, you must provide your AUMID
            //ToastNotificationManager.CreateToastNotifier(App.AUMID).Show(toast);
        }
    }
}
