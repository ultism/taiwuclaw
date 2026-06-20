using UnityEngine;

namespace TaiwuClaw.UI
{
    /// <summary>
    /// 统一的 IMGUI 皮肤——深色面板 + 金字标题 + 蓝调按钮，向游戏自带 UI 及社区 MOD
    /// （「实时人物编辑器」）的配色看齐，让多个浮窗看起来像一套。
    /// 延迟构建（首次 OnGUI 时），所有贴图 HideAndDontSave 避免泄漏。
    /// </summary>
    internal static class TaiwuStyles
    {
        public static GUIStyle Window;
        public static GUIStyle Header;
        public static GUIStyle InfoBox;
        public static GUIStyle Button;
        public static GUIStyle ButtonDanger;
        public static GUIStyle Transcript;
        public static GUIStyle Input;
        public static GUIStyle Hint;

        private static bool _init;

        public static void EnsureInit()
        {
            if (_init) return;
            _init = true;

            // 窗口：深色底，沿用默认 window 的边框/标题排版（仅换背景与标题字色）
            Window = new GUIStyle(GUI.skin.window);
            Window.normal.background = Tex(new Color(0.12f, 0.12f, 0.16f, 0.96f));
            Window.onNormal.background = Window.normal.background;
            Window.normal.textColor = new Color(0.9f, 0.8f, 0.5f);
            Window.onNormal.textColor = Window.normal.textColor;
            Window.fontStyle = FontStyle.Bold;

            Header = new GUIStyle(GUI.skin.label);
            Header.fontSize = 16;
            Header.fontStyle = FontStyle.Bold;
            Header.normal.textColor = new Color(0.9f, 0.8f, 0.5f);

            InfoBox = new GUIStyle(GUI.skin.box);
            InfoBox.normal.background = Tex(new Color(0.15f, 0.18f, 0.25f, 0.9f));
            InfoBox.normal.textColor = new Color(0.85f, 0.9f, 1f);
            InfoBox.fontSize = 12;
            InfoBox.alignment = TextAnchor.MiddleLeft;
            InfoBox.padding = new RectOffset(8, 8, 4, 4);

            Button = new GUIStyle(GUI.skin.button);
            Button.normal.background = Tex(new Color(0.25f, 0.35f, 0.55f));
            Button.hover.background = Tex(new Color(0.3f, 0.45f, 0.7f));
            Button.active.background = Tex(new Color(0.2f, 0.3f, 0.5f));
            Button.normal.textColor = Color.white;
            Button.hover.textColor = Color.white;
            Button.active.textColor = Color.white;
            Button.fontSize = 13;
            Button.fixedHeight = 26;
            Button.padding = new RectOffset(10, 10, 4, 4);

            ButtonDanger = new GUIStyle(Button);
            ButtonDanger.normal.background = Tex(new Color(0.6f, 0.2f, 0.2f));
            ButtonDanger.hover.background = Tex(new Color(0.75f, 0.25f, 0.25f));
            ButtonDanger.active.background = Tex(new Color(0.5f, 0.15f, 0.15f));

            // 对话区：更暗的底 + 浅字，自动换行；保留 textArea 的可选中复制能力
            Transcript = new GUIStyle(GUI.skin.textArea);
            Transcript.normal.background = Tex(new Color(0.08f, 0.08f, 0.11f, 0.95f));
            Transcript.focused.background = Transcript.normal.background;
            Transcript.normal.textColor = new Color(0.88f, 0.9f, 0.92f);
            Transcript.focused.textColor = Transcript.normal.textColor;
            Transcript.fontSize = 13;
            Transcript.wordWrap = true;
            Transcript.padding = new RectOffset(8, 8, 6, 6);

            Input = new GUIStyle(GUI.skin.textField);
            Input.normal.background = Tex(new Color(0.18f, 0.18f, 0.22f, 0.95f));
            Input.focused.background = Tex(new Color(0.2f, 0.22f, 0.28f, 0.95f));
            Input.normal.textColor = new Color(0.92f, 0.92f, 0.95f);
            Input.focused.textColor = Input.normal.textColor;
            Input.fontSize = 13;
            Input.fixedHeight = 26;
            Input.padding = new RectOffset(8, 6, 4, 4);

            Hint = new GUIStyle(GUI.skin.label);
            Hint.fontSize = 12;
            Hint.wordWrap = true;
            Hint.normal.textColor = new Color(0.7f, 0.7f, 0.78f);
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels(new[] { c });
            t.Apply();
            return t;
        }
    }
}
