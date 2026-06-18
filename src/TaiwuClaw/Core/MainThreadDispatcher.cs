using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace TaiwuClaw.Core
{
    /// <summary>
    /// Unity 主线程泵。HTTP 请求在后台线程到达，但游戏 API 多数只能在主线程访问，
    /// 因此 action 统一通过本类回到主线程执行。纯数据查询（如百晓册）其实不需要，
    /// 但未来「操纵游戏状态」的 action 必须如此，故地基先打好。
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public static MainThreadDispatcher Instance { get; private set; }

        /// <summary>在主线程调用（如 MOD Initialize）以创建常驻泵对象。</summary>
        public static MainThreadDispatcher Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("TaiwuClawDispatcher");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<MainThreadDispatcher>();
            }
            return Instance;
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogWarning("[TaiwuClaw] main-thread task error: " + e); }
            }
        }

        /// <summary>从后台线程把工作派发到主线程，同步等待并取回结果（或抛出其异常）。</summary>
        public T Run<T>(Func<T> func)
        {
            // 已在主线程时直接执行，避免自我等待死锁。
            if (Instance != null && ReferenceEquals(this, Instance) && IsMainThread())
                return func();

            var done = new ManualResetEventSlim(false);
            T result = default;
            Exception error = null;
            _queue.Enqueue(() =>
            {
                try { result = func(); }
                catch (Exception e) { error = e; }
                finally { done.Set(); }
            });
            if (!done.Wait(TimeSpan.FromSeconds(30)))
                throw new Exception("main-thread dispatch timed out");
            if (error != null) throw error;
            return result;
        }

        private static int _mainThreadId = -1;
        private void Awake() { _mainThreadId = Thread.CurrentThread.ManagedThreadId; }
        private static bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public void Shutdown()
        {
            Destroy(gameObject);
            Instance = null;
        }
    }
}
