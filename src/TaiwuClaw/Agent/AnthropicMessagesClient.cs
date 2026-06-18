using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Agent
{
    /// <summary>
    /// Anthropic Messages wire format 的 ILlmClient（手写 HTTP + Newtonsoft）。
    /// 支持交错思考（thinking: adaptive）+ 工具循环；可经兼容 Anthropic Messages 的第三方网关使用。
    /// 调用应在后台线程进行（同步阻塞）。
    /// </summary>
    public class AnthropicMessagesClient : ILlmClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        private readonly LlmConfig _cfg;

        public AnthropicMessagesClient(LlmConfig cfg) => _cfg = cfg;

        public LlmResponse CreateMessage(string system, JArray tools, JArray messages)
        {
            var body = new JObject
            {
                ["model"] = _cfg.Model,
                ["max_tokens"] = _cfg.MaxTokens,
                ["messages"] = messages,
            };
            if (!string.IsNullOrEmpty(system)) body["system"] = system;
            if (tools != null && tools.Count > 0) body["tools"] = tools;
            // 交错思考：adaptive 在 4.6+ 自动启用 interleaved thinking；display:summarized 便于面板展示
            if (_cfg.Thinking) body["thinking"] = new JObject { ["type"] = "adaptive", ["display"] = "summarized" };
            if (!string.IsNullOrEmpty(_cfg.Effort)) body["output_config"] = new JObject { ["effort"] = _cfg.Effort };

            using (var req = new HttpRequestMessage(HttpMethod.Post, _cfg.Endpoint))
            {
                req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                ApplyAuth(req);

                HttpResponseMessage resp = Http.SendAsync(req).GetAwaiter().GetResult();
                string text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"LLM HTTP {(int)resp.StatusCode}: {Trunc(text)}");

                return Parse(text);
            }
        }

        private void ApplyAuth(HttpRequestMessage req)
        {
            if (string.Equals(_cfg.AuthStyle, "bearer", StringComparison.OrdinalIgnoreCase))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.ApiKey);
            }
            else // anthropic
            {
                req.Headers.TryAddWithoutValidation("x-api-key", _cfg.ApiKey);
                req.Headers.TryAddWithoutValidation("anthropic-version", _cfg.AnthropicVersion);
            }
            if (!string.IsNullOrEmpty(_cfg.BetaHeader))
                req.Headers.TryAddWithoutValidation("anthropic-beta", _cfg.BetaHeader);
        }

        private static LlmResponse Parse(string text)
        {
            var obj = JObject.Parse(text);
            // DeepClone → parentless，可安全塞回 messages（原样保留 thinking + signature）
            var content = obj["content"] is JArray a ? (JArray)a.DeepClone() : new JArray();

            var result = new LlmResponse
            {
                Content = content,
                StopReason = (string)obj["stop_reason"],
            };
            foreach (var b in content.OfType<JObject>())
            {
                if ((string)b["type"] == "tool_use")
                {
                    result.ToolCalls.Add(new ToolCall
                    {
                        Id = (string)b["id"],
                        Name = (string)b["name"],
                        Input = b["input"] as JObject ?? new JObject(),
                    });
                }
            }
            return result;
        }

        private static string Trunc(string s) => s != null && s.Length > 800 ? s.Substring(0, 800) + "…" : s;
    }
}
