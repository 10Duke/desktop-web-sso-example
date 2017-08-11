//-----------------------------------------------------------------------
// <copyright file="WebBrowserWebSsoRequest.cs" company="10Duke Software">
//     Copyright (c) 10Duke
// </copyright>
// <author>Jarkko Selkäinaho</author>
//-----------------------------------------------------------------------
namespace Tenduke.SsoClient.WPF.WebBrowserTest
{
    using CefSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using System.Xml;
    using Tenduke.SsoClient.Request;

    /// <summary>
    /// Web SSO request implementation that uses <see cref="WebBrowser"/> component owned by <see cref="WebBrowserWindow"/> for web SSO process.
    /// </summary>
    public class WebBrowserWebSsoRequest : BasicWebSsoRequestBase<WebBrowserWebSsoRequest>
    {
        #region private fields

        /// <summary>
        /// Window that will be set as owner of web browser window.
        /// </summary>
        private readonly Window _webBrowserOwner;

        /// <summary>
        /// Pattern used against response URL for matching responses received during interaction to detect when interaction is ready.
        /// </summary>
        private readonly Regex _interactionReadyPattern;

        #endregion

        #region constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WebBrowserWebSsoRequest"/> class.
        /// </summary>
        /// <param name="httpWebRequest">The wrapped <see cref="HttpWebRequest"/>.</param>
        /// <param name="loginPromptPattern"><see cref="Regex"/> pattern used against response URL for matching responses that
        /// require interaction, or <c>null</c> if interaction is never required.</param>
        /// <param name="webBrowserOwner">Window that will be set as owner of web browser window possibly opened by the request.</param>
        /// <param name="interactionReadyPattern">Pattern used against response URL for matching responses received during interaction
        /// to detect when interaction is ready. Must not be <c>null</c>.</param>
        public WebBrowserWebSsoRequest(HttpWebRequest httpWebRequest,
                Regex loginPromptPattern,
                Window webBrowserOwner,
                Regex interactionReadyPattern)
            : base(httpWebRequest, loginPromptPattern)
        {
            if (interactionReadyPattern == null)
            {
                throw new ArgumentNullException(nameof(interactionReadyPattern));
            }

            _webBrowserOwner = webBrowserOwner;
            _interactionReadyPattern = interactionReadyPattern;
        }

        #endregion

        #region methods

        /// <summary>
        /// Called to performed required interaction.
        /// </summary>
        /// <param name="response"><see cref="HttpWebResponse"/> representing current HTTP response.</param>
        /// <param name="nextResponseCallback">Callback for receiving <see cref="HttpWebResponse"/> returned by the interaction step.</param>
        protected override void Interact(HttpWebResponse response, NextResponseCallback nextResponseCallback)
        {
            _webBrowserOwner.Dispatcher.Invoke(() =>
            {
                var originalRequest = GetHttpWebRequest();
                var cookieContainer = originalRequest.CookieContainer;

                var webBrowserWindow = new WebBrowserWindow { Owner = _webBrowserOwner };

                webBrowserWindow.webBrowser.Address = originalRequest.RequestUri.ToString();
                var currentAddress = string.IsNullOrEmpty(webBrowserWindow.webBrowser.Address)
                        ? null
                        : new Uri(webBrowserWindow.webBrowser.Address);
                webBrowserWindow.webBrowser.LoadingStateChanged += (sender, args) =>
                {
                    if (!args.IsLoading)
                    {
                        if (cookieContainer != null && currentAddress != null)
                        {
                            var cookiesPath = string.Format("{0}{1}{2}/", currentAddress.Scheme,
                                Uri.SchemeDelimiter, currentAddress.Authority);
                            SetCookiesToCookieContainer(new Uri(cookiesPath), cookieContainer);
                        }

                        var newAddress = string.IsNullOrEmpty(args.Browser.MainFrame.Url)
                                ? null
                                : new Uri(args.Browser.MainFrame.Url);
                        if (newAddress != null && _interactionReadyPattern.Match(newAddress.ToString()).Success)
                        {
                            var request = (HttpWebRequest)WebRequest.Create(newAddress);
                            request.CookieContainer = cookieContainer;
                            using (var interactionReadyResponse = (HttpWebResponse)request.GetResponse())
                            {
                                nextResponseCallback(interactionReadyResponse, false);
                            }

                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                webBrowserWindow.DialogResult = true;
                                webBrowserWindow.Close();
                            });
                        }
                    }
                };

                if (true != webBrowserWindow.ShowDialog())
                {
                    nextResponseCallback(null, true);
                }
            });
        }

        #endregion

        #region private methods

        /// <summary>
        /// Gets the URI cookie container.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="cookieContainer"><see cref="CookieContainer"/> in which cookies are set.</param>
        private async void SetCookiesToCookieContainer(Uri uri, CookieContainer cookieContainer)
        {
            var cookieManager = Cef.GetGlobalCookieManager();
            var cookies = await cookieManager.VisitUrlCookiesAsync(uri.ToString(), true);

            if (cookies.Count > 0)
            {
                foreach (var cefCookie in cookies)
                {
                    var cookie = new System.Net.Cookie(cefCookie.Name, cefCookie.Value, cefCookie.Path, cefCookie.Domain);
                    if (cefCookie.Expires != null)
                        cookie.Expires = cefCookie.Expires.Value;
                    cookie.HttpOnly = cefCookie.HttpOnly;
                    cookie.Secure = cefCookie.Secure;
                    cookieContainer.Add(cookie);
                }
            }
        }

        #endregion
    }
}
