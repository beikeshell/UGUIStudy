using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;

namespace UnityEngine.EventSystems
{
    /// <summary>
    /// A BaseInputModule for pointer input.
    /// </summary>
    public abstract class PointerInputModule : BaseInputModule
    {
        /// <summary>
        /// Id of the cached left mouse pointer event.
        /// </summary>
        public const int kMouseLeftId = -1;

        /// <summary>
        /// Id of the cached right mouse pointer event.
        /// </summary>
        public const int kMouseRightId = -2;

        /// <summary>
        /// Id of the cached middle mouse pointer event.
        /// </summary>
        public const int kMouseMiddleId = -3;

        /// <summary>
        /// Touch id for when simulating touches on a non touch device.
        /// </summary>
        public const int kFakeTouchesId = -4;

        /// <summary>
        /// 用来产生并记录与【按键/点击】相对应的EventData字典
        /// 【键】是PointerId或鼠标按键的枚举值
        /// 【值】是PointerEventData
        /// </summary>
        protected Dictionary<int, PointerEventData> m_PointerData = new Dictionary<int, PointerEventData>();

        /// <summary>
        /// Search the cache for currently active pointers, return true if found.
        /// 根据id获取PointerEventData数据，并返回是否为新创建的数据
        /// </summary>
        /// <param name="id">Touch ID</param>
        /// <param name="data">Found data</param>
        /// <param name="create">If not found should it be created</param>
        /// <returns>True if pointer is found.</returns>
        protected bool GetPointerData(int id, out PointerEventData data, bool create)
        {
            if (!m_PointerData.TryGetValue(id, out data) && create)
            {
                data = new PointerEventData(eventSystem)
                {
                    pointerId = id,
                };
                m_PointerData.Add(id, data);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove the PointerEventData from the cache.
        /// </summary>
        protected void RemovePointerData(PointerEventData data)
        {
            m_PointerData.Remove(data.pointerId);
        }

        /// <summary>
        /// GetTouchPointerEventData获取对应的PointerEventData，并且同时会取到其是否是按下或释放的状态。
        /// Given a touch populate the PointerEventData and return if we are pressed or released.
        /// 获取（产生）PointerEventData，判断其状态，并且要判断它是否是新创建的created，
        /// input.phase表示当前触摸事件位于的阶段（Began，Moved，Stationary，Ended和Canceled ，也是由底层的api获取的）。
        /// 根据是否新建以及触摸的阶段来对其进行的一些初始化工作或值的更新。
        /// 最后是一些射线投射的内容，使用当前事件做射线投射，获取第一个投射结果并更新pointerData.pointerCurrentRaycast
        /// </summary>
        /// <param name="input">Touch being processed</param>
        /// <param name="pressed">Are we pressed this frame</param>
        /// <param name="released">Are we released this frame</param>
        /// <returns></returns>
        protected PointerEventData GetTouchPointerEventData(Touch input, out bool pressed, out bool released)
        {
            PointerEventData pointerData;
            var created = GetPointerData(input.fingerId, out pointerData, true);

            //使用标记设置为false，表示该pointerData未被使用过
            pointerData.Reset();

            pressed = created || (input.phase == TouchPhase.Began);
            released = (input.phase == TouchPhase.Canceled) || (input.phase == TouchPhase.Ended);

            if (created)
                pointerData.position = input.position;

            if (pressed)
                pointerData.delta = Vector2.zero;
            else
                pointerData.delta = input.position - pointerData.position;

            pointerData.position = input.position;

            pointerData.button = PointerEventData.InputButton.Left;

            if (input.phase == TouchPhase.Canceled)
            {
                pointerData.pointerCurrentRaycast = new RaycastResult();
            }
            else
            {
                //进行射线投射计算
                eventSystem.RaycastAll(pointerData, m_RaycastResultCache);

                //FindFirstRaycast -> 找到第一个有效的RaycastResult
                var raycast = FindFirstRaycast(m_RaycastResultCache);
                pointerData.pointerCurrentRaycast = raycast;
                m_RaycastResultCache.Clear();
            }
            return pointerData;
        }

        /// <summary>
        /// Copy one PointerEventData to another.
        /// </summary>
        protected void CopyFromTo(PointerEventData @from, PointerEventData @to)
        {
            @to.position = @from.position;
            @to.delta = @from.delta;
            @to.scrollDelta = @from.scrollDelta;
            @to.pointerCurrentRaycast = @from.pointerCurrentRaycast;
            @to.pointerEnter = @from.pointerEnter;
        }

        /// <summary>
        /// Given a mouse button return the current state for the frame.
        /// </summary>
        /// <param name="buttonId">Mouse button ID</param>
        protected PointerEventData.FramePressState StateForMouseButton(int buttonId)
        {
            var pressed = input.GetMouseButtonDown(buttonId);
            var released = input.GetMouseButtonUp(buttonId);
            if (pressed && released)
                return PointerEventData.FramePressState.PressedAndReleased;
            if (pressed)
                return PointerEventData.FramePressState.Pressed;
            if (released)
                return PointerEventData.FramePressState.Released;
            return PointerEventData.FramePressState.NotChanged;
        }

        /// <summary>
        /// Information about a mouse button event.
        /// 鼠标按键事件数据
        /// 对PointerEventData的封装
        /// 额外添加了一个表示当前帧鼠标状态【Pressed/Released/PressedAndReleased/NotChanged】的类型为PointerEventData.FramePressState变量buttonState
        /// </summary>
        public class MouseButtonEventData
        {
            /// <summary>
            /// The state of the button this frame.
            /// </summary>
            public PointerEventData.FramePressState buttonState;

            /// <summary>
            /// Pointer data associated with the mouse event.
            /// </summary>
            public PointerEventData buttonData;

            /// <summary>
            /// Was the button pressed this frame?
            /// </summary>
            public bool PressedThisFrame()
            {
                return buttonState == PointerEventData.FramePressState.Pressed || buttonState == PointerEventData.FramePressState.PressedAndReleased;
            }

            /// <summary>
            /// Was the button released this frame?
            /// </summary>
            public bool ReleasedThisFrame()
            {
                return buttonState == PointerEventData.FramePressState.Released || buttonState == PointerEventData.FramePressState.PressedAndReleased;
            }
        }

        /// <summary>
        /// 对MouseButtonEventData的进一步封装。
        /// 内部包含了一个鼠标按键的id InputButton 和一个MouseButtonEventData 。
        /// </summary>
        protected class ButtonState
        {
            private PointerEventData.InputButton m_Button = PointerEventData.InputButton.Left;

            public MouseButtonEventData eventData
            {
                get { return m_EventData; }
                set { m_EventData = value; }
            }

            public PointerEventData.InputButton button
            {
                get { return m_Button; }
                set { m_Button = value; }
            }

            private MouseButtonEventData m_EventData;
        }

        /// <summary>
        /// 一个BottomState list
        /// 一个管理鼠标按键状态的列表
        /// 每一项表示了某个鼠标按键的状态
        /// </summary>
        protected class MouseState
        {
            private List<ButtonState> m_TrackedButtons = new List<ButtonState>();

            public bool AnyPressesThisFrame()
            {
                for (int i = 0; i < m_TrackedButtons.Count; i++)
                {
                    if (m_TrackedButtons[i].eventData.PressedThisFrame())
                        return true;
                }
                return false;
            }

            public bool AnyReleasesThisFrame()
            {
                for (int i = 0; i < m_TrackedButtons.Count; i++)
                {
                    if (m_TrackedButtons[i].eventData.ReleasedThisFrame())
                        return true;
                }
                return false;
            }

            public ButtonState GetButtonState(PointerEventData.InputButton button)
            {
                ButtonState tracked = null;
                for (int i = 0; i < m_TrackedButtons.Count; i++)
                {
                    if (m_TrackedButtons[i].button == button)
                    {
                        tracked = m_TrackedButtons[i];
                        break;
                    }
                }

                if (tracked == null)
                {
                    tracked = new ButtonState { button = button, eventData = new MouseButtonEventData() };
                    m_TrackedButtons.Add(tracked);
                }
                return tracked;
            }

            /// <summary>
            /// 设置某个鼠标按键（左/中/右）状态
            /// </summary>
            /// <param name="button"></param>
            /// <param name="stateForMouseButton"></param>
            /// <param name="data"></param>
            public void SetButtonState(PointerEventData.InputButton button, PointerEventData.FramePressState stateForMouseButton, PointerEventData data)
            {
                var toModify = GetButtonState(button);
                toModify.eventData.buttonState = stateForMouseButton;
                toModify.eventData.buttonData = data;
            }
        }



        private readonly MouseState m_MouseState = new MouseState();

        /// <summary>
        /// Return the current MouseState. Using the default pointer.
        /// </summary>
        protected virtual MouseState GetMousePointerEventData()
        {
            return GetMousePointerEventData(0);
        }

        /// <summary>
        /// Return the current MouseState.
        /// </summary>
        protected virtual MouseState GetMousePointerEventData(int id)
        {
            // Populate the left button...
            PointerEventData leftData;
            var created = GetPointerData(kMouseLeftId, out leftData, true);

            leftData.Reset();

            if (created)
                leftData.position = input.mousePosition;

            Vector2 pos = input.mousePosition;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // We don't want to do ANY cursor-based interaction when the mouse is locked
                leftData.position = new Vector2(-1.0f, -1.0f);
                leftData.delta = Vector2.zero;
            }
            else
            {
                leftData.delta = pos - leftData.position;
                leftData.position = pos;
            }
            leftData.scrollDelta = input.mouseScrollDelta;
            leftData.button = PointerEventData.InputButton.Left;
            eventSystem.RaycastAll(leftData, m_RaycastResultCache);
            var raycast = FindFirstRaycast(m_RaycastResultCache);
            leftData.pointerCurrentRaycast = raycast;
            m_RaycastResultCache.Clear();

            // copy the apropriate data into right and middle slots
            PointerEventData rightData;
            GetPointerData(kMouseRightId, out rightData, true);
            CopyFromTo(leftData, rightData);
            rightData.button = PointerEventData.InputButton.Right;

            PointerEventData middleData;
            GetPointerData(kMouseMiddleId, out middleData, true);
            CopyFromTo(leftData, middleData);
            middleData.button = PointerEventData.InputButton.Middle;

            m_MouseState.SetButtonState(PointerEventData.InputButton.Left, StateForMouseButton(0), leftData);
            m_MouseState.SetButtonState(PointerEventData.InputButton.Right, StateForMouseButton(1), rightData);
            m_MouseState.SetButtonState(PointerEventData.InputButton.Middle, StateForMouseButton(2), middleData);

            return m_MouseState;
        }

        /// <summary>
        /// Return the last PointerEventData for the given touch / mouse id.
        /// </summary>
        protected PointerEventData GetLastPointerEventData(int id)
        {
            PointerEventData data;
            GetPointerData(id, out data, false);
            return data;
        }

        /// <summary>
        /// 是否开始拖拽
        /// </summary>
        /// <param name="pressPos"></param>
        /// <param name="currentPos"></param>
        /// <param name="threshold"></param>
        /// <param name="useDragThreshold"></param>
        /// <returns></returns>
        private static bool ShouldStartDrag(Vector2 pressPos, Vector2 currentPos, float threshold, bool useDragThreshold)
        {
            if (!useDragThreshold)
                return true;

            return (pressPos - currentPos).sqrMagnitude >= threshold * threshold;
        }

        /// <summary>
        /// Process movement for the current frame with the given pointer event.
        /// ProcessMove和ProcessDrag是触摸点击和鼠标点击【公用】的逻辑
        /// </summary>
        protected virtual void ProcessMove(PointerEventData pointerEvent)
        {
            var targetGO = (Cursor.lockState == CursorLockMode.Locked ? null : pointerEvent.pointerCurrentRaycast.gameObject);
            HandlePointerExitAndEnter(pointerEvent, targetGO);
        }

        /// <summary>
        /// Process the drag for the current frame with the given pointer event.
        /// ProcessMove和ProcessDrag是触摸点击和鼠标点击【公用】的逻辑
        /// </summary>
        protected virtual void ProcessDrag(PointerEventData pointerEvent)
        {
            if (!pointerEvent.IsPointerMoving() ||
                Cursor.lockState == CursorLockMode.Locked ||
                pointerEvent.pointerDrag == null)
                return;

            if (!pointerEvent.dragging
                && ShouldStartDrag(pointerEvent.pressPosition, pointerEvent.position, eventSystem.pixelDragThreshold, pointerEvent.useDragThreshold))
            {
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.beginDragHandler);
                pointerEvent.dragging = true;
            }

            // Drag notification
            if (pointerEvent.dragging)
            {
                // Before doing drag we should cancel any pointer down state
                // And clear selection!
                if (pointerEvent.pointerPress != pointerEvent.pointerDrag)
                {
                    ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);

                    pointerEvent.eligibleForClick = false;
                    pointerEvent.pointerPress = null;
                    pointerEvent.rawPointerPress = null;
                }
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.dragHandler);
            }
        }

        public override bool IsPointerOverGameObject(int pointerId)
        {
            var lastPointer = GetLastPointerEventData(pointerId);
            if (lastPointer != null)
                return lastPointer.pointerEnter != null;
            return false;
        }

        /// <summary>
        /// Clear all pointers and deselect any selected objects in the EventSystem.
        /// </summary>
        protected void ClearSelection()
        {
            var baseEventData = GetBaseEventData();

            foreach (var pointer in m_PointerData.Values)
            {
                // clear all selection
                HandlePointerExitAndEnter(pointer, null);
            }

            m_PointerData.Clear();
            eventSystem.SetSelectedGameObject(null, baseEventData);
        }

        public override string ToString()
        {
            var sb = new StringBuilder("<b>Pointer Input Module of type: </b>" + GetType());
            sb.AppendLine();
            foreach (var pointer in m_PointerData)
            {
                if (pointer.Value == null)
                    continue;
                sb.AppendLine("<B>Pointer:</b> " + pointer.Key);
                sb.AppendLine(pointer.Value.ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Deselect the current selected GameObject if the currently pointed-at GameObject is different.
        /// </summary>
        /// <param name="currentOverGo">The GameObject the pointer is currently over.</param>
        /// <param name="pointerEvent">Current event data.</param>
        protected void DeselectIfSelectionChanged(GameObject currentOverGo, BaseEventData pointerEvent)
        {
            // Selection tracking
            var selectHandlerGO = ExecuteEvents.GetEventHandler<ISelectHandler>(currentOverGo);
            // if we have clicked something new, deselect the old thing
            // leave 'selection handling' up to the press event though.
            if (selectHandlerGO != eventSystem.currentSelectedGameObject)
                eventSystem.SetSelectedGameObject(null, pointerEvent);
        }
    }
}
