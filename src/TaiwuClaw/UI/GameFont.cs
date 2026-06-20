using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace TaiwuClaw.UI
{
    /// <summary>
    /// 发现并复用游戏字体，供 IMGUI（GUIStyle.font 只认 UnityEngine.Font，
    /// 不能直接用 TMP_FontAsset）使用。策略：
    ///   ① 反射扫已加载的 TMP_FontAsset，取其 sourceFontFile（游戏文字真正的源字体）；
    ///   ② 退而扫所有已加载的 Font，挑「动态中文字体」（IMGUI 动态字体才能按需渲染 CJK）；
    ///   ③ 都没有则返回 null，沿用 IMGUI 默认字体。
    /// 全程反射、不硬依赖 TMPro 程序集（抄「实时人物编辑器」的 FindLoadedType 套路）。
    /// 首次解析会把所有候选字体名打到日志，方便按需锁定/覆盖。
    /// </summary>
    internal static class GameFont
    {
        private static bool _resolved;
        private static Font _font;

        /// <summary>可选：在 llm.json 里指定要用的字体名（精确或子串匹配），优先级最高。</summary>
        public static string PreferredName = "";

        public static Font Resolve()
        {
            if (_resolved) return _font;
            _resolved = true;
            try
            {
                _font = FindByPreferredName() ?? FindFromTmp() ?? FindGameLikeLoadedFont() ?? CreateSerifCjkFont();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TaiwuClaw] 字体发现异常：" + e);
            }
            Debug.Log(_font != null
                ? $"[TaiwuClaw] 已复用游戏字体：{_font.name}（dynamic={_font.dynamic}）"
                : "[TaiwuClaw] 未找到可复用字体，沿用 IMGUI 默认字体。");
            return _font;
        }

        // ── ① 指定名优先 ──────────────────────────────────────────────
        private static Font FindByPreferredName()
        {
            if (string.IsNullOrEmpty(PreferredName)) return null;
            foreach (Font f in AllLoadedFonts())
            {
                if (f != null && !string.IsNullOrEmpty(f.name) &&
                    f.name.IndexOf(PreferredName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return f;
            }
            Debug.LogWarning($"[TaiwuClaw] 未匹配到指定字体「{PreferredName}」，转自动发现。");
            return null;
        }

        // ── ② 从 TMP_FontAsset.sourceFontFile 取 ──────────────────────
        private static Font FindFromTmp()
        {
            Type tmpType = FindLoadedType("TMP_FontAsset");
            if (tmpType == null) return null;

            UnityEngine.Object[] assets = Resources.FindObjectsOfTypeAll(tmpType);
            PropertyInfo srcProp = tmpType.GetProperty("sourceFontFile",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var names = new StringBuilder();
            Font first = null;
            foreach (UnityEngine.Object asset in assets)
            {
                Font src = (srcProp != null) ? srcProp.GetValue(asset) as Font : null;
                names.Append("  - ").Append(asset.name)
                     .Append(" → sourceFontFile=").Append(src != null ? src.name : "null").Append('\n');
                if (first == null && src != null) first = src;
            }
            if (assets.Length > 0)
                Debug.Log($"[TaiwuClaw] 已加载 TMP_FontAsset {assets.Length} 个：\n{names}");
            return first;
        }

        // 系统通用字体黑名单：这些都不是游戏字体，宁可退到 ④ 系统衬线 CJK
        private static readonly string[] GenericFonts =
            { "Arial", "LegacyRuntime", "Consola", "Courier", "Verdana", "Tahoma", "Times", "Segoe", "Calibri" };

        // ── ③ 扫已加载 Font，挑「像游戏自带」的动态字体（排除系统通用字体）──
        private static Font FindGameLikeLoadedFont()
        {
            var all = AllLoadedFonts();
            var names = new StringBuilder();
            Font picked = null;
            foreach (Font f in all)
            {
                if (f == null) continue;
                names.Append("  - ").Append(f.name).Append(" (dynamic=").Append(f.dynamic).Append(")\n");
                bool generic = false;
                foreach (string g in GenericFonts)
                    if (f.name.IndexOf(g, StringComparison.OrdinalIgnoreCase) >= 0) { generic = true; break; }
                if (picked == null && f.dynamic && !generic) picked = f;
            }
            if (all.Count > 0)
                Debug.Log($"[TaiwuClaw] 已加载 Font {all.Count} 个：\n{names}");
            return picked; // 这台机器上通常全是系统字体 → 返回 null，落到 ④
        }

        // ── ④ 用系统衬线中文字体动态建一个，蹭游戏的宋体/思源衬线观感 ──
        private static Font CreateSerifCjkFont()
        {
            // 按优先级尝试；CreateDynamicFontFromOSFont 取系统里第一个存在的
            string[] prefer =
            {
                "Source Han Serif SC", "Source Han Serif CN", "Noto Serif CJK SC",
                "思源宋体", "STSong", "华文宋体", "SimSun", "宋体", "NSimSun",
                "Microsoft YaHei", "微软雅黑" // 最后退到黑体，至少是干净中文
            };
            try
            {
                Font f = Font.CreateDynamicFontFromOSFont(prefer, 16);
                if (f != null)
                    Debug.Log($"[TaiwuClaw] 已用系统字体构建：{f.name}（请求列表首选 {prefer[0]}）");
                return f;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TaiwuClaw] 构建系统衬线字体失败：" + e.Message);
                return null;
            }
        }

        private static List<Font> AllLoadedFonts()
        {
            var list = new List<Font>();
            foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
                list.Add(f);
            return list;
        }

        private static Type FindLoadedType(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type t in asm.GetTypes())
                        if (t.Name == name) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
