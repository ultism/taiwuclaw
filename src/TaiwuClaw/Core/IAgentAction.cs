using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Core
{
    /// <summary>
    /// 一个可被 agent 调用的能力。新增能力只需实现本接口，再作为核心常驻（SkillRegistry.RegisterCore）
    /// 或打包进一个技能（见 <see cref="ISkill"/> / <see cref="CodeSkill"/>）注册即可，agent 循环无需改动。
    /// </summary>
    public interface IAgentAction
    {
        /// <summary>调用名，agent 在请求 {"name":...} 中使用。限 [a-zA-Z0-9_-]，<b>不能带点号</b>；
        /// 用下划线分域，如 encyclopedia_query。</summary>
        string Name { get; }

        /// <summary>给 agent 看的自描述（参数、语义）。会被内置 list action 暴露。</summary>
        string Description { get; }

        /// <summary>
        /// 工具入参的 JSON Schema（Anthropic tools 的 input_schema）。
        /// 形如 {"type":"object","properties":{...},"required":[...]}，模型据此构造调用。
        /// </summary>
        JObject InputSchema { get; }

        /// <summary>
        /// 执行能力。默认在 Unity 主线程被调用（见 MainThreadDispatcher），
        /// 因此实现里可安全访问游戏状态。返回值会作为 JSON 响应的 result 字段。
        /// </summary>
        JToken Execute(JObject args);
    }
}
