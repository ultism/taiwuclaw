using TaiwuModdingLib.Core.Plugin;
using TaiwuClaw.Actions;
using TaiwuClaw.Agent;
using TaiwuClaw.Core;
using TaiwuClaw.UI;
using UnityEngine;

namespace TaiwuClaw
{
    /// <summary>
    /// MOD 入口。组装游戏内 agent harness：
    ///   action 注册表（= 给模型的 tools） + 主线程泵 + LLM client + 交错思考工具循环 + IMGUI 聊天面板。
    /// 新增能力：实现 IAgentAction 并在下方 registry.Register(...) 即可，agent 循环/通信层无需改动。
    /// </summary>
    [PluginConfig("TaiwuClaw", "ultism", "0.1.0")]
    public class ModEntry : TaiwuRemakePlugin
    {
        private MainThreadDispatcher _dispatcher;

        public override void Initialize()
        {
            _dispatcher = MainThreadDispatcher.Ensure();

            var registry = new ActionRegistry();
            registry.Register(new EncyclopediaQueryAction());
            registry.Register(new ListActionsAction(registry));

            var cfg = LlmConfig.LoadOrCreate(); // 缺失则写模板返回 null
            bool ready = cfg != null && cfg.IsReady;

            AgentRunner runner = null;
            if (ready)
            {
                var client = new AnthropicMessagesClient(cfg);
                runner = new AgentRunner(client, registry, _dispatcher, cfg.SystemPrompt);
            }

            ChatPanel.Create(runner, LlmConfig.ConfigPath, ready, cfg?.UiScale ?? 0f);

            Debug.Log(ready
                ? "[TaiwuClaw] agent harness 就绪，按 F8 打开聊天面板。"
                : $"[TaiwuClaw] 已加载，但未配置 LLM。请编辑 {LlmConfig.ConfigPath} 填入 apiKey 后重启。按 F8 查看。");
        }

        public override void Dispose()
        {
            if (ChatPanel.Instance != null)
                Object.Destroy(ChatPanel.Instance);
            _dispatcher?.Shutdown();
            Debug.Log("[TaiwuClaw] agent harness stopped.");
        }
    }
}
