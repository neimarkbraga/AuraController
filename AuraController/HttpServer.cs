using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuraController
{
    struct RouteHandler
    {
        public string method;
        public string path;
        public Action<HttpListenerRequest, HttpListenerResponse, Func<bool>> handler;
    }

    class HttpServer
    {
        private HttpListener listener;
   
        private List<RouteHandler> handlers = new List<RouteHandler>();

        private async static void Send(byte[] data, HttpListenerResponse res)
        {
            res.ContentEncoding = Encoding.UTF8;
            res.ContentLength64 = data.LongLength;
            await res.OutputStream.WriteAsync(data, 0, data.Length);
            res.Close();
        }

        private bool IsResponseResolved(HttpListenerResponse res)
        {
            try
            {
                res.OutputStream.GetType();
                return false;
            }
            catch (ObjectDisposedException e)
            {
                return true;
            }
        }

        private bool PathMatched(HttpListenerRequest req, string handlerPath)
        {
            string reqPath = new Regex(@"\?.*").Replace(req.RawUrl, "");
            if (WebUtility.UrlDecode(reqPath) == handlerPath)
                return true;
            
            if (new Regex(@"\/:[^\/]+").IsMatch(handlerPath))
            {
                string[] rIndexes = reqPath.Split('/');
                string[] hIndexes = handlerPath.Split('/');

                if (rIndexes.Length != hIndexes.Length)
                    return false;

                for (int i = 0; i < hIndexes.Length; i++)
                {
                    string rIndex = WebUtility.UrlDecode(rIndexes[i]);
                    string hIndex = hIndexes[i];

                    if (rIndex != hIndex && !new Regex(@"^:[^\/]+").IsMatch(hIndex))
                        return false;
                }

                return true;
            }
            
            return false;
        }

        private async Task HandleIncomingConnections()
        {
            while (true)
            {
                Exception error = null;
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse res = ctx.Response;

                foreach (RouteHandler handler in handlers)
                {
                    if ((req.HttpMethod == handler.method) && PathMatched(req, handler.path))
                    {
                        try
                        {
                            bool next = false;
                            req.Headers.Set("$SERVER_HANDLER_PATH", handler.path);
                            handler.handler(req, res, () => next = true);
                            if (!next)
                            {
                                if (!IsResponseResolved(res))
                                    res.Close();
                                break;
                            }
                        } catch (Exception e)
                        {
                            error = e;
                            break;
                        }
                    }
                }

                // already resolved
                if (IsResponseResolved(res)) continue;
                
                // has redirect
                if (res.RedirectLocation != null)
                {
                    res.Close();
                    continue;
                }

                // has error
                if (error != null)
                {
                    res.StatusCode = 500;
                    SendJson(new
                    {
                        message = error.Message,
                        status = 500,
                        data = error.StackTrace
                    }, res);
                    continue;
                }

                // default response
                res.StatusCode = 404;
                SendJson(new
                {
                    message = "Not Found",
                    status = 404
                }, res);
            }
        }

        public void Post(string path, Action<HttpListenerRequest, HttpListenerResponse, Func<bool>>  handler)
        {
            RouteHandler _handler = new RouteHandler();
            _handler.method = "POST";
            _handler.path = path;
            _handler.handler = handler;
            handlers.Add(_handler);
        }

        public void Get(string path, Action<HttpListenerRequest, HttpListenerResponse, Func<bool>> handler)
        {
            RouteHandler _handler = new RouteHandler();
            _handler.method = "GET";
            _handler.path = path;
            _handler.handler = handler;
            handlers.Add(_handler);
        }

        public void Put(string path, Action<HttpListenerRequest, HttpListenerResponse, Func<bool>> handler)
        {
            RouteHandler _handler = new RouteHandler();
            _handler.method = "PUT";
            _handler.path = path;
            _handler.handler = handler;
            handlers.Add(_handler);
        }

        public void Delete(string path, Action<HttpListenerRequest, HttpListenerResponse, Func<bool>> handler)
        {
            RouteHandler _handler = new RouteHandler();
            _handler.method = "DELETE";
            _handler.path = path;
            _handler.handler = handler;
            handlers.Add(_handler);
        }

        public static void SendText(string text, HttpListenerResponse res)
        {
            res.ContentType = "text/html";
            Send(Encoding.UTF8.GetBytes(text), res);
        }

        public static void SendJson(dynamic value, HttpListenerResponse res)
        {
            JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.General);
            options.WriteIndented = true;

            string text = JsonSerializer.Serialize(value, options);
            res.ContentType = "application/json";
            Send(Encoding.UTF8.GetBytes(text), res);
        }

        public static Dictionary<string, string> GetParams(HttpListenerRequest req)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            string reqPath = new Regex(@"\?.*").Replace(req.RawUrl, "");
            string handlerPath = req.Headers.Get("$SERVER_HANDLER_PATH");
            string[] rIndexes = reqPath.Split('/');
            string[] hIndexes = handlerPath.Split('/');

            if (rIndexes.Length == hIndexes.Length)
            {
                for (int i = 0; i < hIndexes.Length; i++)
                {
                    string rIndex = WebUtility.UrlDecode(rIndexes[i]);
                    string hIndex = hIndexes[i];
                    if (new Regex(@"^:[^\/]+").IsMatch(hIndex))
                        param.Add(new Regex(@"^:").Replace(hIndex, ""), rIndex);
                }
            }
            return param;
        }

        public static string GetBodyContent(HttpListenerRequest req)
        {
            Stream body = req.InputStream;
            Encoding encoding = req.ContentEncoding;
            StreamReader reader = new StreamReader(body, encoding);
            string content = reader.ReadToEnd();
            body.Close();
            reader.Close();
            return content;
        }

        public void Listen(int port)
        {
            string url = "http://127.0.0.1:" + port + "/";
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            
            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();
            listener.Close();
        } 
    }
}