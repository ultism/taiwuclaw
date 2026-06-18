using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TaiwuClaw.Agent
{
    /// <summary>
    /// LLM 配置，从 &lt;游戏&gt;/Mod/TaiwuClaw/llm.json 读取（不入库，含密钥）。
    /// 首次运行若文件不存在则写出模板并返回 null。
    /// </summary>
    public class LlmConfig
    {
        public string Endpoint = "https://api.anthropic.com/v1/messages";
        public string Model = "claude-opus-4-8";
        public string ApiKey = "";
        /// <summary>鉴权风格："anthropic"(x-api-key + anthropic-version) 或 "bearer"(Authorization: Bearer)。</summary>
        public string AuthStyle = "anthropic";
        public string AnthropicVersion = "2023-06-01";
        /// <summary>可选 anthropic-beta 头，如老模型上交错思考需要 "interleaved-thinking-2025-05-14"。</summary>
        public string BetaHeader = "";
        public int MaxTokens = 16000;
        public bool Thinking = true;
        /// <summary>可选 effort：low|medium|high|xhigh|max，留空用服务端默认。</summary>
        public string Effort = "";
        /// <summary>面板 UI 缩放；0=按屏幕高自动(屏幕高/1080)，4K≈2×。手动可填 1.5/2/2.5…</summary>
        public float UiScale = 0f;
        public string SystemPrompt =
            "你是《太吾绘卷》的游戏内助手。你可以使用 encyclopedia_query 工具检索百晓册（游戏内置百科）" +
            "来回答关于游戏机制、数值、概率、配方、物品、功法等问题。优先检索后基于结果作答，不要凭空编造。";

        public bool IsReady => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Endpoint);

        /// <summary>
        /// 配置位置：per-user 可写目录 persistentDataPath/TaiwuClaw/llm.json。
        /// 刻意放在 mod 内容目录之外——创意工坊上传的是整个 mod 目录，含密钥的配置绝不能在里面。
        /// </summary>
        public static string ConfigPath =>
            Path.GetFullPath(Path.Combine(Application.persistentDataPath, "TaiwuClaw", "llm.json"));

        /// <summary>旧位置（mod 目录内），用于一次性迁移。</summary>
        private static string LegacyConfigPath =>
            Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "..", "..", "Mod", "TaiwuClaw", "llm.json"));

        /// <summary>读取配置；不存在则写模板并返回 null。</summary>
        public static LlmConfig LoadOrCreate()
        {
            string path = ConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // 从旧位置迁移：顺带把含密钥的文件移出 mod 目录，避免随创意工坊包上传
            if (!File.Exists(path) && File.Exists(LegacyConfigPath))
            {
                try
                {
                    File.Move(LegacyConfigPath, path);
                    Debug.Log($"[TaiwuClaw] 已将 llm.json 迁出 mod 目录 → {path}");
                }
                catch (System.Exception e)
                {
                    try { File.Copy(LegacyConfigPath, path, true); File.Delete(LegacyConfigPath); }
                    catch { Debug.LogWarning("[TaiwuClaw] 迁移 llm.json 失败：" + e.Message); }
                }
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, new LlmConfig().ToJson().ToString(Newtonsoft.Json.Formatting.Indented));
                Debug.LogWarning($"[TaiwuClaw] 已生成 LLM 配置模板：{path}，请填入 apiKey 后重启游戏。");
                return null;
            }

            try
            {
                var o = JObject.Parse(File.ReadAllText(path));
                var c = new LlmConfig();
                c.Endpoint = (string)o["endpoint"] ?? c.Endpoint;
                c.Model = (string)o["model"] ?? c.Model;
                c.ApiKey = (string)o["apiKey"] ?? "";
                c.AuthStyle = (string)o["authStyle"] ?? c.AuthStyle;
                c.AnthropicVersion = (string)o["anthropicVersion"] ?? c.AnthropicVersion;
                c.BetaHeader = (string)o["betaHeader"] ?? "";
                c.MaxTokens = o["maxTokens"]?.Value<int>() ?? c.MaxTokens;
                c.Thinking = o["thinking"]?.Value<bool>() ?? c.Thinking;
                c.Effort = (string)o["effort"] ?? "";
                c.UiScale = o["uiScale"]?.Value<float>() ?? 0f;
                c.SystemPrompt = (string)o["systemPrompt"] ?? c.SystemPrompt;
                return c;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TaiwuClaw] LLM 配置解析失败 ({path}): {e.Message}");
                return null;
            }
        }

        private JObject ToJson() => new JObject
        {
            ["endpoint"] = Endpoint,
            ["model"] = Model,
            ["apiKey"] = ApiKey,
            ["authStyle"] = AuthStyle,
            ["anthropicVersion"] = AnthropicVersion,
            ["betaHeader"] = BetaHeader,
            ["maxTokens"] = MaxTokens,
            ["thinking"] = Thinking,
            ["effort"] = Effort,
            ["uiScale"] = UiScale,
            ["systemPrompt"] = SystemPrompt,
        };
    }
}
