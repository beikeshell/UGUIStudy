namespace UnityEngine.EventSystems
{
    /// <summary>
    /// Event Data associated with Axis Events (Controller / Keyboard).
    /// 包含两个属性
    ///     （1）移动方向 moveDir
    ///     （2）移动矢量 moveVector
    /// </summary>
    public class AxisEventData : BaseEventData
    {
        /// <summary>
        /// Raw input vector associated with this event.
        /// </summary>
        public Vector2 moveVector { get; set; }

        /// <summary>
        /// MoveDirection for this event.
        /// 移动方向：Left/Up/Right/Down/None
        /// </summary>
        public MoveDirection moveDir { get; set; }

        public AxisEventData(EventSystem eventSystem)
            : base(eventSystem)
        {
            moveVector = Vector2.zero;
            moveDir = MoveDirection.None;
        }
    }
}
