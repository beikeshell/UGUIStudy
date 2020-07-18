using System;

namespace UnityEngine.UI
{
    /// <summary>
    ///   This element is capable of being masked out.
    ///   实现此接口的类可以被Mask遮罩。
    /// </summary>
    public interface IMaskable
    {
        /// <summary>
        /// Recalculate masking for this element and all children elements.
        ///更新对【当前对象】和【所有子节点对象】的遮罩信息
        /// </summary>
        /// <remarks>
        /// Use this to update the internal state (recreate materials etc).
        /// </remarks>
        void RecalculateMasking();
    }
}
