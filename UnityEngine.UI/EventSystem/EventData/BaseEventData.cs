namespace UnityEngine.EventSystems
{
    /// <summary>
    /// A class that can be used for sending simple events via the event system.
    /// EventData抽象基类，只有一个m_Used字段表示该事件数据是否被使用过
    /// </summary>
    public abstract class AbstractEventData
    {
        protected bool m_Used;

        /// <summary>
        /// Reset the event.
        /// </summary>
        public virtual void Reset()
        {
            m_Used = false;
        }

        /// <summary>
        /// Use the event.
        /// </summary>
        /// <remarks>
        /// Internally sets a flag that can be checked via used to see if further processing should happen.
        /// </remarks>
        public virtual void Use()
        {
            m_Used = true;
        }

        /// <summary>
        /// Is the event used?
        /// </summary>
        public virtual bool used
        {
            get { return m_Used; }
        }
    }

    /// <summary>
    /// A class that contains the base event data that is common to all event types in the new EventSystem.
    /// 相对于AbstractEventData多了两个属性（1）当前输入模块（2）当前被选择对象
    /// </summary>
    public class BaseEventData : AbstractEventData
    {
        // 保持对当前EventSystem对象的引用
        private readonly EventSystem m_EventSystem;
        public BaseEventData(EventSystem eventSystem)
        {
            m_EventSystem = eventSystem;
        }

        /// <summary>
        /// >A reference to the BaseInputModule that sent this event.
        /// 保持对当前InputModule的引用
        /// </summary>
        public BaseInputModule currentInputModule
        {
            get { return m_EventSystem.currentInputModule; }
        }

        /// <summary>
        /// The object currently considered selected by the EventSystem.
        /// 当前被EventSystem选中的GameObject
        /// </summary>
        public GameObject selectedObject
        {
            get { return m_EventSystem.currentSelectedGameObject; }
            set { m_EventSystem.SetSelectedGameObject(value, this); }
        }
    }
}
