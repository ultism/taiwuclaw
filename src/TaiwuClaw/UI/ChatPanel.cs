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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "TaiwuClaw 助手  (F8 开关)");
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

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(360));
            foreach (var line in _runner.Snapshot())
            {
                GUILayout.Label(Prefix(line.Role) + line.Text);
                GUILayout.Space(2);
            }
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
