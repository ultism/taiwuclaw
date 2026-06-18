using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Agent
{
    /// <summary>模型从一次响应里发起的一次工具调用。</summary>
    public class ToolCall
    {
        public string Id;
        public string Name;
        public JObject Input;
    }

    /// <summary>一次模型响应。Content 是原始内容块数组，须原样回灌历史（保住 thinking 签名）。</summary>
    public class LlmResponse
    {
        /// <summary>助手回合的原始内容块（thinking/text/tool_use…），parentless，可直接塞回 messages。</summary>
        public JArray Content;
        /// <summary>end_turn / tool_use / max_tokens / refusal …</summary>
        public string StopReason;
        public List<ToolCall> ToolCalls = new List<ToolCall>();
    }

    /// <summary>
    /// LLM 通信抽象。内部规范消息形态采用 Anthropic Messages（最丰富，承载 thinking 块）；
    /// 其它 wire format（OpenAI chat/responses）的实现负责在自己边界上做转换。
    /// </summary>
    public interface ILlmClient
    {
        /// <param name="system">系统提示</param>
        /// <param name="tools">Anthropic tools 数组（input_schema）</param>
        /// <param name="messages">规范消息历史（Anthropic Messages 形态）</param>
        LlmResponse CreateMessage(string system, JArray tools, JArray messages);
    }
}
