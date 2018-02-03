using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Win32Extensions;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace WpfDesktopToastsApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ButtonPopToast_Click(object sender, RoutedEventArgs e)
        {
            string title = "Andrew sent you a picture";
            string content = "Check this out, Happy Canyon in Utah!";
            string image = "https://picsum.photos/360/202?image=883";
            int conversationId = 5;

            // Construct the toast content
            ToastContent toastContent = new ToastContent()
            {
                // Arguments when the user taps body of toast
                Launch = new QueryString()
                {
                    { "action", "viewConversation" },
                    { "conversationId", conversationId.ToString() }

                }.ToString(),

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = title
                            },

                            new AdaptiveText()
                            {
                                Text = content
                            }
                        }
                    }
                },

                Actions = new ToastActionsCustom()
                {
                    Inputs =
                    {
                        new ToastTextBox("tbReply")
                        {
                            PlaceholderContent = "Type a response"
                        }
                    },

                    Buttons =
                    {
                        new ToastButton("Reply", new QueryString()
                        {
                            { "action", "reply" },
                            { "conversationId", conversationId.ToString() }

                        }.ToString())
                        {
                            ActivationType = ToastActivationType.Background
                        },

                        new ToastButton("Like", new QueryString()
                        {
                            { "action", "like" },
                            { "conversationId", conversationId.ToString() }

                        }.ToString())
                        {
                            ActivationType = ToastActivationType.Background
                        },

                        new ToastButton("View", new QueryString()
                        {
                            { "action", "viewImage" },
                            { "imageUrl", image }

                        }.ToString())
                    }
                }
            };

            Clipboard.SetText(toastContent.GetContent());

            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the toast notification
            var toast = new ToastNotification(doc);

            // And then show it
            DesktopNotificationManagerCompat.CreateToastNotifier().Show(toast);
        }

        internal void ShowConversation()
        {
            ContentBody.Content = new TextBlock()
            {
                Text = "You've just opened a conversation!",
                FontWeight = FontWeights.Bold
            };
        }

        internal void ShowImage(string imageUrl)
        {
            ContentBody.Content = new Image()
            {
                Source = new BitmapImage(new Uri(imageUrl))
            };
        }
    }
}
