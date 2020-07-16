using System;
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    [RequireComponent(typeof(EventSystem))]
    /// <summary>
    /// A base module that raises events and sends them to GameObjects.
    /// </summary>
    /// <remarks>
    /// An Input Module is a component of the EventSystem that is responsible for raising events and sending them to GameObjects for handling.
    /// The BaseInputModule is a class that all Input Modules in the EventSystem inherit from.
    /// Examples of provided modules are TouchInputModule and StandaloneInputModule,
    /// if these are inadequate for your project you can create your own by extending from the BaseInputModule.
    /// </remarks>
    /// <example>
    /// <code>
    /// using UnityEngine;
    /// using UnityEngine.EventSystems;
    ///
    /// /**
    ///  * Create a module that every tick sends a 'Move' event to
    ///  * the target object
    ///  */
    /// public class MyInputModule : BaseInputModule
    /// {
    ///     public GameObject m_TargetObject;
    ///
    ///     public override void Process()
    ///     {
    ///         if (m_TargetObject == null)
    ///             return;
    ///         ExecuteEvents.Execute (m_TargetObject, new BaseEventData (eventSystem), ExecuteEvents.moveHandler);
    ///     }
    /// }
    /// </code>
    /// </example>
    ///

    public abstract class BaseInputModule : UIBehaviour
    {
        [NonSerialized]
        protected List<RaycastResult> m_RaycastResultCache = new List<RaycastResult>();

        private AxisEventData m_AxisEventData;

        private EventSystem m_EventSystem;
        private BaseEventData m_BaseEventData;

        protected BaseInput m_InputOverride;
        private BaseInput m_DefaultInput;

        /// <summary>
        /// The current BaseInput being used by the input module.
        /// </summary>
        public BaseInput input
        {
            get
            {
                if (m_InputOverride != null)
                    return m_InputOverride;

                if (m_DefaultInput == null)
                {
                    var inputs = GetComponents<BaseInput>();
                    foreach (var baseInput in inputs)
                    {
                        // We dont want to use any classes that derrive from BaseInput for default.
                        if (baseInput != null && baseInput.GetType() == typeof(BaseInput))
                        {
                            m_DefaultInput = baseInput;
                            break;
                        }
                    }

                    if (m_DefaultInput == null)
                        m_DefaultInput = gameObject.AddComponent<BaseInput>();
                }

                return m_DefaultInput;
            }
        }

        /// <summary>
        /// Used to override the default BaseInput for the input module.
        /// </summary>
        /// <remarks>
        /// With this it is possible to bypass the Input system with your own but still use the same InputModule.
        /// For example this can be used to feed fake input into the UI or interface with a different input system.
        /// </remarks>
        public BaseInput inputOverride
        {
            get { return m_InputOverride; }
            set { m_InputOverride = value; }
        }

        protected EventSystem eventSystem
        {
            get { return m_EventSystem; }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_EventSystem = GetComponent<EventSystem>();
            m_EventSystem.UpdateModules();
        }

        protected override void OnDisable()
        {
            m_EventSystem.UpdateModules();
            base.OnDisable();
        }

        /// <summary>
        /// Process the current tick for the module.
        /// 子类实现
        /// 在EventSystem中被每帧调用
        /// </summary>
        public abstract void Process();

        /// <summary>
        /// Return the first valid RaycastResult.
        /// 找到第一个有效的RaycastResult
        /// </summary>
        protected static RaycastResult FindFirstRaycast(List<RaycastResult> candidates)
        {
            for (var i = 0; i < candidates.Count; ++i)
            {
                if (candidates[i].gameObject == null)
                    continue;

                return candidates[i];
            }
            return new RaycastResult();
        }

        /// <summary>
        /// Given an input movement, determine the best MoveDirection.
        /// </summary>
        /// <param name="x">X movement.</param>
        /// <param name="y">Y movement.</param>
        protected static MoveDirection DetermineMoveDirection(float x, float y)
        {
            return DetermineMoveDirection(x, y, 0.6f);
        }

        /// <summary>
        /// Given an input movement, determine the best MoveDirection.
        /// </summary>
        /// <param name="x">X movement.</param>
        /// <param name="y">Y movement.</param>
        /// <param name="deadZone">Dead zone.</param>
        protected static MoveDirection DetermineMoveDirection(float x, float y, float deadZone)
        {
            // if vector is too small... just return
            if (new Vector2(x, y).sqrMagnitude < deadZone * deadZone)
                return MoveDirection.None;

            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                if (x > 0)
                    return MoveDirection.Right;
                return MoveDirection.Left;
            }
            else
            {
                if (y > 0)
                    return MoveDirection.Up;
                return MoveDirection.Down;
            }
        }

        /// <summary>
        /// Given 2 GameObjects, return a common root GameObject (or null).
        /// 找到两个节点的共同根节点
        /// </summary>
        /// <param name="g1">GameObject to compare</param>
        /// <param name="g2">GameObject to compare</param>
        /// <returns></returns>
        protected static GameObject FindCommonRoot(GameObject g1, GameObject g2)
        {
            if (g1 == null || g2 == null)
                return null;

            var t1 = g1.transform;
            while (t1 != null)
            {
                var t2 = g2.transform;
                while (t2 != null)
                {
                    if (t1 == t2)
                        return t1.gameObject;
                    t2 = t2.parent;
                }
                t1 = t1.parent;
            }
            return null;
        }

        // walk up the tree till a common root between the last entered and the current entered is found
        // send exit events up to (but not including) the common root. Then send enter events up to
        // (but not including the common root).
        /// <summary>
        ///寻找之前最后一次进入的对象，以及当前进入的对象的共同父节点，然后对从离开的对象到共同父节点（不含共同的父节点）发送“离开（Exit）”事件，
        ///  同理对从进入的对象到共同父节点（不含共同的父节点）发送“进入（Enter）”事件。
        ///
        ///整个过程分为几步：
        /// （1）- 如果没有新进入的对象，或者currentPointerData.pointerEnter是null，那么就不处理进入（Enter），只处理离开（Exit），
        ///   即对currentPointerData.hovered中的各个对象都执行离开的事件，并在最后确保将currentPointerData.pointerEnter设为null
        /// （2）- 如果对象没有变化，就是说即将进入的对象和当前事件数据中的进入对象是同一个对象，直接返回。
        /// （3）- 寻找当前事件数据中的进入对象currentPointerData.pointerEnter和即将要进入的对象newEnterTarget二者的共同父节点。
        ///   然后如前边说的那样，从旧的进入对象到共有父节点，逐个执行离开事件，并从hovered中移除，此操作不含共有父节点；
        ///   接下来从新的进入对象到共有父节点，逐个执行进入事件，并将其加入hovered，此操作不包含共有父节点。
        /// </summary>
        /// <param name="currentPointerData"></param>
        /// <param name="newEnterTarget"></param>
        protected void HandlePointerExitAndEnter(PointerEventData currentPointerData, GameObject newEnterTarget)
        {
            // if we have no target or pointerEnter has been DELETED
            // just send exit events to anything we are tracking
            // then exit
            if (newEnterTarget == null || currentPointerData.pointerEnter == null)
            {
                for (var i = 0; i < currentPointerData.hovered.Count; ++i)
                    ExecuteEvents.Execute(currentPointerData.hovered[i], currentPointerData, ExecuteEvents.pointerExitHandler);

                currentPointerData.hovered.Clear();

                if (newEnterTarget == null)
                {
                    currentPointerData.pointerEnter = null;
                    return;
                }
            }

            // if we have not changed hover target
            if (currentPointerData.pointerEnter == newEnterTarget && newEnterTarget)
                return;

            GameObject commonRoot = FindCommonRoot(currentPointerData.pointerEnter, newEnterTarget);

            // and we already an entered object from last time
            if (currentPointerData.pointerEnter != null)
            {
                // send exit handler call to all elements in the chain
                // until we reach the new target, or null!
                Transform t = currentPointerData.pointerEnter.transform;

                while (t != null)
                {
                    // if we reach the common root break out!
                    if (commonRoot != null && commonRoot.transform == t)
                        break;

                    ExecuteEvents.Execute(t.gameObject, currentPointerData, ExecuteEvents.pointerExitHandler);
                    currentPointerData.hovered.Remove(t.gameObject);
                    t = t.parent;
                }
            }

            // now issue the enter call up to but not including the common root
            currentPointerData.pointerEnter = newEnterTarget;
            if (newEnterTarget != null)
            {
                Transform t = newEnterTarget.transform;

                while (t != null && t.gameObject != commonRoot)
                {
                    ExecuteEvents.Execute(t.gameObject, currentPointerData, ExecuteEvents.pointerEnterHandler);
                    currentPointerData.hovered.Add(t.gameObject);
                    t = t.parent;
                }
            }
        }

        /// <summary>
        /// Given some input data generate an AxisEventData that can be used by the event system.
        /// 生成一个AxisEventData
        /// </summary>
        /// <param name="x">X movement.</param>
        /// <param name="y">Y movement.</param>
        /// <param name="moveDeadZone">Dead zone.</param>
        protected virtual AxisEventData GetAxisEventData(float x, float y, float moveDeadZone)
        {
            if (m_AxisEventData == null)
                m_AxisEventData = new AxisEventData(eventSystem);

            //设置m_Used标记为假，表示该事件数据未被使用过
            m_AxisEventData.Reset();
            m_AxisEventData.moveVector = new Vector2(x, y);
            m_AxisEventData.moveDir = DetermineMoveDirection(x, y, moveDeadZone);
            return m_AxisEventData;
        }

        /// <summary>
        /// Generate a BaseEventData that can be used by the EventSystem.
        /// 生成一个BaseEventData
        /// </summary>
        protected virtual BaseEventData GetBaseEventData()
        {
            if (m_BaseEventData == null)
                m_BaseEventData = new BaseEventData(eventSystem);

            //设置m_Used标记为假，表示该事件数据未被使用过
            m_BaseEventData.Reset();
            return m_BaseEventData;
        }

        /// <summary>
        /// If the module is pointer based, then override this to return true if the pointer is over an event system object.
        /// </summary>
        /// <param name="pointerId">Pointer ID</param>
        /// <returns>Is the given pointer over an event system object?</returns>
        public virtual bool IsPointerOverGameObject(int pointerId)
        {
            return false;
        }

        /// <summary>
        /// Should the module be activated.
        /// </summary>
        public virtual bool ShouldActivateModule()
        {
            return enabled && gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Called when the module is deactivated.
        /// Override this if you want custom code to execute when you deactivate your module.
        /// </summary>
        public virtual void DeactivateModule()
        {}

        /// <summary>
        /// Called when the module is activated.
        /// Override this if you want custom code to execute when you activate your module.
        /// </summary>
        public virtual void ActivateModule()
        {}

        /// <summary>
        /// Update the internal state of the Module.
        /// 在EventSystem中被每帧调用
        /// </summary>
        public virtual void UpdateModule()
        {}

        /// <summary>
        /// Check to see if the module is supported. Override this if you have a platform specific module
        /// (eg. TouchInputModule that you do not want to activate on standalone.)
        /// </summary>
        /// <returns>Is the module supported.</returns>
        public virtual bool IsModuleSupported()
        {
            return true;
        }
    }
}
