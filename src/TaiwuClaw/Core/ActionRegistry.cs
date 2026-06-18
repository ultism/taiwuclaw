using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Core
{
    /// <summary>action 名到实现的注册表与分发器。</summary>
    public class ActionRegistry
    {
        private readonly Dictionary<string, IAgentAction> _actions =
            new Dictionary<string, IAgentAction>(StringComparer.OrdinalIgnoreCase);

        public void Register(IAgentAction action) => _actions[action.Name] = action;

        public IEnumerable<IAgentAction> All => _actions.Values;

        /// <summary>按名分发。未知 action 抛异常（由通信层转成错误响应）。</summary>
        public JToken Execute(string name, JObject args)
        {
            if (!_actions.TryGetValue(name, out var action))
                throw new Exception($"unknown action '{name}'");
            return action.Execute(args ?? new JObject());
        }
    }
}
