//-----------------------------------------------------------------------
// <copyright file="WebSsoRequestBase.cs" company="10Duke Software">
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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// References a method to be called when handling a <see cref="WebSsoRequestBase{T}"/> is completed.
    /// </summary>
    /// <typeparam name="T">Request type derived from <see cref="WebSsoRequestBase{T}"/>.</typeparam>
    /// <param name="webSsoRequest">The completed request.</param>
    /// <param name="canceled"><c>true</c> if request handling was canceled, <c>false</c> otherwise.</param>
    public delegate void WebSsoRequestCallback<T>(T webSsoRequest, bool canceled) where T : WebSsoRequestBase<T>;

    /// <summary>
    /// References a method to be called when received response for next step in handling the request.
    /// </summary>
    /// <param name="nextResponse"><see cref="HttpWebResponse"/> returned by next request, or <c>null</c> if no further requests required.</param>
    /// <param name="cancel"><c>true</c> to cancel request handling, <c>false</c> otherwise.</param>
    public delegate void NextResponseCallback(HttpWebResponse nextResponse, bool cancel);

    /// <summary>
    /// Base classes for classes that wrap a <see cref="HttpWebRequest"/> and support authentication in a Web Single Sign-On environment.
    /// </summary>
    public abstract class WebSsoRequestBase<T> where T : WebSsoRequestBase<T>
    {
        #region constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSsoRequestBase{T}"/> class.
        /// </summary>
        protected WebSsoRequestBase()
        {
        }

        #endregion

        #region properties

        /// <summary>
        /// Asynchronous request status object, used during request handling.
        /// </summary>
        protected IAsyncResult AsyncResult { get; set; }

        /// <summary>
        /// The HTTP response, set when handling request is ready.
        /// </summary>
        public HttpWebResponse Response { get; protected set; }

        #endregion

        #region methods

        /// <summary>
        /// Gets the <see cref="HttpWebRequest"/> wrapped by this object.
        /// </summary>
        /// <returns>The <see cref="HttpWebRequest"/>.</returns>
        protected abstract HttpWebRequest GetHttpWebRequest();

        /// <summary>
        /// Executes next request required for getting the final response for the <see cref="WebSsoRequestBase{T}"/>.
        /// </summary>
        /// <param name="response"><see cref="HttpWebResponse"/> representing current HTTP response.</param>
        /// <param name="nextResponseCallback">Callback for receiving <see cref="HttpWebResponse"/> returned by next request, or <c>null</c> if
        /// the given <paramref name="response"/> is the final response to the <see cref="WebSsoRequestBase{T}"/>.</param>
        protected abstract void GetNextResponse(HttpWebResponse response, NextResponseCallback nextResponseCallback);

        /// <summary>
        /// Begins getting HTTP response asynchronously.
        /// </summary>
        /// <param name="responseCallback"><see cref="WebSsoRequestCallback{T}"/> called when handling the request is completed.
        /// The callback method is responsible for handling and closing the response (<see cref="WebSsoRequestBase{T}.Response"/>).</param>
        /// <returns>Returns this object.</returns>
        /// <exception cref="InvalidOperationException">
        /// <para><see cref="HttpWebRequest"/> is not initialized, i.e. calling <see cref="GetHttpWebRequest"/> returns <c>null</c>.</para>
        /// <para>-or-</para>
        /// <para>The stream is already in use by a previous call to <see cref="HttpWebRequest.BeginGetResponse(AsyncCallback, object)"/>.</para>
        /// <para>-or-</para>
        /// <para>TransferEncoding is set to a value and SendChunked is <c>false</c>.</para>
        /// <para>-or-</para>
        /// <para>The thread pool is running out of threads.</para>
        /// </exception>
        /// <exception cref="ProtocolViolationException">
        /// <para>Method is GET or HEAD, and either ContentLength is greater than zero or SendChunked is <c>true</c>.</para>
        /// <para>-or-</para>
        /// <para>KeepAlive is <c>true</c>, AllowWriteStreamBuffering is <c>false</c>, and either ContentLength is -1, SendChunked is <c>false</c> and Method is POST or PUT.</para>
        /// <para>-or-</para>
        /// <para>The HttpWebRequest has an entity body but the BeginGetResponse method is called without calling the BeginGetRequestStream method.</para>
        /// <para>-or-</para>
        /// <para>The ContentLength is greater than zero, but the application does not write all of the promised data.</para>
        /// </exception>
        /// <exception cref="WebException">Abort was previously called.</exception>
        public T BeginGetResponse(WebSsoRequestCallback<T> responseCallback)
        {
            var request = GetHttpWebRequest();
            if (request == null)
            {
                throw new InvalidOperationException("HttpWebRequest must be initialized");
            }

            var cookieContainer = request.CookieContainer;
            if (cookieContainer == null)
            {
                throw new InvalidOperationException("CookieContainer must not be null");
            }

            var asyncResult = request.BeginGetResponse(result =>
            {
                var response = (HttpWebResponse)request.EndGetResponse(result);
                HandleResponse(response, responseCallback);
            }, this);

            AsyncResult = asyncResult;

            return (T)this;
        }

        /// <summary>
        /// Gets HTTP response synchronously. After calling this method, caller can access the response in <see cref="WebSsoRequestBase{T}.Response"/>.
        /// Caller of this method is responsible for handling and closing the response.
        /// </summary>
        /// <param name="timeout">The request timeout, or a <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.</param>
        /// <returns>Returns this object.</returns>
        /// <exception cref="TimeoutException">Thrown if timeout occurs.</exception>
        /// <exception cref="OperationCanceledException">Thrown if handling the <see cref="WebSsoRequestBase{T}"/> was canceled.</exception>
        /// <exception cref="InvalidOperationException">
        /// <para><see cref="HttpWebRequest"/> is not initialized, i.e. calling <see cref="GetHttpWebRequest"/> returns <c>null</c>.</para>
        /// <para>-or-</para>
        /// <para>The stream is already in use by a previous call to <see cref="HttpWebRequest.BeginGetResponse(AsyncCallback, object)"/>.</para>
        /// <para>-or-</para>
        /// <para>TransferEncoding is set to a value and SendChunked is <c>false</c>.</para>
        /// <para>-or-</para>
        /// <para>The thread pool is running out of threads.</para>
        /// </exception>
        /// <exception cref="ProtocolViolationException">
        /// <para>Method is GET or HEAD, and either ContentLength is greater than zero or SendChunked is <c>true</c>.</para>
        /// <para>-or-</para>
        /// <para>KeepAlive is <c>true</c>, AllowWriteStreamBuffering is <c>false</c>, and either ContentLength is -1, SendChunked is <c>false</c> and Method is POST or PUT.</para>
        /// <para>-or-</para>
        /// <para>The HttpWebRequest has an entity body but the BeginGetResponse method is called without calling the BeginGetRequestStream method.</para>
        /// <para>-or-</para>
        /// <para>The ContentLength is greater than zero, but the application does not write all of the promised data.</para>
        /// </exception>
        /// <exception cref="WebException">Abort was previously called.</exception>
        public T GetResponse(TimeSpan timeout)
        {
            var doneEvent = new ManualResetEvent(false);
            var done = false;
            var webSsoRequestCanceled = false;
            BeginGetResponse((webSsoRequest, canceled) =>
                {
                    webSsoRequestCanceled = canceled;
                    done = true;
                    doneEvent.Set();
                }
            );

            doneEvent.WaitOne(timeout);
            if (webSsoRequestCanceled)
            {
                throw new OperationCanceledException("Request handling canceled");
            }

            if (!done)
            {
                throw new TimeoutException("Request timeout");
            }

            return (T)this;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Handles HTTP response.
        /// </summary>
        /// <param name="response"><see cref="HttpWebResponse"/> representing HTTP response.</param>
        /// <param name="responseCallback"><see cref="WebSsoRequestCallback{T}"/> called when handling the request is completed.
        /// The callback method is responsible for handling and closing the response (<see cref="WebSsoRequestBase{T}.Response"/>).</param>
        private void HandleResponse(HttpWebResponse response, WebSsoRequestCallback<T> responseCallback)
        {
            GetNextResponse(response, (nextResponse, cancel) =>
                {
                    if (cancel)
                    {
                        response?.Dispose();
                        responseCallback((T) this, true);
                    }
                    else if (nextResponse == null)
                    {
                        Response = response;
                        responseCallback((T)this, false);
                    }
                    else
                    {
                        response?.Dispose();
                        HandleResponse(nextResponse, responseCallback);
                    }
                }
            );
        }

        #endregion
    }
}
