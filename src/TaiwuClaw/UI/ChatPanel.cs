using System.Text;
using System.Threading;
using TaiwuClaw.Actions;
using TaiwuClaw.Agent;
using UnityEngine;
using UnityEngine.UI;

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

        // UGUI 透明拦截层：可见时盖住窗口区域，挡掉透传到游戏 UI 的点击
        private GameObject _blocker;
        private RectTransform _blockerRect;

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

        // IMGUI 不跟随 UGUI 缩放，按屏幕高自动缩放（4K≈2×）
        private float CurrentScale => _uiScale > 0f ? _uiScale : Mathf.Clamp(Screen.height / 1080f, 1f, 4f);

        private void Update()
        {
            if (_blocker == null) EnsureBlocker();
            UpdateBlocker();
        }

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

            float scale = CurrentScale;
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "太吾百晓 · TaiwuClaw", TaiwuStyles.Window);
            GUI.matrix = prev;
        }

        // 建一个 ScreenSpaceOverlay Canvas + 透明 Image（raycastTarget）作输入拦截层。
        // IMGUI 与 UGUI 是两套独立输入管线：拦截层让游戏的 EventSystem 射线先打到它，
        // 游戏 UI 收不到点击；而我们自己的 IMGUI 控件走旧事件管线，照常响应。
        private void EnsureBlocker()
        {
            _blocker = new GameObject("TaiwuClawInputBlocker");
            DontDestroyOnLoad(_blocker);
            var canvas = _blocker.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // 盖在游戏所有 UI 之上
            _blocker.AddComponent<GraphicRaycaster>();

            var img = new GameObject("Area").AddComponent<Image>();
            img.transform.SetParent(_blocker.transform, false);
            img.color = new Color(0f, 0f, 0f, 0f); // 全透明，但 raycastTarget 仍拦截
            img.raycastTarget = true;
            _blockerRect = img.rectTransform;
            _blockerRect.anchorMin = _blockerRect.anchorMax = _blockerRect.pivot = new Vector2(0f, 1f); // 左上为原点，对齐 GUI 坐标系
            _blocker.SetActive(false);
        }

        private void UpdateBlocker()
        {
            if (_blocker == null) return;
            bool show = _visible;
            if (_blocker.activeSelf != show) _blocker.SetActive(show);
            if (!show) return;

            float scale = CurrentScale;
            // GUI 坐标 y 向下；左上锚点的 anchoredPosition y 向上，故取负
            _blockerRect.anchoredPosition = new Vector2(_window.x * scale, -_window.y * scale);
            _blockerRect.sizeDelta = new Vector2(_window.width * scale, _window.height * scale);
        }

        private void OnDestroy()
        {
            if (_blocker != null) Destroy(_blocker);
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
            var hits = EncyclopediaSearch.RecentHits; // 引用快照，主线程读
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
