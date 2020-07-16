using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEngine.EventSystems
{
    [AddComponentMenu("Event/Event System")]
    /// <summary>
    /// Handles input, raycasting, and sending events.
    /// </summary>
    /// <remarks>
    /// The EventSystem is responsible for processing and handling events in a Unity scene.
    /// NOTE: A scene should only contain one EventSystem.
    /// EventSystem与许多模块结合使用，并且大多数情况下仅保留状态并将功能委托给特定的可覆盖的组件。
    /// The EventSystem works in conjunction with a number of modules and mostly just holds state and delegates functionality to specific,
    /// overrideable components.
    ///
    /// When the EventSystem is started it searches for any BaseInputModules attached to the SAME GameObject and adds them to an internal list.
    /// On update each attached module receives an UpdateModule call, where the module can modify internal state.
    ///
    /// After each module has been Updated, the active module has the Process call executed.
    /// This is where custom module processing can take place.
    ///
    /// Unity的UI系统中，EventSystem负责管理调度事件，控制各输入模块、射线投射以及事件动作的执行。
    /// UI的事件系统处理用户的交互动作，通过BaseInput来获取用户输入的信息和状态，通过InputModule处理输入，产生和发送事件，
    /// 通过RayCaster判断和选择需要响应交互事件的对象，最终由ExecuteEvents执行响应的动作，调用EventSystemHandler，以完成交互。
    /// </remarks>
    ///
    /// 单例
    public class EventSystem : UIBehaviour
    {
        // 维护了一个列表，处于激活状态的输入模块。BaseInputModule是输入模块的基类，衍生类有TouchInputModule和StandaloneInputModule。
        private List<BaseInputModule> m_SystemInputModules = new List<BaseInputModule>();

        //当前正在响应的输入模块，私有方法ChangeEventModule会更新和改变此成员
        private BaseInputModule m_CurrentInputModule;

        private  static List<EventSystem> m_EventSystems = new List<EventSystem>();

        /// <summary>
        /// Return the current EventSystem.
        /// EventSystem的全局单例
        /// </summary>
        public static EventSystem current
        {
            get { return m_EventSystems.Count > 0 ? m_EventSystems[0] : null; }
            set
            {
                int index = m_EventSystems.IndexOf(value);

                if (index >= 0)
                {
                    m_EventSystems.RemoveAt(index);
                    m_EventSystems.Insert(0, value);
                }
            }
        }

        /// <summary>
        /// 首个选中的对象，在StandaloneInputModule中会用到
        /// </summary>
        [SerializeField]
        [FormerlySerializedAs("m_Selected")]
        private GameObject m_FirstSelected;

        [SerializeField]
        private bool m_sendNavigationEvents = true;

        /// <summary>
        /// Should the EventSystem allow navigation events (move / submit / cancel).
        /// </summary>
        public bool sendNavigationEvents
        {
            get { return m_sendNavigationEvents; }
            set { m_sendNavigationEvents = value; }
        }

        [SerializeField]
        private int m_DragThreshold = 10;

        /// <summary>
        /// The soft area for dragging in pixels.
        /// </summary>
        public int pixelDragThreshold
        {
            get { return m_DragThreshold; }
            set { m_DragThreshold = value; }
        }

        // 当前选中的对象，可由各个输入模块调用EventSystem的SetSelectedGameObject方法来更新和改变此成员。
        // 在执行事件的动作时以此成员为对象，即ExecuteEvents.Execute(m_CurrentSelected, ...)
        private GameObject m_CurrentSelected;

        /// <summary>
        /// The currently active EventSystems.BaseInputModule.
        /// </summary>
        public BaseInputModule currentInputModule
        {
            get { return m_CurrentInputModule; }
        }

        /// <summary>
        /// Only one object can be selected at a time. Think: controller-selected button.
        /// </summary>
        public GameObject firstSelectedGameObject
        {
            get { return m_FirstSelected; }
            set { m_FirstSelected = value; }
        }

        /// <summary>
        /// The GameObject currently considered active by the EventSystem.
        /// </summary>
        public GameObject currentSelectedGameObject
        {
            get { return m_CurrentSelected; }
        }

        [Obsolete("lastSelectedGameObject is no longer supported")]
        public GameObject lastSelectedGameObject
        {
            get { return null; }
        }

        private bool m_HasFocus = true;

        /// <summary>
        /// Flag to say whether the EventSystem thinks it should be paused or not based upon focused state.
        /// </summary>
        /// <remarks>
        /// Used to determine inside the individual InputModules if the module should be ticked while the application doesnt have focus.
        /// </remarks>
        public bool isFocused
        {
            get { return m_HasFocus; }
        }

        protected EventSystem()
        {}

        /// <summary>
        /// Recalculate the internal list of BaseInputModules.
        /// 当输入模块的激活状态改变时（OnEnable或OnDisable）会调用此函数来更新EventSystem中管理的输入模块的列表。
        /// </summary>
        public void UpdateModules()
        {
            GetComponents(m_SystemInputModules);
            for (int i = m_SystemInputModules.Count - 1; i >= 0; i--)
            {
                if (m_SystemInputModules[i] && m_SystemInputModules[i].IsActive())
                    continue;

                m_SystemInputModules.RemoveAt(i);
            }
        }

        // 用于标记选择对象时标记。
        // 用在下面的SetSelectedGameObject方法和Selectable类的Select方法中
        private bool m_SelectionGuard;

        /// <summary>
        /// Returns true if the EventSystem is already in a SetSelected GameObject.
        /// </summary>
        public bool alreadySelecting
        {
            get { return m_SelectionGuard; }
        }

        /// <summary>
        /// Set the object as selected. Will send an OnDeselect the the old selected object and OnSelect to the new selected object.
        /// 设置当前选中的对象。还有一个重载方法void SetSelectedGameObject(GameObject selected)。
        /// 此方法会被一些衍生自Selectable的类直接调用，指定当前响应事件的对象。
        /// </summary>
        /// <param name="selected">GameObject to select.</param>
        /// <param name="pointer">Associated EventData.</param>
        public void SetSelectedGameObject(GameObject selected, BaseEventData pointer)
        {
            if (m_SelectionGuard)
            {
                Debug.LogError("Attempting to select " + selected +  "while already selecting an object.");
                return;
            }

            m_SelectionGuard = true;
            if (selected == m_CurrentSelected)
            {
                m_SelectionGuard = false;
                return;
            }

            // Debug.Log("Selection: new (" + selected + ") old (" + m_CurrentSelected + ")");
            ExecuteEvents.Execute(m_CurrentSelected, pointer, ExecuteEvents.deselectHandler);
            m_CurrentSelected = selected;
            ExecuteEvents.Execute(m_CurrentSelected, pointer, ExecuteEvents.selectHandler);
            m_SelectionGuard = false;
        }

        // 一份伪造的BaseEventData假数据
        private BaseEventData m_DummyData;
        private BaseEventData baseEventDataCache
        {
            get
            {
                if (m_DummyData == null)
                    m_DummyData = new BaseEventData(this);

                return m_DummyData;
            }
        }

        /// <summary>
        /// Set the object as selected. Will send an OnDeselect the the old selected object and OnSelect to the new selected object.
        /// </summary>
        /// <param name="selected">GameObject to select.</param>
        public void SetSelectedGameObject(GameObject selected)
        {
            SetSelectedGameObject(selected, baseEventDataCache);
        }

        /// <summary>
        /// 优先级依次是：
        /// 如果是不同的Raycaster：
        ///     module.eventCamera.depth 高者优先
        ///     module.sortOrderPriority 高者优先
        ///     module.renderOrderPriority 高者优先
        /// 如果是相同的Raycaster：
        ///     sortingLayer 高者优先
        ///     sortingOrder 高者优先
        ///     depth 高者优先
        ///     distance 小者优先
        ///     index 小者优先
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        private static int RaycastComparer(RaycastResult lhs, RaycastResult rhs)
        {
            if (lhs.module != rhs.module)
            {
                // 首先比较两个对象的 Camera 的 depth。在渲染中，Camera depth 越小会越先渲染，越大越往后渲染，
                // 因此对于射线检测来说，Camera 的 depth 越大，它对应的物体应该先于 Camera depth 小的物体进行射线检测，检测得到的结果也应排在前面。
                var lhsEventCamera = lhs.module.eventCamera;
                var rhsEventCamera = rhs.module.eventCamera;
                if (lhsEventCamera != null && rhsEventCamera != null && lhsEventCamera.depth != rhsEventCamera.depth)
                {
                    // need to reverse the standard compareTo
                    // lhsEventCamera.depth 摄像机渲染顺序，低深度摄像机在高深度摄像机之前渲染
                    // Use this to control the order in which cameras are drawn if you have multiple cameras and some of them don't cover the full screen.
                    if (lhsEventCamera.depth < rhsEventCamera.depth)
                        return 1;
                    if (lhsEventCamera.depth == rhsEventCamera.depth)
                        return 0;

                    return -1;
                }

                // 当 Camera depth 相等的时候，使用 sortOrderPriority 进行比较。优先级数值越大，越先被射线检测选中，
                // 所以这里的 CompareTo 方法使用的是右边的参数去比较左边的参数，最终的结果就是按照从大到小(降序)的顺序排列。
                // 在 PhysicsRaycaster 和 Physics2DRaycaster 类中没有覆写 sortOrderPriority 方法，因此都返回基类的 int.MinValue；
                // 但在 GraphicRaycaster 类中覆写了此方法，当对应的 Canvas 的 renderMode 设置为 RenderMode.ScreenSpaceOverlay 时，
                // 此时的 sortOrderPriority 返回 Canvas 的 sortingOrder(Sort Order越大越在上层)，
                // 否则同样也是返回基类设置的 int.MinValue，这是因为在 RenderMode.ScreenSpaceOverlay 模式下，所有的 distance 都将是 0。
                if (lhs.module.sortOrderPriority != rhs.module.sortOrderPriority)
                    return rhs.module.sortOrderPriority.CompareTo(lhs.module.sortOrderPriority);

                // 当 sortOrderPriority 相同，再使用 renderOrderPriority 比较。
                // renderOrderPriority 和 sortOrderPriority 类似，仅在 GraphicRaycaster 类中被覆写，
                // 也只有在 Canvas 的 renderMode 设置为 RenderMode.ScreenSpaceOverlay 时才返回 canvas.rootCanvas.renderOrder，
                // 这是因为 Canvas 在其他几种 renderMode 下，渲染的先后顺序都和距离摄像机的距离有关。
                // 所以 renderOrderPriority 比较也是按照从大到小的顺序得到最终的结果。
                if (lhs.module.renderOrderPriority != rhs.module.renderOrderPriority)
                    return rhs.module.renderOrderPriority.CompareTo(lhs.module.renderOrderPriority);
            }

            // 同属于一个 Raycaster 检测得到，但是它们的 sortingLayer 不一样
            // - 对于 PhysicsRaycaster 检测得到的对象，sortingLayer 都为 0。
            // - 对于 Physics2DRaycaster 检测得到的对象，如果对象上挂载有 SpriteRenderer 组件，那么 sortingLayer 对应的 sortingLayerID，否则也为 0。
            // - 对于 GraphicRaycaster 检测所得，sortingLayer 就是所在 Canvas 的 sortingLayerID。
            if (lhs.sortingLayer != rhs.sortingLayer)
            {
                // Uses the layer value to properly compare the relative order of the layers.
                // 通过 SortingLayer.GetLayerValueFromID 方法计算 sortingLayer 最终的 sorting layer 值，同样是按照降序排列，
                // 因此计算得到的 sorting layer 值越大越先排在前面。
                var rid = SortingLayer.GetLayerValueFromID(rhs.sortingLayer);
                var lid = SortingLayer.GetLayerValueFromID(lhs.sortingLayer);
                return rid.CompareTo(lid);
            }

            // sortingLayer 也相同，使用 sortingOrder 比较
            // sortingOrder 和 sortingLayer 类似，PhysicsRaycaster 检测得到的对象 sortingOrder 为 0；
            // Physics2DRaycaster 检测得到的对象是 SpriteRenderer 中的 sortingOrder；
            // GraphicRaycaster 检测所得是所在 Canvas 的 sortingOrder。最终 sortingOrder 越大的对象越排前面。
            if (lhs.sortingOrder != rhs.sortingOrder)
                return rhs.sortingOrder.CompareTo(lhs.sortingOrder);

            // sortingOrder 相同，使用 depth 比较
            // PhysicsRaycaster 和 Physics2DRaycaster 中 depth 都被设置为了 0；
            // GraphicRaycaster 检测所得的对象的 depth 就是继承自 Graphic 类的对象所在的 Graphic 的 depth，
            // 即 Canvas 下所有 Graphic 深度遍历的顺序。比较同样也是按照降序进行的，因此越嵌套在靠近 Canvas 的对象越排在前面。
            // comparing depth only makes sense if the two raycast results have the same root canvas (case 912396)
            if (lhs.depth != rhs.depth && lhs.module.rootRaycaster == rhs.module.rootRaycaster)
                return rhs.depth.CompareTo(lhs.depth);

            // depth 相同，使用 distance 比较
            // PhysicsRaycaster 中的 distance 就是 RaycastHit 的 distance(射线起点到射线碰撞点的距离)。
            // Physics2DRaycaster 类中返回的是 Camera 的位置和射线碰撞点之间的距离。
            // GraphicRaycaster 类中 distance 计算如下:
            // var go = m_RaycastResults[index].gameObject;
            // Transform trans = go.transform
            // Vector3 transForward = trans.forward;
            // distance = Vector3.Dot(transForward, trans.position - currentEventCamera.transform.position) / Vector3.Dot(transForward, ray.direction);
            // 距离 distance 越小越靠前。
            if (lhs.distance != rhs.distance)
                return lhs.distance.CompareTo(rhs.distance);

            // 最后如果上述情况都不能满足，使用 index 比较。先被射线检测到的对象排在前面。
            return lhs.index.CompareTo(rhs.index);
        }

        private static readonly Comparison<RaycastResult> s_RaycastComparer = RaycastComparer;

        /// <summary>
        /// Raycast into the scene using all configured BaseRaycasters.
        /// 各个在RaycasterManager中记录的激活状态的Raycaster调用Raycast方法，在传入的raycastResults后追加射线投射结果。
        /// 遍历全部的Raycaster之后，对所有的投射结果排序。
        /// </summary>
        /// <param name="eventData">Current pointer data.</param>
        /// <param name="raycastResults">List of 'hits' to populate.</param>
        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults)
        {
            raycastResults.Clear();
            var modules = RaycasterManager.GetRaycasters();
            for (int i = 0; i < modules.Count; ++i)
            {
                var module = modules[i];
                if (module == null || !module.IsActive())
                    continue;

                module.Raycast(eventData, raycastResults);
            }

            raycastResults.Sort(s_RaycastComparer);
        }

        /// <summary>
        /// Is the pointer with the given ID over an EventSystem object?
        /// </summary>
        public bool IsPointerOverGameObject()
        {
            return IsPointerOverGameObject(PointerInputModule.kMouseLeftId);
        }

        /// <summary>
        /// Is the pointer with the given ID over an EventSystem object?
        /// </summary>
        /// <remarks>
        /// If you use IsPointerOverGameObject() without a parameter, it points to the "left mouse button" (pointerId = -1); therefore when you use IsPointerOverGameObject for touch, you should consider passing a pointerId to it
        /// Note that for touch, IsPointerOverGameObject should be used with ''OnMouseDown()'' or ''Input.GetMouseButtonDown(0)'' or ''Input.GetTouch(0).phase == TouchPhase.Began''.
        /// </remarks>
        /// <example>
        /// <code>
        /// using UnityEngine;
        /// using System.Collections;
        /// using UnityEngine.EventSystems;
        ///
        /// public class MouseExample : MonoBehaviour
        /// {
        ///     void Update()
        ///     {
        ///         // Check if the left mouse button was clicked
        ///         if (Input.GetMouseButtonDown(0))
        ///         {
        ///             // Check if the mouse was clicked over a UI element
        ///             if (EventSystem.current.IsPointerOverGameObject())
        ///             {
        ///                 Debug.Log("Clicked on the UI");
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool IsPointerOverGameObject(int pointerId)
        {
            if (m_CurrentInputModule == null)
                return false;

            return m_CurrentInputModule.IsPointerOverGameObject(pointerId);
        }

        // OnEnable中把自己加入到静态事件系统列表中
        // current属性get访问器来看，总是会返回m_EventSystems第一个EventSytem对象返回
        // 所以，就目前UGUI实现来说，EventSystem是一个单例
        protected override void OnEnable()
        {
            base.OnEnable();
            m_EventSystems.Add(this);
        }

        // OnDisable中把自己从静态时间系统列表中删除（如果m_CurrentInputModule不为null，则调用其DeactivateModule方法）
        protected override void OnDisable()
        {
            if (m_CurrentInputModule != null)
            {
                m_CurrentInputModule.DeactivateModule();
                m_CurrentInputModule = null;
            }

            m_EventSystems.Remove(this);

            base.OnDisable();
        }

        //在Update中调用，每帧刷新
        //遍历m_SystemInputModules列表，调用InputModule的UpdateModule方法
        private void TickModules()
        {
            for (var i = 0; i < m_SystemInputModules.Count; i++)
            {
                if (m_SystemInputModules[i] != null)
                    m_SystemInputModules[i].UpdateModule();
            }
        }

        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            m_HasFocus = hasFocus;
        }

        //每帧更新
        //做三件事情：
        //（1）在TickModules函数中遍历所有可用InputModule，并调用其UpdateModule方法
        //（2）检查输入模块是否发生变化。遍历InputModule列表，找到第一个可用（被支持且应当被激活）的InputModule，设置m_CurrentInputModule
        //（3）调用当前InputModule的Process处理函数
        protected virtual void Update()
        {
            if (current != this)
                return;
            TickModules();

            bool changedModule = false;
            for (var i = 0; i < m_SystemInputModules.Count; i++)
            {
                var module = m_SystemInputModules[i];
                if (module.IsModuleSupported() && module.ShouldActivateModule())
                {
                    if (m_CurrentInputModule != module)
                    {
                        ChangeEventModule(module);
                        changedModule = true;
                    }
                    break;
                }
            }

            // no event module set... set the first valid one...
            if (m_CurrentInputModule == null)
            {
                for (var i = 0; i < m_SystemInputModules.Count; i++)
                {
                    var module = m_SystemInputModules[i];
                    if (module.IsModuleSupported())
                    {
                        ChangeEventModule(module);
                        changedModule = true;
                        break;
                    }
                }
            }

            // 输入模块没有发生变化
            if (!changedModule && m_CurrentInputModule != null)
                m_CurrentInputModule.Process();
        }

        // 切换InputModule
        // 做了两件事情：（1）调用前一个InputModule的DeactivateModule函数（2）调用当前InputModule的ActivateModule函数
        private void ChangeEventModule(BaseInputModule module)
        {
            if (m_CurrentInputModule == module)
                return;

            if (m_CurrentInputModule != null)
                m_CurrentInputModule.DeactivateModule();

            if (module != null)
                module.ActivateModule();

            m_CurrentInputModule = module;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>Selected:</b>" + currentSelectedGameObject);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(m_CurrentInputModule != null ? m_CurrentInputModule.ToString() : "No module");
            return sb.ToString();
        }
    }
}
