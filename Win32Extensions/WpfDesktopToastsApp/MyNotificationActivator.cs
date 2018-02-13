using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using DesktopNotifCompat;

namespace WpfDesktopToastsApp
{
    // The GUID must be unique to your app. Create a new GUID if copying this code.
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(INotificationActivationCallback))]
    [Guid("7956E95C-D42E-413B-9C8E-C173E6ADF0C7"), ComVisible(true)]
    public class MyNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string invokedArgs, NotificationUserInput userInput, string appUserModelId)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                // Parse the query string (using QueryString.NET)
                QueryString args = QueryString.Parse(invokedArgs);

                // See what action is being requested 
                switch (args["action"])
                {
                    // Open the image
                    case "viewImage":

                        // The URL retrieved from the toast args
                        string imageUrl = args["imageUrl"];

                        // Make sure we have a window open (in case user clicked this while app closed)
                        if (App.Current.Windows.Count == 0)
                        {
                            new MainWindow().Show();
                        }

                        // And then show the image
                        (App.Current.Windows[0] as MainWindow).ShowImage(imageUrl);

                        break;


                    // Open the conversation
                    case "viewConversation":

                        // The conversation ID retrieved from the toast args
                        int conversationId = int.Parse(args["conversationId"]);


                        // Make sure we have a window open (in case user clicked this while app closed)
                        if (App.Current.Windows.Count == 0)
                        {
                            new MainWindow().Show();
                        }

                        // And then show the conversation
                        (App.Current.Windows[0] as MainWindow).ShowConversation();
                        break;

                    // Background: Quick reply to the conversation
                    case "reply":

                        // Get the response the user typed
                        string msg = userInput["tbReply"];

                        // And send this message (we'll just show a toast)
                        ShowToast("Sending message: " + msg);

                        // If there's no windows open, exit the app
                        if (App.Current.Windows.Count == 0)
                        {
                            Application.Current.Shutdown();
                        }

                        break;

                    // Background: Send a like
                    case "like":

                        // Send a like (we'll just show a toast)
                        ShowToast("Sent a like!");

                        // If there's no windows open, exit the app
                        if (App.Current.Windows.Count == 0)
                        {
                            Application.Current.Shutdown();
                        }

                        break;

                    default:

                        new MainWindow().Show();
                        break;
                }
            });
        }

        private void ShowToast(string msg)
        {
            // Construct the visuals of the toast
            ToastContent toastContent = new ToastContent()
            {
                // Arguments when the user taps body of toast
                Launch = "action=ok",

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = msg
                            }
                        }
                    }
                }
            };

            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the toast notification
            var toast = new ToastNotification(doc);

            // And then show it
            // If non-Desktop Bridge app, you must provide your AUMID
            DesktopNotificationManagerCompat.CreateToastNotifier().Show(toast);
        }
    }
}
