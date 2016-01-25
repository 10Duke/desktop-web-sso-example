//-----------------------------------------------------------------------
// <copyright file="MainWindow.cs" company="10Duke Software">
//     Copyright (c) 10Duke
// </copyright>
// <author>Jarkko Selkäinaho</author>
//-----------------------------------------------------------------------
namespace Tenduke.SsoClient.WPF.LoginFormTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Policy;
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
    using Tenduke.SsoClient.Request;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// CookieContainer to use with requests sent by the application.
        /// </summary>
        private readonly CookieContainer _cookieContainer = new CookieContainer();

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Runs the test.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event arguments.</param>
        private void buttonTest_Click(object sender, RoutedEventArgs e)
        {
            textBoxResponse.Text = string.Empty;

            if (HasAuthenticatedSession())
            {
                textBoxResponse.Text += "Logged in user session found\n";
                SendTestRequests();
            }
            else
            {
                textBoxResponse.Text += "No logged in user session found, logging in..\n";
                Login((request, canceled) =>
                {
                    request.Response?.Close();
                    
                    if (canceled)
                    {
                        textBoxResponse.Text += "Login canceled\n";
                    }
                    else
                    {
                        SendTestRequests();
                    }
                });
            }
        }

        /// <summary>
        /// Sends the test requests.
        /// </summary>
        private void SendTestRequests()
        {
            SendMyInfoRequest((myInfoRequest, myInfoRequestCanceled) =>
            {
                if (myInfoRequestCanceled)
                {
                    myInfoRequest.Response?.Close();
                    textBoxResponse.Text += "User profile info request canceled\n";
                }
                else
                {
                    using (var myInfoResponse = myInfoRequest.Response)
                    {
                        var responseContent = GetResponseTextContent(myInfoResponse);
                        textBoxResponse.Text += "Logged in user profile details: " + responseContent + "\n";
                    }

                    SendLicenseQuery((testLicenseRequest, testLicenseRequestCanceled) =>
                    {
                        if (testLicenseRequestCanceled)
                        {
                            testLicenseRequest.Response?.Close();
                            textBoxResponse.Text += "Test license request canceled\n";
                        }
                        else
                        {
                            using (var testLicenseResponse = testLicenseRequest.Response)
                            {
                                var responseContent = GetResponseTextContent(testLicenseResponse);
                                textBoxResponse.Text += "Test license response: " + responseContent + "\n";
                            }
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Sends a <c>/graph/me</c> request to the provider for querying logged in user basic profile information.
        /// </summary>
        /// <param name="responseCallback">Response callback called in the UI thread when response is available.</param>
        private void SendMyInfoRequest(WebSsoRequestCallback<WpfLoginFormWebSsoRequest> responseCallback)
        {
            var myInfoUrl = new Uri(new Uri(textBoxProviderProtocolAndZone.Text), "/graph/me.json?wrap=false&authenticate=always");
            SendRequest(myInfoUrl, responseCallback);
        }

        /// <summary>
        /// Sends an <c>/authz</c> request for checking test license.
        /// </summary>
        /// <param name="responseCallback">Response callback called in the UI thread when response is available.</param>
        private void SendLicenseQuery(WebSsoRequestCallback<WpfLoginFormWebSsoRequest> responseCallback)
        {
            var checkTestLicenseUrl = new Uri(new Uri(textBoxProviderProtocolAndZone.Text), "/authz?__TestLicensedItem__&authenticate=always");
            SendRequest(checkTestLicenseUrl, responseCallback);
        }

        /// <summary>
        /// Sends a request that will trigger identity provider authentication challenge, if not already logged in.
        /// </summary>
        /// <param name="responseCallback">
        /// Response callback called in the UI thread when response is available. If login is required, login is
        /// handled during request processing and the response callback is called after login.
        /// </param>
        private void Login(WebSsoRequestCallback<WpfLoginFormWebSsoRequest> responseCallback)
        {
            var loginUrl = new Uri(new Uri(textBoxProviderProtocolAndZone.Text), "/login");
            SendRequest(loginUrl, responseCallback);
        }

        /// <summary>
        /// Sends an HTTP GET request to the given URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="responseCallback">Response callback called in the UI thread when response is available.</param>
        private void SendRequest(Uri url, WebSsoRequestCallback<WpfLoginFormWebSsoRequest> responseCallback)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.CookieContainer = _cookieContainer;
            var interactionRequiredPattern = BuildLoginPromptPattern();
            var submitLoginUrl = new Uri(new Uri(textBoxProviderProtocolAndZone.Text), "/login/authenticate.vsl");

            var ssoRequest = new WpfLoginFormWebSsoRequest(
                httpWebRequest,
                interactionRequiredPattern,
                this,
                submitLoginUrl);
            ssoRequest.BeginGetResponse((request, canceled) =>
            {
                Dispatcher.Invoke(() =>
                {
                    responseCallback(request, canceled);
                });
            });
        }

        /// <summary>
        /// Checks if this test client thinks it has authenticated session against the provider. This check is
        /// made by checking if user id cookie is found the test client cookie container.
        /// </summary>
        /// <returns><c>true</c> if authenticated session information found, <c>false</c> otherwise.</returns>
        private bool HasAuthenticatedSession()
        {
            var providerUrl = new Uri(textBoxProviderProtocolAndZone.Text);
            return _cookieContainer.GetCookies(providerUrl).Cast<Cookie>().Any(cookie => "djsuid" == cookie.Name);
        }

        /// <summary>
        /// Builds pattern used for recognising when login prompt is required.
        /// </summary>
        /// <returns>The <see cref="Regex"/> pattern that is matched against resource URL to detect whether login prompt is required.</returns>
        private Regex BuildLoginPromptPattern()
        {
            var providerProtocolAndZone = textBoxProviderProtocolAndZone.Text;

            // Escape slash and dot for Regex
            providerProtocolAndZone = providerProtocolAndZone.Replace("/", "\\/").Replace(".", "\\.");
                        
            return new Regex(new StringBuilder().Append('^').Append(providerProtocolAndZone).Append("(\\/login)?(((\\/authenticate\\.vsl)?)|((\\/)?))(\\?.*)?$").ToString());
        }

        /// <summary>
        /// Gets <see cref="HttpWebResponse"/> text content. This method assumes that the response carries
        /// text content in UTF-8 character encoding.
        /// </summary>
        /// <param name="response">The <see cref="HttpWebResponse"/>.</param>
        /// <returns>The text content.</returns>
        private string GetResponseTextContent(HttpWebResponse response)
        {
            using (var responseStream = response.GetResponseStream())
            {
                var reader = new StreamReader(responseStream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }
    }
}
