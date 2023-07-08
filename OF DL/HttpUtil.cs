using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace WidevineClient
{
    class HttpUtil
    {
        public static HttpClient Client { get; set; } = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            //Proxy = null
        });

        public static byte[] PostData(string URL, Dictionary<string, string> headers, string postData)
        {
            var mediaType = postData.StartsWith("{") ? "application/json" : "application/x-www-form-urlencoded";
            StringContent content = new StringContent(postData, Encoding.UTF8, mediaType);
            //ByteArrayContent content = new ByteArrayContent(postData);

            HttpResponseMessage response = Post(URL, headers, content);
            byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
            return bytes;
        }

        public static byte[] PostData(string URL, Dictionary<string, string> headers, byte[] postData)
        {
            ByteArrayContent content = new ByteArrayContent(postData);

            HttpResponseMessage response = Post(URL, headers, content);
            byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
            return bytes;
        }

        public static byte[] PostData(string URL, Dictionary<string, string> headers, Dictionary<string, string> postData)
        {
            FormUrlEncodedContent content = new FormUrlEncodedContent(postData);

            HttpResponseMessage response = Post(URL, headers, content);
            byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
            return bytes;
        }

        public static string GetWebSource(string URL, Dictionary<string, string> headers = null)
        {
            HttpResponseMessage response = Get(URL, headers);
            byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
            return Encoding.UTF8.GetString(bytes);
        }

        public static byte[] GetBinary(string URL, Dictionary<string, string> headers = null)
        {
            HttpResponseMessage response = Get(URL, headers);
            byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
            return bytes;
        }
        public static string GetString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        static HttpResponseMessage Get(string URL, Dictionary<string, string> headers = null)
        {
            HttpRequestMessage request = new HttpRequestMessage()
            {
                RequestUri = new Uri(URL),
                Method = HttpMethod.Get
            };

            if (headers != null)
                foreach (KeyValuePair<string, string> header in headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return Send(request);
        }

        static HttpResponseMessage Post(string URL, Dictionary<string, string> headers, HttpContent content)
        {
            HttpRequestMessage request = new HttpRequestMessage()
            {
                RequestUri = new Uri(URL),
                Method = HttpMethod.Post,
                Content = content
            };

            if (headers != null)
                foreach (KeyValuePair<string, string> header in headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return Send(request);
        }

        static HttpResponseMessage Send(HttpRequestMessage request)
        {
            return Client.SendAsync(request).Result;
        }
    }
}
