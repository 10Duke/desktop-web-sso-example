//-----------------------------------------------------------------------
// <copyright file="MainWindow.cs" company="10Duke Software">
//     Copyright (c) 10Duke
// </copyright>
// <author>Jarkko Selkäinaho</author>
//-----------------------------------------------------------------------
namespace Tenduke.SsoClient.WPF.WebBrowserTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
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

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// CookieContainer to use with requests sent by the application.
        /// </summary>
        private readonly CookieContainer _cookieContainer = new CookieContainer();

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Send request to URL entered in the URL text box, using <see cref="WebBrowserWebSsoRequest"/>. This
        /// handles, if necessary, Web SSO (Single Sign-On) process in a <see cref="WebBrowser"/> hosted by <see cref="WebBrowserWindow"/>.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event arguments.</param>
        private void buttonGo_Click(object sender, RoutedEventArgs e)
        {
            textBoxResponse.Text = string.Empty;

            // Request URL
            var url = new Uri(textBoxUrl.Text);

            // Regex pattern for detecting when Web SSO interaction is required
            var interactionRequiredPattern = string.Empty.Equals(textBoxInteractPattern.Text) ? null : new Regex(textBoxInteractPattern.Text);

            // Regex pattern for detecting when Web SSO interaction is ready. If left empty, request URL with
            // any parameters is used as default.
            Regex interactionReadyPattern;
            if (string.Empty.Equals(textBoxInteractReadyPattern.Text))
            {
                var urlWithoutQuery = string.Format("{0}{1}{2}{3}", url.Scheme,
                    Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
                interactionReadyPattern = new Regex(Regex.Escape(urlWithoutQuery) + "(\\?)?.*");
            }
            else
            {
                interactionReadyPattern = new Regex(textBoxInteractReadyPattern.Text);
            }

            // Create underlying HttpWebRequest
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
            httpWebRequest.CookieContainer = _cookieContainer;

            // Initialize and execute the WebBrowserWebSsoRequest
            var ssoRequest = new WebBrowserWebSsoRequest(
                httpWebRequest,
                interactionRequiredPattern,
                this,
                interactionReadyPattern);
            ssoRequest.BeginGetResponse((request, canceled) =>
            {
                Dispatcher.Invoke(() =>
                {
                    using (request.Response)
                    {
                        if (!canceled)
                        {
                            var responseStream = request.Response.GetResponseStream();
                            var reader = new StreamReader(responseStream);
                            var responseText = reader.ReadToEnd();
                            textBoxResponse.Text += responseText;
                        }
                    }
                });
            });
        }
    }
}
