//-----------------------------------------------------------------------
// <copyright file="WpfLoginFormWebSsoRequest.cs" company="10Duke Software">
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
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using System.Xml;
    using Tenduke.SsoClient.Request;

    /// <summary>
    /// Web SSO request implementation that uses WPF <see cref="LoginWindow"/> for web login prompt.
    /// </summary>
    public class WpfLoginFormWebSsoRequest : BasicWebSsoRequestBase<WpfLoginFormWebSsoRequest>
    {
        #region private fields

        /// <summary>
        /// Window that will be set as owner of the <see cref="LoginWindow"/>
        /// </summary>
        private readonly Window _loginWindowOwner;

        /// <summary>
        /// Url of endpoint to use for submitting login.
        /// </summary>
        private readonly Uri _submitLoginUrl;

        #endregion

        #region constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WpfLoginFormWebSsoRequest"/> class.
        /// </summary>
        /// <param name="httpWebRequest">The wrapped <see cref="HttpWebRequest"/>.</param>
        /// <param name="loginPromptPattern"><see cref="Regex"/> pattern used against response URL for matching responses require
        /// displaying login prompt.</param>
        /// <param name="loginWindowOwner">Window that will be set as owner of web browser window possibly opened by the request.</param>
        /// <param name="submitLoginUrl">Url of endpoint to use for submitting login.</param>
        public WpfLoginFormWebSsoRequest(HttpWebRequest httpWebRequest,
                Regex loginPromptPattern,
                Window loginWindowOwner,
                Uri submitLoginUrl)
            : base(httpWebRequest, loginPromptPattern)
        {
            _loginWindowOwner = loginWindowOwner;
            _submitLoginUrl = submitLoginUrl;
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
            _loginWindowOwner.Dispatcher.Invoke(() =>
            {
                var loginWindow = new LoginWindow {Owner = _loginWindowOwner};
                if (true == loginWindow.ShowDialog())
                {
                    var originalRequest = GetHttpWebRequest();
                    var cookieContainer = originalRequest.CookieContainer;
                    var userName = loginWindow.textBoxUserName.Text;
                    var password = loginWindow.passwordBox.Password;

                    var submitParameters = new StringBuilder()
                            .Append("userName=").Append(Uri.EscapeDataString(userName)).Append('&')
                            .Append("password=").Append(Uri.EscapeDataString(password)).Append('&')
                            .Append("continueTo=").Append(Uri.EscapeDataString(originalRequest.RequestUri.PathAndQuery)).Append('&')
                            .Append("authorized=true").ToString();
                    var postData = Encoding.ASCII.GetBytes(submitParameters);

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_submitLoginUrl);
                    request.CookieContainer = cookieContainer;
                    request.Method = "POST";
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = postData.Length;
                    using (var dataStream = request.GetRequestStream())
                    {
                        dataStream.Write(postData, 0, postData.Length);
                    }

                    using (HttpWebResponse submitResponse = (HttpWebResponse) request.GetResponse())
                    {
                        nextResponseCallback(submitResponse, false);
                    }
                }
                else
                {
                    nextResponseCallback(null, true);
                }
            });
        }

        #endregion
    }
}
