using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace BMAUtils
{
    public static class BMAUtils
    {
        internal static BMAHelper[] m_helpers;

        static BMAUtils()
        {
            m_helpers = new BMAHelper[3];
            for (int i = 0; i < 3; i++)
                m_helpers[i] = new BMAHelper();
        }

        public static BMAHelper GetOrReserveBMAHelper(string owner, ILogger log)
        {
            foreach (BMAHelper helper in m_helpers)
                if (helper.m_isUsed && helper.m_owner == owner)
                {
                    log.LogInformation("Slot[{object}] found in use by owner: {owner}", helper.GetHashCode(), owner);
                    return helper;
                }

            foreach (BMAHelper helper in m_helpers)
                if (!helper.m_isUsed)
                {
                    helper.m_isUsed = true;
                    helper.m_owner = owner;
                    var parts = owner.Split(new char[] {' ','\t' }, 2);
                    helper.m_first = parts[0];
                    helper.m_rest = parts.Length == 2 ? parts[1] : "";
                    log.LogInformation("" +
                        "Slot[{object}] given to owner: {owner}, and initialized[{init}",
                        helper.GetHashCode(), owner, helper.m_isVolunteerHourPageDownloaded);
                    return helper;
                }

            return null;
        }

    }

    public class BMAHelper
    {
        private static readonly Encoding encoding = Encoding.UTF8;
        private static readonly string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36";

        internal bool m_isUsed;
        internal string m_owner;
        internal string m_first;
        internal string m_rest;
        private string m_cookies;
        private string m_volunteerPageContents;
        internal bool m_isVolunteerHourPageDownloaded;

        private const string m_defaultGetURL = "http://bma1.ca/record-volunteer-hours-c251.php";
        private const string m_defaultPostURL = m_defaultGetURL;

        private string m_getURL;
        public string GetURL {
            get
            {
                return m_getURL == null ? m_defaultGetURL : m_getURL;
            }
            set
            {
                m_getURL = value;
            }
        }

        private string m_postURL;
        public string PostURL
        {
            get
            {
                return m_postURL == null ? m_defaultPostURL : m_postURL;
            }

            set
            {
                m_postURL = value;
            }
        }

        public bool RetrieveVolunteerHourPage(ILogger log)
        {
            using (WebClient client = new WebClient())
            {
                try
                {
                    m_volunteerPageContents = client.DownloadString(GetURL);
                    m_cookies = client.ResponseHeaders["Set-Cookie"];
                    m_isVolunteerHourPageDownloaded = true;
                    return true;
                }
                catch (WebException wExc)
                {
                    log.LogError(wExc, "failed to download BMA VolunteerHoursPage");
                    return false;
                }
            }
        }

        public string Cookies {  get
            {
                return m_cookies;
            }
        }

        public bool PostVolunteerPage(string first, string last, string task, int hours, ILogger log)
        {
            string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;
            Dictionary<string, object> postParameters = new Dictionary<string, object>();
            postParameters.Add("JFREXPIRYDATE", "2022-04-03");
            postParameters.Add("JFRFIRSTNAME", first);
            postParameters.Add("JFRLASTNAME", last);
            postParameters.Add("fbField3149", hours);
            postParameters.Add("fbField3148", task);
            postParameters.Add("command", "submitForm");
            postParameters.Add("JFRPAID", "Y");
            postParameters.Add("ID", 70);
            postParameters.Add("fbVisitVerify", 6579);

            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            HttpWebResponse response = PostForm(PostURL, UserAgent, contentType, Cookies, formData);

            return response != null ? true : false;
        }

        private static HttpWebResponse PostForm(string postUrl, string userAgent, string contentType, string cookies, byte[] formData)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null)
            {
                throw new NullReferenceException("request is not a http request");
            }

            // Set up the request properties.
            request.Method = "POST";
            request.ContentType = contentType;
            request.UserAgent = userAgent;
            request.ContentLength = formData.Length;
            request.CookieContainer = new CookieContainer();
            CookieCollection cc = GetAllCookiesFromHeader(cookies, new Uri(postUrl).Host);
            foreach(Cookie cookie in cc)
            {
                request.CookieContainer.Add(cookie);
            }

            // You could add authentication here as well if needed:
            // request.PreAuthenticate = true;
            // request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
            // request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("username" + ":" + "password")));

            // Send the form data to the request.
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(formData, 0, formData.Length);
                requestStream.Close();
            }

            return request.GetResponse() as HttpWebResponse;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                    boundary,
                    param.Key,
                    param.Value);
                formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }
        public static CookieCollection GetAllCookiesFromHeader(string strHeader, string strHost)
        {
            ArrayList al = new ArrayList();
            CookieCollection cc = new CookieCollection();
            if (strHeader != string.Empty)
            {
                al = ConvertCookieHeaderToArrayList(strHeader);
                cc = ConvertCookieArraysToCookieCollection(al, strHost);
            }
            return cc;
        }


        private static ArrayList ConvertCookieHeaderToArrayList(string strCookHeader)
        {
            strCookHeader = strCookHeader.Replace("\r", "");
            strCookHeader = strCookHeader.Replace("\n", "");
            string[] strCookTemp = strCookHeader.Split(',');
            ArrayList al = new ArrayList();
            int i = 0;
            int n = strCookTemp.Length;
            while (i < n)
            {
                if (strCookTemp[i].IndexOf("expires=", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    al.Add(strCookTemp[i] + "," + strCookTemp[i + 1]);
                    i = i + 1;
                }
                else
                {
                    al.Add(strCookTemp[i]);
                }
                i = i + 1;
            }
            return al;
        }


        private static CookieCollection ConvertCookieArraysToCookieCollection(ArrayList al, string strHost)
        {
            CookieCollection cc = new CookieCollection();

            int alcount = al.Count;
            string strEachCook;
            string[] strEachCookParts;
            for (int i = 0; i < alcount; i++)
            {
                strEachCook = al[i].ToString();
                strEachCookParts = strEachCook.Split(';');
                int intEachCookPartsCount = strEachCookParts.Length;
                string strCNameAndCValue = string.Empty;
                string strPNameAndPValue = string.Empty;
                string strDNameAndDValue = string.Empty;
                string[] NameValuePairTemp;
                Cookie cookTemp = new Cookie();

                for (int j = 0; j < intEachCookPartsCount; j++)
                {
                    if (j == 0)
                    {
                        strCNameAndCValue = strEachCookParts[j];
                        if (strCNameAndCValue != string.Empty)
                        {
                            int firstEqual = strCNameAndCValue.IndexOf("=");
                            string firstName = strCNameAndCValue.Substring(0, firstEqual);
                            string allValue = strCNameAndCValue.Substring(firstEqual + 1, strCNameAndCValue.Length - (firstEqual + 1));
                            cookTemp.Name = firstName;
                            cookTemp.Value = allValue;
                        }
                        continue;
                    }
                    if (strEachCookParts[j].IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');
                            if (NameValuePairTemp[1] != string.Empty)
                            {
                                cookTemp.Path = NameValuePairTemp[1];
                            }
                            else
                            {
                                cookTemp.Path = "/";
                            }
                        }
                        continue;
                    }

                    if (strEachCookParts[j].IndexOf("domain", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');

                            if (NameValuePairTemp[1] != string.Empty)
                            {
                                cookTemp.Domain = NameValuePairTemp[1];
                            }
                            else
                            {
                                cookTemp.Domain = strHost;
                            }
                        }
                        continue;
                    }
                }

                if (cookTemp.Path == string.Empty)
                {
                    cookTemp.Path = "/";
                }
                if (cookTemp.Domain == string.Empty)
                {
                    cookTemp.Domain = strHost;
                }
                cc.Add(cookTemp);
            }
            return cc;
        }
    }
}
