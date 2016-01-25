//-----------------------------------------------------------------------
// <copyright file="BasicWebSsoRequestBase.cs" company="10Duke Software">
//     Copyright (c) 10Duke
// </copyright>
// <author>Jarkko Selkäinaho</author>
//-----------------------------------------------------------------------
namespace Tenduke.SsoClient.Request
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    /// <summary>
    /// Basic Web SSO (Single Sign-On) request base class, for implementing request handling that may include
    /// interaction. <see cref="BasicWebSsoRequestBase{T}"/> wraps a <see cref="HttpWebRequest"/> and checks response
    /// url to detect if interaction is required.
    /// </summary>
    public abstract class BasicWebSsoRequestBase<T> : WebSsoRequestBase<T> where T : BasicWebSsoRequestBase<T>
    {
        #region private fields

        /// <summary>
        /// The wrapped <see cref="HttpWebRequest"/>.
        /// </summary>
        private readonly HttpWebRequest _httpWebRequest;

        /// <summary>
        /// Pattern used against response URL for matching responses that require interaction, or <c>null</c> if interaction is never required.
        /// </summary>
        private readonly Regex _loginPromptPattern;

        #endregion

        #region constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicWebSsoRequestBase{T}"/> class.
        /// </summary>
        /// <param name="httpWebRequest">The wrapped <see cref="HttpWebRequest"/>.</param>
        /// <param name="loginPromptPattern"><see cref="Regex"/> pattern used against response URL for matching responses that
        /// require interaction, or <c>null</c> if interaction is never required.</param>
        protected BasicWebSsoRequestBase(HttpWebRequest httpWebRequest, Regex loginPromptPattern)
        {
            _httpWebRequest = httpWebRequest;
            _loginPromptPattern = loginPromptPattern;
        }

        #endregion

        #region methods

        /// <summary>
        /// Gets the <see cref="HttpWebRequest"/> wrapped by this object.
        /// </summary>
        /// <returns>The <see cref="HttpWebRequest"/>.</returns>
        protected override HttpWebRequest GetHttpWebRequest()
        {
            return _httpWebRequest;
        }

        /// <summary>
        /// Executes next request required for getting the final response for the Web SSO request.
        /// </summary>
        /// <param name="response"><see cref="HttpWebResponse"/> representing current HTTP response.</param>
        /// <param name="nextResponseCallback">Callback for receiving <see cref="HttpWebResponse"/> returned by next request, or <c>null</c> if
        /// the given <paramref name="response"/> is the final response to the Web SSO request.</param>
        protected override void GetNextResponse(HttpWebResponse response, NextResponseCallback nextResponseCallback)
        {
            var requiresInteractionPattern = _loginPromptPattern;
            if (requiresInteractionPattern == null || !requiresInteractionPattern.Match(response.ResponseUri.ToString()).Success)
            {
                nextResponseCallback(null, false);
            }
            else
            {
                Interact(response, nextResponseCallback);
            }
        }

        /// <summary>
        /// Called to performed required interaction.
        /// </summary>
        /// <param name="response"><see cref="HttpWebResponse"/> representing current HTTP response.</param>
        /// <param name="nextResponseCallback">Callback for receiving <see cref="HttpWebResponse"/> returned by the interaction step.</param>
        protected abstract void Interact(HttpWebResponse response, NextResponseCallback nextResponseCallback);

        #endregion
    }
}
