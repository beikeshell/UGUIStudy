using System;
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    /// <summary>
    /// Base class for any RayCaster.
    /// Raycaster的职责是向场景中的对象投射射线以判断光标是否在其上方.
    /// 在Unity中，Raycaster系统用于当事件发生时筛选和确定事件的接收对象。
    /// 对于指定的屏幕坐标点，使用射线投射可以取到【所有】位于该坐标点之下的对象并得出其中【最靠近屏幕】的一个。
    /// </summary>
    /// <remarks>
    /// A Raycaster is responsible for raycasting against scene elements to determine if the cursor is over them.
    /// Default Raycasters include PhysicsRaycaster, Physics2DRaycaster, GraphicRaycaster.
    /// Custom raycasters can be added by extending this class.
    /// </remarks>
    public abstract class BaseRaycaster : UIBehaviour
    {
        private BaseRaycaster m_RootRaycaster;

        /// <summary>
        /// Raycast against the scene.
        /// 执行射线投射的方法
        /// </summary>
        /// <param name="eventData">Current event data.</param>
        /// <param name="resultAppendList">List of hit Objects.</param>
        public abstract void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList);

        /// <summary>
        /// The camera that will generate rays for this raycaster.
        /// 用于产生射线的事件摄像机
        /// </summary>
        public abstract Camera eventCamera { get; }

        [Obsolete("Please use sortOrderPriority and renderOrderPriority", false)]
        public virtual int priority
        {
            get { return 0; }
        }

        /// <summary>
        /// Priority of the raycaster based upon sort order.
        /// </summary>
        public virtual int sortOrderPriority
        {
            get { return int.MinValue; }
        }

        /// <summary>
        /// Priority of the raycaster based upon render order.
        /// </summary>
        public virtual int renderOrderPriority
        {
            get { return int.MinValue; }
        }

        /// <summary>
        /// Raycaster on root canvas
        /// </summary>
        public BaseRaycaster rootRaycaster
        {
            get
            {
                if (m_RootRaycaster == null)
                {
                    var baseRaycasters = GetComponentsInParent<BaseRaycaster>();
                    if (baseRaycasters.Length != 0)
                        m_RootRaycaster = baseRaycasters[baseRaycasters.Length - 1];
                }

                return m_RootRaycaster;
            }
        }

        public override string ToString()
        {
            return "Name: " + gameObject + "\n" +
                "eventCamera: " + eventCamera + "\n" +
                "sortOrderPriority: " + sortOrderPriority + "\n" +
                "renderOrderPriority: " + renderOrderPriority;
        }

        /// <summary>
        /// 在OnEnable()和OnDisable()中，Raycaster会把自己从RaycasterManager管理的一个列表中添加/移除。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            RaycasterManager.AddRaycaster(this);
        }

        /// <summary>
        /// 在OnEnable()和OnDisable()中，Raycaster会把自己从RaycasterManager管理的一个列表中添加/移除。
        /// </summary>
        protected override void OnDisable()
        {
            RaycasterManager.RemoveRaycasters(this);
            base.OnDisable();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            m_RootRaycaster = null;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            m_RootRaycaster = null;
        }
    }
}
