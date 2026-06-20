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

            // 技能注册表：核心能力常驻；扩展能力打包成技能、默认关闭、用时 open_skill 才进上下文。
            var registry = new SkillRegistry();
            registry.RegisterCore(new EncyclopediaQueryAction());

            // 扩展能力在此 registry.RegisterSkill(new XxxSkill()); —— 一旦注册，open_skill/close_skill
            // 与技能目录会自动出现；零技能时不产生任何额外上下文开销。
            // TODO(磁盘技能): 扫描 persistentDataPath/TaiwuClaw/Skills/*/SKILL.md 解析为 markdown playbook
            //   技能（ISkill，Actions 为空）并在此 RegisterSkill；接口已就位，仅差文件加载器。
            // TODO(配置系统): 技能增多后改造 LlmConfig，支持按技能开关/各自参数。

            var cfg = LlmConfig.LoadOrCreate(); // 缺失则写模板返回 null
            bool ready = cfg != null && cfg.IsReady;

            // 复用游戏字体（IMGUI）：可在 llm.json 用 fontName 锁定，留空则自动发现
            GameFont.PreferredName = cfg?.FontName ?? "";

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
