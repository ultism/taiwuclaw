using System.Text;
using System.Threading;
using TaiwuClaw.Actions;
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
        private float _uiScale; // 0 = 按屏幕高自动

        private bool _visible;
        private string _input = "";
        private Vector2 _scroll;
        private Rect _window = new Rect(40, 40, 560, 460);

        private string _status = "";
        private float _statusTime;

        public static void Create(AgentRunner runner, string configPath, bool ready, float uiScale)
        {
            var go = new GameObject("TaiwuClawChatPanel");
            DontDestroyOnLoad(go);
            var panel = go.AddComponent<ChatPanel>();
            panel._runner = runner;
            panel._configPath = configPath;
            panel._ready = ready;
            panel._uiScale = uiScale;
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

            TaiwuStyles.EnsureInit();

            // IMGUI 不跟随游戏 UGUI 缩放，自己按屏幕高缩放（4K≈2×）
            float scale = _uiScale > 0f ? _uiScale : Mathf.Clamp(Screen.height / 1080f, 1f, 4f);
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "太吾百晓 · TaiwuClaw", TaiwuStyles.Window);
            GUI.matrix = prev;
        }

        private void DrawWindow(int id)
        {
            // 标题行：金字标题 + 右上角关闭
            GUILayout.BeginHorizontal();
            GUILayout.Label("百晓册助手", TaiwuStyles.Header);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Shift+F8 开关", TaiwuStyles.Hint);
            if (GUILayout.Button("✕", TaiwuStyles.ButtonDanger, GUILayout.Width(30)))
                _visible = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            if (!_ready)
            {
                GUILayout.Label("未配置 LLM。请编辑以下文件填入 apiKey 后重启游戏：", TaiwuStyles.Hint);
                GUILayout.TextField(_configPath, TaiwuStyles.Input);
                GUILayout.Space(4);
                GUI.DragWindow();
                return;
            }

            string transcript = BuildText();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("复制全部", TaiwuStyles.Button, GUILayout.Width(80)))
                GUIUtility.systemCopyBuffer = transcript;
            GUI.enabled = !_runner.Busy;
            if (GUILayout.Button("清空历史", TaiwuStyles.ButtonDanger, GUILayout.Width(80)))
            {
                _runner.Clear();
                _scroll = Vector2.zero;
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (_runner.Busy)
                GUILayout.Label("思考中…", TaiwuStyles.Hint);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // 只读但可选中复制（每帧用最新文本重建，编辑被丢弃）
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
            GUILayout.TextArea(transcript, TaiwuStyles.Transcript, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.Space(4);

            DrawEntryLinks();

            // 操作状态（导航成功/失败），5 秒后淡出
            if (!string.IsNullOrEmpty(_status) && Time.time - _statusTime < 5f)
                GUILayout.Label(_status, TaiwuStyles.Hint);

            GUILayout.BeginHorizontal();
            GUI.enabled = !_runner.Busy;
            _input = GUILayout.TextField(_input, TaiwuStyles.Input, GUILayout.ExpandWidth(true));
            bool submit = GUILayout.Button(_runner.Busy ? "…" : "发送", TaiwuStyles.Button, GUILayout.Width(64));
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

        // 把最近一次检索命中的词条渲染成可点链接，点击在游戏内打开原生百晓册原文
        private void DrawEntryLinks()
        {
            var hits = EncyclopediaQueryAction.RecentHits; // 引用快照，主线程读
            if (hits == null || hits.Count == 0) return;

            GUILayout.Label("相关词条（点击在游戏内打开原文）", TaiwuStyles.Hint);
            const int perRow = 2;
            for (int i = 0; i < hits.Count; i++)
            {
                if (i % perRow == 0) GUILayout.BeginHorizontal();
                var h = hits[i];
                if (GUILayout.Button("📖 " + Leaf(h.Title), TaiwuStyles.Button, GUILayout.ExpandWidth(true)))
                {
                    if (EncyclopediaNavigator.OpenEntry(h.Key, Leaf(h.Title), this, SetStatus, out string err))
                        SetStatus("正在打开：" + Leaf(h.Title));
                    else
                        SetStatus("打开失败：" + err);
                }
                if (i % perRow == perRow - 1 || i == hits.Count - 1) GUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
        }

        // 标题路径取末段（"甲 / 乙 / 丙" → "丙"），按钮更紧凑
        private static string Leaf(string titlePath)
        {
            if (string.IsNullOrEmpty(titlePath)) return "(无标题)";
            int idx = titlePath.LastIndexOf(" / ", System.StringComparison.Ordinal);
            return idx >= 0 ? titlePath.Substring(idx + 3) : titlePath;
        }

        private void SetStatus(string msg)
        {
            _status = msg;
            _statusTime = Time.time;
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
