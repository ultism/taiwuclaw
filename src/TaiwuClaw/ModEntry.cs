using TaiwuModdingLib.Core.Plugin;
using TaiwuClaw.Actions;
using TaiwuClaw.Core;
using UnityEngine;

namespace TaiwuClaw
{
    /// <summary>
    /// MOD 入口。组装可扩展 harness：主线程泵 + action 注册表 + HTTP 通信层。
    /// 新增能力：实现 IAgentAction 并在下方 registry.Register(...) 即可，无需改动通信/调度层。
    /// </summary>
    [PluginConfig("TaiwuClaw", "ultism", "0.1.0")]
    public class ModEntry : TaiwuRemakePlugin
    {
        private const int Port = 8420;

        private HttpHarnessServer _server;
        private MainThreadDispatcher _dispatcher;

        public override void Initialize()
        {
            _dispatcher = MainThreadDispatcher.Ensure();

            var registry = new ActionRegistry();
            registry.Register(new EncyclopediaQueryAction());
            registry.Register(new ListActionsAction(registry)); // 自描述，便于 agent 发现能力

            _server = new HttpHarnessServer(registry, _dispatcher, Port);
            _server.Start();

            Debug.Log($"[TaiwuClaw] harness listening on http://127.0.0.1:{Port}/  " +
                      "(POST body: {\"name\":\"encyclopedia.query\",\"args\":{\"keyword\":\"...\"}})");
        }

        public override void Dispose()
        {
            _server?.Stop();
            _dispatcher?.Shutdown();
            Debug.Log("[TaiwuClaw] harness stopped.");
        }
    }
}
