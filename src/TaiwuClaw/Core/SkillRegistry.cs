using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Core
{
    /// <summary>
    /// 技能注册表 / 渐进披露的总调度（取代旧的扁平 ActionRegistry）。
    ///
    /// 三层工具：
    ///   1. 核心 action：始终在 tools 里（如 encyclopedia_query）。RegisterCore。
    ///   2. 元工具：open_skill / close_skill，<b>仅当注册了至少一个技能时</b>才出现（零技能=零开销，行为同旧版）。
    ///   3. 技能 action：仅当其所属技能被 open 后才进 tools。
    ///
    /// 线程：open 集合在主线程被工具执行改写（open_skill/close_skill），在后台线程被 ToToolsJson 读取，故加锁。
    ///
    /// TODO(磁盘技能)：将来扫描 persistentDataPath/TaiwuClaw/Skills/*&#47;SKILL.md，解析 frontmatter
    ///   （name/desc）+ 正文为一个 Actions 为空的 ISkill 实现，逐个 RegisterSkill 即可并入。
    ///   接口（ISkill）与披露机制已就位，此处只差一个文件加载器。用户 DIY 空间目前较小，先留接口。
    /// TODO(配置系统)：技能多起来后 harness 配置需重构——单文件 llm.json 难承载「按技能开关/各自参数」，
    ///   届时把技能清单与默认开启项纳入配置；见 Agent/LlmConfig.cs 同名 TODO。
    /// </summary>
    public class SkillRegistry
    {
        private readonly List<IAgentAction> _core = new List<IAgentAction>();
        private readonly List<IAgentAction> _meta = new List<IAgentAction>();
        private readonly Dictionary<string, ISkill> _skills =
            new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _open = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 扁平分发表：核心 + 元 + 所有技能的 action（无论开关），按名直查执行。
        // 模型只能调用它看得见的工具，故未开启技能的 action 留在表里也不会被误调。
        private readonly Dictionary<string, IAgentAction> _dispatch =
            new Dictionary<string, IAgentAction>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new object();

        public SkillRegistry()
        {
            // 元工具随注册表自带；是否对模型可见由 ToToolsJson 按「有无技能」决定。
            AddMeta(new OpenSkillAction(this));
            AddMeta(new CloseSkillAction(this));
        }

        private void AddMeta(IAgentAction a) { _meta.Add(a); _dispatch[a.Name] = a; }

        /// <summary>注册常驻能力（始终可见）。</summary>
        public void RegisterCore(IAgentAction action)
        {
            _core.Add(action);
            _dispatch[action.Name] = action;
        }

        /// <summary>注册一个技能（默认关闭，其 action 进分发表但不进 tools 直到被 open）。</summary>
        public void RegisterSkill(ISkill skill)
        {
            _skills[skill.Id] = skill;
            foreach (var a in skill.Actions)
                _dispatch[a.Name] = a;
        }

        public int SkillCount => _skills.Count;

        /// <summary>给 system prompt 的常驻技能目录（关闭态唯一的上下文成本）。无技能时返回空串。</summary>
        public string CatalogText()
        {
            if (_skills.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine("可用技能（默认关闭，其工具不在工具列表里）：");
            foreach (var s in _skills.Values)
                sb.Append("- ").Append(s.Id).Append("：").AppendLine(s.Summary);
            sb.Append("需要某技能时先调 open_skill(skill=\"<id>\") 开启，其工具随后可用；用完可 close_skill 释放上下文。");
            return sb.ToString();
        }

        /// <summary>导出当前可见工具：核心 +（有技能时）元工具 + 已开启技能的 action。</summary>
        public JArray ToToolsJson()
        {
            var arr = new JArray();
            lock (_lock)
            {
                foreach (var a in _core) arr.Add(ToolJson(a));
                if (_skills.Count > 0)
                {
                    foreach (var a in _meta) arr.Add(ToolJson(a));
                    foreach (var id in _open)
                        if (_skills.TryGetValue(id, out var s))
                            foreach (var a in s.Actions) arr.Add(ToolJson(a));
                }
            }
            return arr;
        }

        /// <summary>按名分发执行。未知名抛异常（由 AgentRunner 转成工具错误结果）。</summary>
        public JToken Execute(string name, JObject args)
        {
            if (!_dispatch.TryGetValue(name, out var action))
                throw new Exception($"unknown action '{name}'");
            return action.Execute(args ?? new JObject());
        }

        // —— 元工具调用的内部操作 ——

        /// <summary>开启技能；返回说明书，未知 id 返回 null。</summary>
        public string Open(string id)
        {
            lock (_lock)
            {
                if (id == null || !_skills.TryGetValue(id, out var s)) return null;
                _open.Add(s.Id);
                return s.Instructions ?? "";
            }
        }

        /// <summary>关闭技能；返回是否原本开着。</summary>
        public bool Close(string id)
        {
            if (id == null) return false;
            lock (_lock) return _open.Remove(id);
        }

        /// <summary>全部技能 id（供 open 失败时回报可选项）。</summary>
        public string[] SkillIds()
        {
            lock (_lock)
            {
                var ids = new string[_skills.Count];
                _skills.Keys.CopyTo(ids, 0);
                return ids;
            }
        }

        private static JObject ToolJson(IAgentAction a) => new JObject
        {
            ["name"] = a.Name,
            ["description"] = a.Description,
            ["input_schema"] = a.InputSchema ?? new JObject { ["type"] = "object" },
        };
    }
}
