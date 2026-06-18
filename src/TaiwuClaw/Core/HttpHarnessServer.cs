using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TaiwuClaw.Core
{
    /// <summary>
    /// 极简 loopback HTTP 服务（TcpListener 手写，避开 HttpListener 的 URL ACL 权限坑）。
    /// 协议：POST 任意路径，body 为 JSON {"name":"action名","args":{...}}。
    /// 响应：{"ok":true,"result":...} 或 {"ok":false,"error":"..."}。
    /// </summary>
    public class HttpHarnessServer
    {
        private readonly ActionRegistry _registry;
        private readonly MainThreadDispatcher _dispatcher;
        private readonly int _port;
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public HttpHarnessServer(ActionRegistry registry, MainThreadDispatcher dispatcher, int port)
        {
            _registry = registry;
            _dispatcher = dispatcher;
            _port = port;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "TaiwuClawHarness" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { /* ignore */ }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; } // listener stopped
                try { HandleClient(client); }
                catch (Exception e) { Debug.LogWarning("[TaiwuClaw] request handler error: " + e); }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                int contentLength = ReadHeaders(stream);
                string body = ReadBody(stream, contentLength);

                JObject respObj;
                int status;
                try
                {
                    var req = string.IsNullOrEmpty(body) ? new JObject() : JObject.Parse(body);
                    string name = (string)req["name"];
                    if (string.IsNullOrEmpty(name))
                        throw new Exception("missing 'name'");
                    var actionArgs = req["args"] as JObject ?? new JObject();

                    JToken result = _dispatcher.Run(() => _registry.Execute(name, actionArgs));
                    respObj = new JObject { ["ok"] = true, ["result"] = result };
                    status = 200;
                }
                catch (Exception e)
                {
                    respObj = new JObject { ["ok"] = false, ["error"] = e.Message };
                    status = 400;
                }

                WriteResponse(stream, status, respObj.ToString(Formatting.None));
            }
        }

        /// <summary>逐字节读到 \r\n\r\n，返回 Content-Length。</summary>
        private static int ReadHeaders(NetworkStream stream)
        {
            var sb = new StringBuilder();
            int b1 = -1, b2 = -1, b3 = -1, cur;
            while ((cur = stream.ReadByte()) != -1)
            {
                sb.Append((char)cur);
                if (b3 == '\r' && b2 == '\n' && b1 == '\r' && cur == '\n') break;
                b3 = b2; b2 = b1; b1 = cur;
            }
            int contentLength = 0;
            foreach (var line in sb.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
            }
            return contentLength;
        }

        private static string ReadBody(NetworkStream stream, int contentLength)
        {
            if (contentLength <= 0) return "";
            var buf = new byte[contentLength];
            int read = 0;
            while (read < contentLength)
            {
                int n = stream.Read(buf, read, contentLength - read);
                if (n <= 0) break;
                read += n;
            }
            return Encoding.UTF8.GetString(buf, 0, read);
        }

        private static void WriteResponse(NetworkStream stream, int status, string json)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(json);
            var head = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Bad Request")}\r\n" +
                       "Content-Type: application/json; charset=utf-8\r\n" +
                       $"Content-Length: {bodyBytes.Length}\r\n" +
                       "Connection: close\r\n\r\n";
            var headBytes = Encoding.ASCII.GetBytes(head);
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }
    }
}
