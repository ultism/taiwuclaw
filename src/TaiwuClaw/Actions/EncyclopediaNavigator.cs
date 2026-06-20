using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TaiwuClaw.Actions
{
    /// <summary>
    /// 借用游戏原生「百晓生」窗口跳转到指定词条——不重写 UI，纯反射（抄 RealTimeModifyChar 套路）。
    ///
    /// 关键：开窗参数（ViewEncyclopediaPanel.OnInit 的 "key"/"link"）只在 UI **首次创建**时生效，
    /// 百晓册一旦初始化过就不再触发 → 只开窗不跳转。所以这里改为开窗后**直接驱动**
    /// 单例视图：<c>BasicInfoView.Instance.JumpTo(key)</c>（JumpTo 内部按 NodeKeyDict 精确命中，
    /// key = EncyclopediaContentItem.Key；对不上则退游戏自带搜索，不开空窗）。
    ///
    /// 须在 Unity 主线程、游戏内（已加载存档）调用。开窗后视图实例要 1~N 帧才出现，故用协程等待。
    /// </summary>
    internal static class EncyclopediaNavigator
    {
        /// <summary>
        /// 打开原生百晓册并跳到该 key 词条。返回值仅表示「开窗调用」是否成功；
        /// 实际定位在协程里完成，结果经 setStatus 回报。
        /// </summary>
        public static bool OpenEntry(string key, string label, MonoBehaviour runner, Action<string> setStatus, out string error)
        {
            error = null;
            try
            {
                Type uiElementType = FindLoadedType("UIElement");
                Type uiManagerType = FindLoadedType("UIManager");
                if (uiElementType == null || uiManagerType == null)
                {
                    error = "未找到 UIElement/UIManager（是否在游戏内？）";
                    return false;
                }

                object encyclopedia = GetStaticMember(uiElementType, "Encyclopedia");
                object uiManager = GetStaticMember(uiManagerType, "Instance");
                if (encyclopedia == null || uiManager == null)
                {
                    error = "UIElement.Encyclopedia 或 UIManager.Instance 为空（可能尚未进入游戏）";
                    return false;
                }

                // UIManager.Instance.ShowUI(UIElement.Encyclopedia, true)
                MethodInfo showUI = uiManagerType.GetMethod("ShowUI",
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { uiElementType, typeof(bool) }, null);
                if (showUI == null) { error = "UIManager.ShowUI(UIElement,bool) 未找到"; return false; }
                showUI.Invoke(uiManager, new object[] { encyclopedia, true });

                // 开窗后等视图实例就绪，直接 JumpTo（绕开只跑一次的 OnInit）
                if (runner != null && runner.isActiveAndEnabled)
                    runner.StartCoroutine(JumpWhenReady(key, label, setStatus));
                else
                    setStatus?.Invoke("已打开百晓册（无法定位：协程宿主不可用）");

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("[TaiwuClaw] 打开百晓册失败：" + e);
                return false;
            }
        }

        private static IEnumerator JumpWhenReady(string key, string label, Action<string> setStatus)
        {
            Type bivType = FindLoadedType("BasicInfoView");
            MethodInfo jumpTo = (bivType != null)
                ? bivType.GetMethod("JumpTo", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null)
                : null;
            if (jumpTo == null) { setStatus?.Invoke("未找到 BasicInfoView.JumpTo(string)"); yield break; }

            // 视图实例开窗后要 1~N 帧才出现，最多等约 2 秒
            object inst = null;
            for (int i = 0; i < 120; i++)
            {
                inst = GetStaticMember(bivType, "Instance");
                if (inst != null) break;
                yield return null;
            }
            if (inst == null) { setStatus?.Invoke("已打开百晓册，但未取到视图实例"); yield break; }

            yield return null; // 再等一帧确保数据/布局就绪
            object latest = GetStaticMember(bivType, "Instance") ?? inst; // 等待期间可能重建，取最新

            string err = TryJump(jumpTo, latest, key);
            setStatus?.Invoke(err == null ? ("已在游戏内定位：" + label) : ("定位失败：" + err));
        }

        // 单独抽出，避免在含 yield 的迭代器里写 try/catch
        private static string TryJump(MethodInfo jumpTo, object instance, string key)
        {
            try { jumpTo.Invoke(instance, new object[] { key }); return null; }
            catch (Exception e) { Debug.LogError("[TaiwuClaw] JumpTo 失败：" + e); return e.Message; }
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

        private static object GetStaticMember(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(null);
            PropertyInfo prop = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return prop != null ? prop.GetValue(null, null) : null;
        }
    }
}
