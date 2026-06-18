using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Core
{
    /// <summary>
    /// 一个可被 agent 调用的能力。新增能力只需实现本接口并向 ActionRegistry 注册，
    /// 通信层（HttpHarnessServer）与调度层（MainThreadDispatcher）完全无需改动。
    /// </summary>
    public interface IAgentAction
    {
        /// <summary>调用名，agent 在请求 {"name":...} 中使用。建议用 "域.动作" 形式，如 encyclopedia.query。</summary>
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
