namespace UnityEngine.UI
{
    /// <summary>
    ///   Interface that can be used to recieve clipping callbacks as part of the canvas update loop.
    ///   裁剪器
    /// </summary>
    public interface IClipper
    {
        /// <summary>
        /// Function to to cull / clip children elements.
        /// </summary>
        /// <remarks>
        /// Called after layout and before Graphic update of the Canvas update loop.
        /// </remarks>

        void PerformClipping();
    }

    /// <summary>
    ///   Interface for elements that can be clipped if they are under an IClipper
    ///   可裁剪对象
    ///   实现此接口的类可以被Clipper（如RectMask2D）裁减
    /// </summary>
    public interface IClippable
    {
        /// <summary>
        /// GameObject of the IClippable object
        /// </summary>
        GameObject gameObject { get; }

        /// <summary>
        /// Will be called when the state of a parent IClippable changed.
        /// 当父级裁剪对象发生变化时调用，用于更新裁剪信息
        /// </summary>
        void RecalculateClipping();

        /// <summary>
        /// The RectTransform of the clippable.
        /// </summary>
        RectTransform rectTransform { get; }

        /// <summary>
        /// Clip and cull the IClippable given a specific clipping rect
        /// 对可裁剪的对象裁剪和剔除
        /// </summary>
        /// <param name="clipRect">The Rectangle in which to clip against.</param>
        /// <param name="validRect">Is the Rect valid. If not then the rect has 0 size.</param>
        void Cull(Rect clipRect, bool validRect);

        /// <summary>
        /// Set the clip rect for the IClippable.
        /// 设置可裁剪区域
        /// </summary>
        /// <param name="value">The Rectangle for the clipping</param>
        /// <param name="validRect">Is the rect valid.</param>
        void SetClipRect(Rect value, bool validRect);
    }
}
