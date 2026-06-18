using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaiwuClaw.Core;
using UnityEngine;

namespace TaiwuClaw.Agent
{
    /// <summary>面板里渲染的一行对话。</summary>
    public class ChatLine
    {
        public string Role; // user | thinking | assistant | tool | error
        public string Text;
        public ChatLine(string role, string text) { Role = role; Text = text; }
    }

    /// <summary>
    /// in-process agent 循环：交错思考 + 工具调用。
    /// RunTurn 应在后台线程执行（内含阻塞 HTTP）；工具经 MainThreadDispatcher 回主线程执行。
    /// </summary>
    public class AgentRunner
    {
        private const int MaxSteps = 12;

        private readonly ILlmClient _client;
        private readonly ActionRegistry _registry;
        private readonly MainThreadDispatcher _dispatcher;
        private readonly string _system;
        private readonly JArray _tools;
        private readonly JArray _messages = new JArray();

        private readonly List<ChatLine> _transcript = new List<ChatLine>();
        private readonly object _lock = new object();

        public bool Busy { get; private set; }

        public AgentRunner(ILlmClient client, ActionRegistry registry, MainThreadDispatcher dispatcher, string system)
        {
            _client = client;
            _registry = registry;
            _dispatcher = dispatcher;
            _system = system;
            _tools = registry.ToToolsJson();
        }

        public ChatLine[] Snapshot()
        {
            lock (_lock) return _transcript.ToArray();
        }

        /// <summary>清空对话与发给模型的消息历史。运行中忽略（避免与后台回合竞态）。</summary>
        public void Clear()
        {
            if (Busy) return;
            _messages.Clear();
            lock (_lock) _transcript.Clear();
        }

        private void Add(string role, string text)
        {
            lock (_lock) _transcript.Add(new ChatLine(role, text));
        }

        /// <summary>跑完一个用户回合（含交错思考的工具循环）。在后台线程调用。</summary>
        public void RunTurn(string userText)
        {
            Busy = true;
            try
            {
                Add("user", userText);
                _messages.Add(new JObject { ["role"] = "user", ["content"] = userText });

                for (int step = 0; step < MaxSteps; step++)
                {
                    LlmResponse resp = _client.CreateMessage(_system, _tools, _messages);

                    // 关键：原样回灌助手内容块（含 thinking + signature），交错思考才能延续
                    _messages.Add(new JObject { ["role"] = "assistant", ["content"] = resp.Content });

                    foreach (var b in resp.Content.OfType<JObject>())
                    {
                        switch ((string)b["type"])
                        {
                            case "thinking":
                                var th = (string)b["thinking"];
                                if (!string.IsNullOrEmpty(th)) Add("thinking", th);
                                break;
                            case "text":
                                Add("assistant", (string)b["text"]);
                                break;
                        }
                    }

                    if (resp.StopReason != "tool_use" || resp.ToolCalls.Count == 0)
                        break;

                    var results = new JArray();
                    foreach (var call in resp.ToolCalls)
                    {
                        Add("tool", $"{call.Name}({call.Input.ToString(Formatting.None)})");
                        string resultText;
                        try
                        {
                            // 工具回主线程执行（为将来操纵游戏状态的 action 保线程安全）
                            JToken r = _dispatcher.Run(() => _registry.Execute(call.Name, call.Input));
                            resultText = r?.ToString(Formatting.None) ?? "null";
                        }
                        catch (Exception e)
                        {
                            resultText = new JObject { ["error"] = e.Message }.ToString(Formatting.None);
                        }
                        results.Add(new JObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = call.Id,
                            ["content"] = resultText,
                        });
                    }
                    _messages.Add(new JObject { ["role"] = "user", ["content"] = results });
                }
            }
            catch (Exception e)
            {
                Add("error", e.Message);
                Debug.LogWarning("[TaiwuClaw] agent turn error: " + e);
            }
            finally
            {
                Busy = false;
            }
        }
    }
}
