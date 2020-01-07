using System;
using System.Text;
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    /// <summary>
    /// Each touch event creates one of these containing all the relevant information.
    /// 每个触摸事件都会创建其中一个包含所有相关信息的PointerEventData。
    /// </summary>
    public class PointerEventData : BaseEventData
    {
        /// <summary>
        /// Input press tracking.
        /// 表示鼠标哪个按键被按下了
        /// </summary>
        public enum InputButton
        {
            /// <summary>
            /// Left button
            /// </summary>
            Left = 0,

            /// <summary>
            /// Right button.
            /// </summary>
            Right = 1,

            /// <summary>
            /// Middle button
            /// </summary>
            Middle = 2
        }

        /// <summary>
        /// The state of a press for the given frame.
        /// 当前帧Button Press状态
        /// </summary>
        public enum FramePressState
        {
            /// <summary>
            /// Button was pressed this frame.
            /// </summary>
            Pressed,

            /// <summary>
            /// Button was released this frame.
            /// </summary>
            Released,

            /// <summary>
            /// Button was pressed and released this frame.
            /// </summary>
            PressedAndReleased,

            /// <summary>
            /// Same as last frame.
            /// </summary>
            NotChanged
        }

        /// <summary>
        /// The object that received 'OnPointerEnter'.
        /// 接收OnPointerEnter事件的游戏对象
        /// </summary>
        public GameObject pointerEnter { get; set; }

        // The object that received 'OnPointerDown'.
        // 接收OnPointerDown事件的游戏对象
        private GameObject m_PointerPress;

        /// <summary>
        /// The raw GameObject for the last press event.
        /// This means that it is the 'pressed' GameObject even if it can not receive the press event itself.
        /// 在更新m_PointerPress时，会将当前m_PointerPress赋值给lastPress
        /// </summary>
        public GameObject lastPress { get; private set; }

        /// <summary>
        /// The object that the press happened on even if it can not handle the press event.
        /// </summary>
        public GameObject rawPointerPress { get; set; }

        /// <summary>
        /// The object that is receiving 'OnDrag'.
        /// 接收OnDrag事件的游戏对象
        /// </summary>
        public GameObject pointerDrag { get; set; }

        /// <summary>
        /// RaycastResult associated with the current event.
        /// 当前事件的射线检测结果
        /// </summary>
        public RaycastResult pointerCurrentRaycast { get; set; }

        /// <summary>
        /// RaycastResult associated with the pointer press.
        /// </summary>
        public RaycastResult pointerPressRaycast { get; set; }

        public List<GameObject> hovered = new List<GameObject>();

        /// <summary>
        /// Is it possible to click this frame
        /// 是不是符合条件的点击
        /// </summary>
        public bool eligibleForClick { get; set; }

        /// <summary>
        /// Id of the pointer (touch id).
        /// </summary>
        public int pointerId { get; set; }

        /// <summary>
        /// Current pointer position.
        /// </summary>
        public Vector2 position { get; set; }

        /// <summary>
        /// Pointer delta since last update.
        /// </summary>
        public Vector2 delta { get; set; }

        /// <summary>
        /// Position of the press.
        /// </summary>
        public Vector2 pressPosition { get; set; }

        /// <summary>
        /// World-space position where a ray cast into the screen hits something
        /// 射线投射到屏幕上的世界空间位置
        /// </summary>

        [Obsolete("Use either pointerCurrentRaycast.worldPosition or pointerPressRaycast.worldPosition")]
        public Vector3 worldPosition { get; set; }

        /// <summary>
        /// World-space normal where a ray cast into the screen hits something
        /// 射线投射到屏幕上的世界空间法向量
        /// </summary>
        [Obsolete("Use either pointerCurrentRaycast.worldNormal or pointerPressRaycast.worldNormal")]
        public Vector3 worldNormal { get; set; }

        /// <summary>
        /// The last time a click event was sent. Used for double click
        /// </summary>
        public float clickTime { get; set; }

        /// <summary>
        /// Number of clicks in a row.
        /// 连续点击次数
        /// </summary>
        /// <example>
        /// <code>
        /// using UnityEngine;
        /// using System.Collections;
        /// using UnityEngine.UI;
        /// using UnityEngine.EventSystems;// Required when using Event data.
        ///
        /// public class ExampleClass : MonoBehaviour, IPointerDownHandler
        /// {
        ///     public void OnPointerDown(PointerEventData eventData)
        ///     {
        ///         //Grab the number of consecutive clicks and assign it to an integer varible.
        ///         int i = eventData.clickCount;
        ///         //Display the click count.
        ///         Debug.Log(i);
        ///     }
        /// }
        /// </code>
        /// </example>
        public int clickCount { get; set; }

        /// <summary>
        /// The amount of scroll since the last update.
        /// 上次更新后的滑动增量
        /// </summary>
        public Vector2 scrollDelta { get; set; }

        /// <summary>
        /// Should a drag threshold be used?
        /// </summary>
        /// <remarks>
        /// If you do not want a drag threshold set this to false in IInitializePotentialDragHandler.OnInitializePotentialDrag.
        /// </remarks>
        public bool useDragThreshold { get; set; }

        /// <summary>
        /// Is a drag operation currently occuring.
        /// 是否正在拖拽
        /// </summary>
        public bool dragging { get; set; }

        /// <summary>
        /// The EventSystems.PointerEventData.InputButton for this event.
        /// 用于鼠标，表示鼠标哪个按键被按下了
        /// </summary>
        public InputButton button { get; set; }

        public PointerEventData(EventSystem eventSystem) : base(eventSystem)
        {
            eligibleForClick = false;

            pointerId = -1;
            position = Vector2.zero; // Current position of the mouse or touch event
            delta = Vector2.zero; // Delta since last update
            pressPosition = Vector2.zero; // Delta since the event started being tracked
            clickTime = 0.0f; // The last time a click event was sent out (used for double-clicks)
            clickCount = 0; // Number of clicks in a row. 2 for a double-click for example.

            scrollDelta = Vector2.zero;
            useDragThreshold = true;
            dragging = false;
            button = InputButton.Left;
        }

        /// <summary>
        /// Is the pointer moving.
        /// </summary>
        public bool IsPointerMoving()
        {
            return delta.sqrMagnitude > 0.0f;
        }

        /// <summary>
        /// Is scroll being used on the input device.
        /// </summary>
        public bool IsScrolling()
        {
            return scrollDelta.sqrMagnitude > 0.0f;
        }

        /// <summary>
        /// The camera associated with the last OnPointerEnter event.
        /// </summary>
        public Camera enterEventCamera
        {
            get { return pointerCurrentRaycast.module == null ? null : pointerCurrentRaycast.module.eventCamera; }
        }

        /// <summary>
        /// The camera associated with the last OnPointerPress event.
        /// </summary>
        public Camera pressEventCamera
        {
            get { return pointerPressRaycast.module == null ? null : pointerPressRaycast.module.eventCamera; }
        }

        /// <summary>
        /// The GameObject that received the OnPointerDown.
        /// </summary>
        public GameObject pointerPress
        {
            get { return m_PointerPress; }
            set
            {
                if (m_PointerPress == value)
                    return;

                lastPress = m_PointerPress;
                m_PointerPress = value;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>Position</b>: " + position);
            sb.AppendLine("<b>delta</b>: " + delta);
            sb.AppendLine("<b>eligibleForClick</b>: " + eligibleForClick);
            sb.AppendLine("<b>pointerEnter</b>: " + pointerEnter);
            sb.AppendLine("<b>pointerPress</b>: " + pointerPress);
            sb.AppendLine("<b>lastPointerPress</b>: " + lastPress);
            sb.AppendLine("<b>pointerDrag</b>: " + pointerDrag);
            sb.AppendLine("<b>Use Drag Threshold</b>: " + useDragThreshold);
            sb.AppendLine("<b>Current Raycast:</b>");
            sb.AppendLine(pointerCurrentRaycast.ToString());
            sb.AppendLine("<b>Press Raycast:</b>");
            sb.AppendLine(pointerPressRaycast.ToString());
            return sb.ToString();
        }
    }
}
