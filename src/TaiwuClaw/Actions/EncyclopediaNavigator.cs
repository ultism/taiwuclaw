using System;
using System.Reflection;
using UnityEngine;

namespace TaiwuClaw.Actions
{
    /// <summary>
    /// 借用游戏原生「百晓生」窗口跳转到指定词条——不重写 UI，纯反射开窗
    /// （抄 RealTimeModifyChar 开化魂阁那套）。入口取自 ViewEncyclopediaPanel.OpenLink：
    /// <code>
    ///   UIElement.Encyclopedia.SetOnInitArgs(ArgumentBox.SetObject("key", key));
    ///   UIManager.Instance.ShowUI(UIElement.Encyclopedia, true);
    /// </code>
    /// key = EncyclopediaContentItem.Key —— BasicInfoView.JumpTo 先按 NodeKeyDict 精确命中，
    /// 命中则直达词条；万一对不上则退到游戏自带搜索（不会开空窗）。
    /// 须在 Unity 主线程、游戏内（已加载存档、百晓册可用）调用。
    /// </summary>
    internal static class EncyclopediaNavigator
    {
        /// <summary>打开原生百晓册并跳到该 key 词条；成功返回 true，失败返回 false 并给出原因。</summary>
        public static bool OpenEntry(string key, out string error)
        {
            error = null;
            try
            {
                Type uiElementType = FindLoadedType("UIElement");
                Type uiManagerType = FindLoadedType("UIManager");
                Type argBoxType = FindLoadedType("ArgumentBox");
                if (uiElementType == null || uiManagerType == null || argBoxType == null)
                {
                    error = "未找到 UIElement/UIManager/ArgumentBox（是否在游戏内？）";
                    return false;
                }

                object encyclopedia = GetStaticMember(uiElementType, "Encyclopedia");
                object uiManager = GetStaticMember(uiManagerType, "Instance");
                if (encyclopedia == null || uiManager == null)
                {
                    error = "UIElement.Encyclopedia 或 UIManager.Instance 为空（可能尚未进入游戏）";
                    return false;
                }

                // var box = new ArgumentBox(); box.SetObject("key", key);
                object box = Activator.CreateInstance(argBoxType);
                if (!string.IsNullOrEmpty(key))
                {
                    MethodInfo setObject = argBoxType.GetMethod("SetObject",
                        BindingFlags.Instance | BindingFlags.Public, null,
                        new[] { typeof(string), typeof(object) }, null);
                    if (setObject == null) { error = "ArgumentBox.SetObject(string,object) 未找到"; return false; }
                    setObject.Invoke(box, new object[] { "key", key });
                }

                // UIElement.Encyclopedia.SetOnInitArgs(box)
                MethodInfo setArgs = uiElementType.GetMethod("SetOnInitArgs",
                    BindingFlags.Instance | BindingFlags.Public, null, new[] { argBoxType }, null);
                if (setArgs == null) { error = "UIElement.SetOnInitArgs 未找到"; return false; }
                setArgs.Invoke(encyclopedia, new[] { box });

                // UIManager.Instance.ShowUI(UIElement.Encyclopedia, true)
                MethodInfo showUI = uiManagerType.GetMethod("ShowUI",
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { uiElementType, typeof(bool) }, null);
                if (showUI == null) { error = "UIManager.ShowUI(UIElement,bool) 未找到"; return false; }
                showUI.Invoke(uiManager, new object[] { encyclopedia, true });

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("[TaiwuClaw] 打开百晓册跳转失败：" + e);
                return false;
            }
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
