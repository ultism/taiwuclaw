using System.Text;
using System.Threading;
using TaiwuClaw.Agent;
using UnityEngine;

namespace TaiwuClaw.UI
{
    /// <summary>
    /// 极简 IMGUI 浮动聊天面板（热键 F8 开关），先跑通框架。
    /// 主线程绘制；提交时在后台线程跑 AgentRunner.RunTurn。
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        private AgentRunner _runner;
        private string _configPath;
        private bool _ready;

        private bool _visible;
        private string _input = "";
        private Vector2 _scroll;
        private Rect _window = new Rect(40, 40, 560, 460);

        public static void Create(AgentRunner runner, string configPath, bool ready)
        {
            var go = new GameObject("TaiwuClawChatPanel");
            DontDestroyOnLoad(go);
            var panel = go.AddComponent<ChatPanel>();
            panel._runner = runner;
            panel._configPath = configPath;
            panel._ready = ready;
            Instance = go;
        }

        public static GameObject Instance { get; private set; }

        private bool _loggedAlive;

        // 切换键。通过 IMGUI 的 Event.current 检测，不依赖新/旧 Input System。
        private static readonly KeyCode ToggleKey = KeyCode.F8;

        private void OnGUI()
        {
            if (!_loggedAlive)
            {
                _loggedAlive = true;
                Debug.Log($"[TaiwuClaw] ChatPanel 已就绪，按 {ToggleKey} 开关；若无效，隐藏时按其它键看日志里上报的 keyCode。");
            }

            // 裸 F8 被游戏热键层吃掉，Shift+F8 可穿透到 IMGUI
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == ToggleKey)
            {
                _visible = !_visible;
                e.Use();
            }

            if (!_visible) return;
            _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "TaiwuClaw 助手  (Shift+F8 开关)");
        }

        private void DrawWindow(int id)
        {
            if (!_ready)
            {
                GUILayout.Label("未配置 LLM。请编辑以下文件填入 apiKey 后重启游戏：");
                GUILayout.TextField(_configPath);
                GUI.DragWindow();
                return;
            }

            string transcript = BuildText();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("复制全部", GUILayout.Width(80)))
                GUIUtility.systemCopyBuffer = transcript;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 只读但可选中复制（每帧用最新文本重建，编辑被丢弃）
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(330));
            GUILayout.TextArea(transcript, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUI.enabled = !_runner.Busy;
            _input = GUILayout.TextField(_input, GUILayout.ExpandWidth(true));
            bool submit = GUILayout.Button(_runner.Busy ? "…" : "发送", GUILayout.Width(64));
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            bool enterPressed = Event.current.type == EventType.KeyDown
                                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            if ((submit || enterPressed) && !_runner.Busy && !string.IsNullOrEmpty(_input.Trim()))
            {
                string text = _input.Trim();
                _input = "";
                var t = new Thread(() => _runner.RunTurn(text)) { IsBackground = true, Name = "TaiwuClawAgent" };
                t.Start();
            }

            GUI.DragWindow();
        }

        private string BuildText()
        {
            var sb = new StringBuilder();
            foreach (var line in _runner.Snapshot())
                sb.Append(Prefix(line.Role)).AppendLine(line.Text).AppendLine();
            return sb.ToString();
        }

        private static string Prefix(string role)
        {
            switch (role)
            {
                case "user": return "你> ";
                case "thinking": return "[思考] ";
                case "tool": return "[工具] ";
                case "error": return "[错误] ";
                default: return "";
            }
        }
    }
}
