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

## 仓库结构

```
src/TaiwuClaw/      MOD 源码（入口 ModEntry.cs）
Config.Lua          MOD 清单（部署时复制到游戏 Mod 目录）
.ref/               反编译参考（本地，.gitignore 忽略，不上传）
```

## MOD 入口约定

入口类须 `public` 且继承 `TaiwuModdingLib.Core.Plugin.TaiwuRemakePlugin`，并带
`[PluginConfig(name, creatorId, version)]` 特性；加载器取每个 DLL 的第一个匹配类型。
