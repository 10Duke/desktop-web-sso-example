using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Owin.Hosting;
using Nancy;
using Nancy.Helpers;
using Nancy.Responses;
using NUnit.Framework;
using Tenduke.SsoClient.Request;
using Tenduke.SsoClient.Test.TestUtil;

namespace Tenduke.SsoClient.Test.Request
{
    [TestFixture]
    public class BasicWebSsoRequestBaseTest
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
        /// Test getting a consumer resource through a request chain that requires interaction.
        /// </summary>
        [Test]
        public void TestGetConsumerResource()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:8088/consumerDoOAuthLogin");
            httpRequest.CookieContainer = new CookieContainer();
            var loginFormValues = new Dictionary<string, string>
            {
                ["userName"] = "testUser",
                ["password"] = "verysecret"
            };
            var webSsoRequest = new TestBasicWebSsoRequest1(httpRequest, new Regex(".*\\/providerOAuthLoginPage.*"), loginFormValues);
            webSsoRequest.GetResponse(TimeSpan.FromMilliseconds(-1));
            var response = webSsoRequest.Response;
            using (var responseStream = response.GetResponseStream())
            {
                using (var reader = new StreamReader(responseStream))
                {
                    var responseContent = reader.ReadToEnd();
                    Assert.AreEqual("LoginRequiredResource", responseContent, "Invalid response content");
                }
            }
        }

        #endregion

        #region nested TestBasicWebSsoRequest1 class

        /// <summary>
        /// Implementation of BaicWebSsoRequestBase for the test.
        /// </summary>
        public class TestBasicWebSsoRequest1 : BasicWebSsoRequestBase<TestBasicWebSsoRequest1>
        {
            private readonly Dictionary<string, string> _formValues;

            public TestBasicWebSsoRequest1(HttpWebRequest req, Regex loginPromptPattern, Dictionary<string, string> formValues)
                : base(req, loginPromptPattern)
            {
                _formValues = formValues;
            }

            protected override void Interact(HttpWebResponse response, NextResponseCallback nextResponseCallback)
            {
                using (var responseStream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        var content = reader.ReadToEnd();
                        var xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(content);
                        var formNode = xmlDoc.GetElementsByTagName("form")[0];
                        var builder = new StringBuilder();
                        foreach (XmlNode childNode in formNode.ChildNodes)
                        {
                            if (childNode.LocalName.Equals("input"))
                            {
                                if (builder.Length > 0)
                                {
                                    builder.Append('&');
                                }

                                var name = childNode.Attributes["name"].Value;
                                builder.Append(HttpUtility.UrlEncode(name, Encoding.UTF8));
                                string value;
                                if (_formValues.ContainsKey(name))
                                {
                                    value = _formValues[name];
                                }
                                else if (childNode.Attributes["value"] != null)
                                {
                                    value = childNode.Attributes["value"].Value;
                                }
                                else
                                {
                                    value = null;
                                }

                                if (value != null)
                                {
                                    builder.Append('=').Append(HttpUtility.UrlEncode(value, Encoding.UTF8));
                                }
                            }
                        }

                        var action = formNode.Attributes["action"].Value;
                        var requestUri = new Uri(response.ResponseUri, action);
                        var request = (HttpWebRequest) WebRequest.Create(requestUri);
                        request.Method = "POST";
                        request.ContentType = "application/x-www-form-urlencoded";
                        using (var dataStream = request.GetRequestStream())
                        {
                            using (var writer = new StreamWriter(dataStream))
                            {
                                writer.Write(builder.ToString());
                            }
                        }

                        var nextResponse = (HttpWebResponse) request.GetResponse();
                        nextResponseCallback(nextResponse, false);
                    }
                }
            }
        }

        #endregion

        #region nested TestNancyModule class

        /// <summary>
        /// Nancy module for handling test requests.
        /// </summary>
        public class TestNancyModule : NancyModule
        {
            public TestNancyModule()
            {
                Get["/consumerDoOAuthLogin"] = parameters =>
                {
                    if (Request.Query["loginDone"] == null)
                    {
                        return Response.AsRedirect("http://localhost:8088/providerOAuthLoginPage?a=b&continueTo=http://127.0.0.1:8088/consumerDoOAuthLogin");
                    }
                    else
                    {
                        return "LoginRequiredResource";
                    }
                };

                Get["/providerOAuthLoginPage"] = parameters =>
                {
                    return "<html><body><form action=\"/providerOAuthLoginHandler\" method=\"post\"><input type=\"text\" name=\"userName\"/><input type=\"password\" name=\"password\"/><input type=\"hidden\" name=\"continueTo\" value=\"" + Request.Query["continueTo"] + "\"/></form></body></html>";
                };

                Post["/providerOAuthLoginHandler"] = parameters =>
                {
                    var redirectUri = new UriBuilder((string)Request.Form["continueTo"]);
                    var query = HttpUtility.ParseQueryString(redirectUri.Query);
                    query["loginDone"] = "true";
                    redirectUri.Query = query.ToString();
                    return new RedirectResponse(redirectUri.ToString());
                };
            }
        }

        #endregion
    }
}
