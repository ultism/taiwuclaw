# IMGUI 面板拦截点击（防透传到游戏 UI）

## 问题

IMGUI（`OnGUI`）和游戏 UGUI 是**两套独立输入管线**：

- IMGUI 走旧的即时事件队列（`Event.current`）。
- 游戏 UI 走 UGUI 的 `EventSystem` + `GraphicRaycaster`（新/旧 Input System 都一样）。

`Event.current.Use()` 只消费 IMGUI 自己的事件，**拦不住 UGUI**。结果点击 IMGUI 浮窗时，点击同时透传到下面的游戏 UI（误触按钮等）。

## 解法：透明 UGUI 拦截层

面板可见时，挂一个盖住窗口区域的**透明 `Image`（`raycastTarget=true`）**，放在一个超高 `sortingOrder` 的 `ScreenSpaceOverlay` Canvas 上：

- 游戏 `EventSystem` 射线先命中拦截层（最高层）→ 下面的游戏 UI 收不到点击。
- 我们自己的 IMGUI 控件走旧管线，不受影响，照常响应。

```csharp
var canvas = go.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.sortingOrder = short.MaxValue;     // 盖在游戏所有 UI 之上
go.AddComponent<GraphicRaycaster>();
var img = ...AddComponent<Image>();
img.color = new Color(0,0,0,0);           // 全透明，raycastTarget 仍拦截
img.raycastTarget = true;
```

每帧把拦截层 `RectTransform` 同步到窗口区域（左上锚点 (0,1)，对齐 GUI 坐标系；GUI 的 y 向下，故 `anchoredPosition.y = -window.y`），并乘上我们给 IMGUI 用的 `GUI.matrix` 缩放系数。窗口隐藏时 `SetActive(false)`。

## 实现位置

`UI/ChatPanel.cs`：`EnsureBlocker()` / `UpdateBlocker()`（`Update` 里驱动），`OnDestroy` 销毁。

## 要点 / 边界

- 只盖**窗口矩形**，窗口外的游戏 UI 点击照常——这是浮窗（非模态），不能全屏拦。
- 拖动窗口时拦截层有 1 帧延迟跟随，肉眼无感。
- **仅拦点击（UGUI 射线）**。键盘透传（IMGUI 输入框打字时游戏热键仍可能触发）是另一回事，目前未处理；如需要再单独做。
- 依赖游戏场景里已有的 `EventSystem`（游戏 UI 本来就有）。需引用 `UnityEngine.UIModule`（Canvas）+ `UnityEngine.UI`（Image/GraphicRaycaster）。
