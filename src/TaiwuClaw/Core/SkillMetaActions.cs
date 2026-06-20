using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Core
{
    /// <summary>元工具：开启一个技能，回灌其说明书，并让该技能的工具在后续轮次可用。</summary>
    public class OpenSkillAction : IAgentAction
    {
        private readonly SkillRegistry _reg;
        public OpenSkillAction(SkillRegistry reg) => _reg = reg;

        public string Name => "open_skill";

        public string Description =>
            "开启一个技能：返回它的说明书，并把它的专用工具激活进工具列表（下一轮起可调用）。" +
            "参数 skill = 技能 id（见系统提示里的「可用技能」目录）。仅在需要该技能时调用。";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["skill"] = new JObject { ["type"] = "string", ["description"] = "技能 id" },
            },
            ["required"] = new JArray { "skill" },
        };

        public JToken Execute(JObject args)
        {
            string id = (string)args["skill"];
            string instructions = _reg.Open(id);
            if (instructions == null)
                return new JObject
                {
                    ["error"] = $"没有名为 '{id}' 的技能",
                    ["available"] = new JArray(_reg.SkillIds()),
                };
            return new JObject
            {
                ["skill"] = id,
                ["instructions"] = instructions,
                ["note"] = "该技能的工具已激活，从下一轮起可直接调用。",
            };
        }
    }

    /// <summary>元工具：关闭一个技能，移除其工具与说明以释放上下文。</summary>
    public class CloseSkillAction : IAgentAction
    {
        private readonly SkillRegistry _reg;
        public CloseSkillAction(SkillRegistry reg) => _reg = reg;

        public string Name => "close_skill";

        public string Description =>
            "关闭一个技能，把它的专用工具从工具列表移除以释放上下文。参数 skill = 技能 id。用完某技能后调用。";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["skill"] = new JObject { ["type"] = "string", ["description"] = "技能 id" },
            },
            ["required"] = new JArray { "skill" },
        };

        public JToken Execute(JObject args)
        {
            string id = (string)args["skill"];
            bool wasOpen = _reg.Close(id);
            return new JObject { ["skill"] = id, ["closed"] = wasOpen };
        }
    }
}
