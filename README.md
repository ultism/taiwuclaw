# TaiwuClaw

太吾绘卷（重制版）极简 MOD —— 把一个查询百晓册（游戏内置约 30 万字百科）的 harness 塞进游戏。

## 工作原理

百晓册数据由 `EncyclopediaContent.tsv`（正文）、`EncyclopediaReference.tsv`（引用表）和约 250 个子表 tsv 组成。
游戏启动时 `EncyclopediaContent.Init()` 已把它们解析进 `EncyclopediaContent.DataArray`（public static）。
本 MOD 复用这份已解析数据，自己做全文过滤——游戏内置搜索只搜标题、不搜正文，所以全文查询需自行遍历。

## 环境

- 游戏 Unity 2022.3.62f2（Mono 后端）
- 目标框架 `netstandard2.1`（与官方库 `TaiwuModdingLib` 一致）
- .NET SDK（9.x 已验证），用 `dotnet build`
- MOD 框架：`TaiwuModdingLib` + Harmony（已随游戏分发）

## 构建与部署

```bash
dotnet build src/TaiwuClaw/TaiwuClaw.csproj -c Release
```

构建后会自动部署到 `<游戏根>/Mod/TaiwuClaw/`（`Config.Lua` + `Plugins/TaiwuClaw.dll`）。
游戏路径默认指向本机 Steam 安装位置，其他机器用 `-p:TaiwuRoot=...` 覆盖：

```bash
dotnet build src/TaiwuClaw/TaiwuClaw.csproj -c Release -p:TaiwuRoot="D:\Games\The Scroll Of Taiwu"
```

## 游戏内 Agent

MOD 在游戏进程内运行一个 agent harness（不是外部进程，也不是单轮聊天）：

- **交错思考 + 工具循环**：模型在 thinking 中决定调用工具、看结果、继续思考，直到收尾。
- **能力 = 工具**：每个 `IAgentAction`（目前仅 `encyclopedia.query`）作为 tool 暴露给模型；新增能力只需实现接口并 `registry.Register(...)`，agent 循环与通信层无需改动。
- **LLM 通信**：`ILlmClient` 抽象，当前实现 `AnthropicMessagesClient`（手写 HTTP + Newtonsoft，原样回灌 thinking 块以延续交错思考）；可经兼容 Anthropic Messages 的第三方网关使用，OpenAI chat/responses 格式可后续按同接口扩展。
- **主线程安全**：HTTP 在后台线程；工具经 `MainThreadDispatcher` 回 Unity 主线程执行。
- **界面**：游戏内按 **F8** 开关 IMGUI 聊天面板。

### 配置 LLM

首次运行会在 **per-user 目录** `…/AppData/LocalLow/Conchship/The Scroll of Taiwu/TaiwuClaw/llm.json`（`Application.persistentDataPath`）生成模板（**不入库、含密钥**）。刻意放在 mod 内容目录之外——创意工坊上传的是整个 mod 目录，密钥绝不能在里面（旧版若在 `Mod/TaiwuClaw/` 留有 llm.json 会自动迁移过去）。填入 `apiKey`（及按需调整 `endpoint`/`model`/`authStyle`）后重启游戏：

```json
{
  "endpoint": "https://api.anthropic.com/v1/messages",
  "model": "claude-opus-4-8",
  "apiKey": "sk-...",
  "authStyle": "anthropic",
  "maxTokens": 16000,
  "thinking": true
}
```

`authStyle`：`anthropic`（`x-api-key` + `anthropic-version`）或 `bearer`（`Authorization: Bearer`）。老模型上若交错思考需 beta 头，填 `"betaHeader": "interleaved-thinking-2025-05-14"`。

## 仓库结构

```
src/TaiwuClaw/
  ModEntry.cs        入口：组装 registry + dispatcher + client + runner + 面板
  Core/              IAgentAction / ActionRegistry / MainThreadDispatcher
  Actions/           EncyclopediaQueryAction（百晓册检索）/ ListActionsAction
  Agent/             ILlmClient / AnthropicMessagesClient / AgentRunner / LlmConfig
  UI/                ChatPanel（IMGUI，F8）
Config.Lua           MOD 清单（部署时复制到游戏 Mod 目录）
.ref/                反编译参考（本地，.gitignore 忽略，不上传）
```

> `llm.json`（含密钥）生成在 `persistentDataPath`（不在 mod 目录、不在仓库），`.gitignore` 也忽略它。发布到创意工坊的包只含 `Config.Lua` + `Plugins/*.dll`，订阅者首次运行各自生成配置、各填各的 key。

## MOD 入口约定

入口类须 `public` 且继承 `TaiwuModdingLib.Core.Plugin.TaiwuRemakePlugin`，并带
`[PluginConfig(name, creatorId, version)]` 特性；加载器取每个 DLL 的第一个匹配类型。
