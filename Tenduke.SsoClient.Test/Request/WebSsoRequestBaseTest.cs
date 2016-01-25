using Microsoft.Owin.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tenduke.SsoClient.Request;
using Tenduke.SsoClient.Test.TestUtil;
using System.Net;
using Nancy;
using System.Threading;

namespace Tenduke.SsoClient.Test.Request
{
    [TestFixture]
    public class WebSsoRequestBaseTest
    {
        #region private fields

        /// <summary>
        /// Embedded web server.
        /// </summary>
        private static IDisposable _webServer;

        #endregion

        #region test setup and teardown

        /// <summary>
        /// Test setup.
        /// </summary>
        [TestFixtureSetUp]
        public static void Initialize()
        {
            _webServer = WebApp.Start<NancyOwinStartup>("http://+:8088");
        }

        /// <summary>
        /// Test teardown.
        /// </summary>
        [TestFixtureTearDown]
        public static void CleanUp()
        {
            _webServer.Dispose();
            _webServer = null;
        }

        #endregion

        #region test methods

        /// <summary>
        /// Test GetResponse method.
        /// </summary>
        [Test]
        public void TestGetResponse()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create("http://localhost:8088/");
            httpRequest.CookieContainer = new CookieContainer();
            var webSsoRequest = new TestWebSsoRequest1(httpRequest);
            webSsoRequest.GetResponse(TimeSpan.FromSeconds(5));
            var httpResponse = webSsoRequest.Response;
            Assert.NotNull(httpResponse, "Response must not be null");
            httpResponse.Close();
        }

        /// <summary>
        /// Test timeout behavior of GetResponse method.
        /// </summary>
        [Test]
        [ExpectedException(typeof(TimeoutException))]
        public void TestGetResponseTimeout()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create("http://localhost:8088/");
            httpRequest.CookieContainer = new CookieContainer();
            var webSsoRequest = new TestWebSsoRequest1(httpRequest);
            webSsoRequest.GetResponse(TimeSpan.FromSeconds(0));
        }

        /// <summary>
        /// Test cancel behavior of GetResponse method.
        /// </summary>
        [Test]
        [ExpectedException(typeof(OperationCanceledException))]
        public void TestGetResponseCancel()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create("http://localhost:8088/");
            httpRequest.CookieContainer = new CookieContainer();
            var webSsoRequest = new TestWebSsoRequest2(httpRequest);
            webSsoRequest.GetResponse(TimeSpan.FromMilliseconds(-1));
        }

        #endregion

        #region nested TestWebSsoRequest1 class

        /// <summary>
        /// Implementation of WebSsoRequestBase for the test. This implementation always considers the first HTTP response
        /// as the final response for handling the request.
        /// </summary>
        public class TestWebSsoRequest1 : WebSsoRequestBase<TestWebSsoRequest1>
        {
            private readonly HttpWebRequest _httpWebRequest;

            public TestWebSsoRequest1(HttpWebRequest req)
            {
                _httpWebRequest = req;
            }

            protected override HttpWebRequest GetHttpWebRequest()
            {
                return _httpWebRequest;
            }

            protected override void GetNextResponse(HttpWebResponse response, NextResponseCallback nextResponseCallback)
            {
                nextResponseCallback(null, false);
            }
        }

        #endregion

        #region nested TestWebSsoRequest2 class

        /// <summary>
        /// Implementation of WebSsoRequestBase for the test. This implementation always cancels the request.
        /// </summary>
        public class TestWebSsoRequest2 : WebSsoRequestBase<TestWebSsoRequest2>
        {
            private readonly HttpWebRequest _httpWebRequest;

            public TestWebSsoRequest2(HttpWebRequest req)
            {
                _httpWebRequest = req;
            }

            protected override HttpWebRequest GetHttpWebRequest()
            {
                return _httpWebRequest;
            }

            protected override void GetNextResponse(HttpWebResponse response, NextResponseCallback nextResponseCallback)
            {
                nextResponseCallback(null, true);
            }
        }

        #endregion

        #region nested TestNancyModule class

        /// <summary>
        /// Nancy module for responding to "/" request.
        /// </summary>
        public class TestNancyModule : NancyModule
        {
            public TestNancyModule()
            {
                Get["/"] = parameters =>
                {
                    return "Hello!";
                };
            }
        }

        #endregion
    }
}
