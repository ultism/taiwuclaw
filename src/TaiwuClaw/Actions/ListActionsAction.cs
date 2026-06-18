using Newtonsoft.Json.Linq;
using TaiwuClaw.Core;

namespace TaiwuClaw.Actions
{
    /// <summary>内置自描述 action：列出所有已注册 action，便于 agent 发现能力。</summary>
    public class ListActionsAction : IAgentAction
    {
        private readonly ActionRegistry _registry;

        public ListActionsAction(ActionRegistry registry) => _registry = registry;

        public string Name => "list";

        public string Description => "列出所有可用 action（工具）及其说明。";

        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JToken Execute(JObject args)
        {
            var arr = new JArray();
            foreach (var action in _registry.All)
                arr.Add(new JObject { ["name"] = action.Name, ["description"] = action.Description });
            return new JObject { ["actions"] = arr };
        }
    }
}
