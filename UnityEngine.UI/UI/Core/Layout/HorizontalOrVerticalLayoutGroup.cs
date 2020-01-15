using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// <summary>
    /// Abstract base class for HorizontalLayoutGroup and VerticalLayoutGroup to generalize common functionality.
    /// </summary>
    ///
    [ExecuteAlways]
    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        [SerializeField] protected float m_Spacing = 0;

        /// <summary>
        /// The spacing to use between layout elements in the layout group.
        /// </summary>
        public float spacing { get { return m_Spacing; } set { SetProperty(ref m_Spacing, value); } }

        [SerializeField] protected bool m_ChildForceExpandWidth = true;
        /// <summary>
        /// Whether to force the children to expand to fill additional available horizontal space.
        /// 是否强制孩子扩展以填充其他可用的水平空间
        /// </summary>
        public bool childForceExpandWidth { get { return m_ChildForceExpandWidth; } set { SetProperty(ref m_ChildForceExpandWidth, value); } }

        [SerializeField] protected bool m_ChildForceExpandHeight = true;

        /// <summary>
        /// Whether to force the children to expand to fill additional available vertical space.
        /// 是否强制孩子扩展以填充其他可用的垂直空间
        /// </summary>
        public bool childForceExpandHeight { get { return m_ChildForceExpandHeight; } set { SetProperty(ref m_ChildForceExpandHeight, value); } }

        [SerializeField] protected bool m_ChildControlWidth = true;

        /// <summary>
        /// Returns true if the Layout Group controls the widths of its children.
        /// Returns false if children control their own widths.
        /// </summary>
        /// <remarks>
        /// If set to false, the layout group will only affect the positions of the children while leaving the widths untouched.
        /// The widths of the children can be set via the respective RectTransforms in this case.
        /// 如果设置为false，则布局组将仅影响子项的位置，而宽度保持不变。 在这种情况下，可以通过相应的RectTransforms设置子项的宽度。
        ///
        /// If set to true, the widths of the children are automatically driven by the layout group according to
        /// their respective minimum, preferred, and flexible widths.
        /// This is useful if the widths of the children should change depending on how much space is available.
        /// In this case the width of each child cannot be set manually in the RectTransform,
        /// BUT the minimum, preferred and flexible width for each child can be controlled by adding a LayoutElement component to it.
        /// </remarks>
        public bool childControlWidth { get { return m_ChildControlWidth; } set { SetProperty(ref m_ChildControlWidth, value); } }

        [SerializeField] protected bool m_ChildControlHeight = true;

        /// <summary>
        /// Returns true if the Layout Group controls the heights of its children.
        /// Returns false if children control their own heights.
        /// </summary>
        /// <remarks>
        /// If set to false, the layout group will only affect the positions of the children while leaving the heights untouched.
        /// The heights of the children can be set via the respective RectTransforms in this case.
        ///
        /// If set to true, the heights of the children are automatically driven by the layout group according to
        /// their respective minimum, preferred, and flexible heights.
        /// This is useful if the heights of the children should change depending on how much space is available.
        /// In this case the height of each child cannot be set manually in the RectTransform,
        /// BUT the minimum, preferred and flexible height for each child can be controlled by adding a LayoutElement component to it.
        /// </remarks>
        public bool childControlHeight { get { return m_ChildControlHeight; } set { SetProperty(ref m_ChildControlHeight, value); } }

        [SerializeField] protected bool m_ChildScaleWidth = false;

        /// <summary>
        /// Whether children widths are scaled by their x scale.
        /// </summary>
        public bool childScaleWidth { get { return m_ChildScaleWidth; } set { SetProperty(ref m_ChildScaleWidth, value); } }

        [SerializeField] protected bool m_ChildScaleHeight = false;

        /// <summary>
        /// Whether children heights are scaled by their y scale.
        /// </summary>
        public bool childScaleHeight { get { return m_ChildScaleHeight; } set { SetProperty(ref m_ChildScaleHeight, value); } }

        /// <summary>
        /// Calculate the layout element properties for this layout element along the given axis.
        /// (1) 第一个要注意的点就是参数，axis表示调用该方法是为了计算更新水平方向0还是竖直方向1的输入参数，
        ///     而isVertical这是指当前的Layout是为了控制水平方向false还是竖直方向true；
        /// (2) 函数内部，首先是获取padding值，是否控制子节点尺寸，是否控制子节点间隔；
        /// (3) 初始化totalMin，totalPreferred和totalFlexible三个值；
        /// (4) 获取alongOtherAxis，axis和isVertical表示的方向不相同时，此值为true；
        /// (5) 遍历rectChildren（即之前基类中的m_RectChildren），使用GetChildSizes获取三个out参数min，preferred和flexible。
        /// (6) 接下来会用到一个spacing参数，是一开始指定好的：
        /// (7) 根据计算的方向与Layout自身控制的方向是否一致来更新totalMin，totalPreferred和totalFlexible；
        ///     如果方向是一致的，那么三个totalXxx的值都要加上当前遍历的子节点的这三个值（及spacing）；
        /// (8) 遍历结束后，如果计算方向与Layout控制的方向一致，且子元素数量大于0，则减去一次spacing；
        /// (9) 对totalPreferred进行修正，应当不小于totalMin；
        /// (10) 最后把计算得出的参数赋值给LayoutGroup中的三个Vector2成员
        /// </summary>
        /// <param name="axis">The axis to calculate for. 0 is horizontal and 1 is vertical.</param>
        /// <param name="isVertical">Is this group a vertical group?</param>
        protected void CalcAlongAxis(int axis, bool isVertical)
        {
            float combinedPadding = (axis == 0 ? padding.horizontal : padding.vertical);
            bool controlSize = (axis == 0 ? m_ChildControlWidth : m_ChildControlHeight);
            bool useScale = (axis == 0 ? m_ChildScaleWidth : m_ChildScaleHeight);
            bool childForceExpandSize = (axis == 0 ? m_ChildForceExpandWidth : m_ChildForceExpandHeight);

            float totalMin = combinedPadding;
            float totalPreferred = combinedPadding;
            float totalFlexible = 0;

            bool alongOtherAxis = (isVertical ^ (axis == 1));
            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform child = rectChildren[i];
                float min, preferred, flexible;
                GetChildSizes(child, axis, controlSize, childForceExpandSize, out min, out preferred, out flexible);

                if (useScale)
                {
                    float scaleFactor = child.localScale[axis];
                    min *= scaleFactor;
                    preferred *= scaleFactor;
                    flexible *= scaleFactor;
                }

                if (alongOtherAxis)
                {
                    totalMin = Mathf.Max(min + combinedPadding, totalMin);
                    totalPreferred = Mathf.Max(preferred + combinedPadding, totalPreferred);
                    totalFlexible = Mathf.Max(flexible, totalFlexible);
                }
                else
                {
                    totalMin += min + spacing;
                    totalPreferred += preferred + spacing;

                    // Increment flexible size with element's flexible size.
                    totalFlexible += flexible;
                }
            }

            if (!alongOtherAxis && rectChildren.Count > 0)
            {
                totalMin -= spacing;
                totalPreferred -= spacing;
            }
            totalPreferred = Mathf.Max(totalMin, totalPreferred);
            SetLayoutInputForAxis(totalMin, totalPreferred, totalFlexible, axis);
        }

        /// <summary>
        /// Set the positions and sizes of the child layout elements for the given axis.
        /// </summary>
        /// <param name="axis">The axis to handle. 0 is horizontal and 1 is vertical.</param>
        /// <param name="isVertical">Is this group a vertical group?</param>
        protected void SetChildrenAlongAxis(int axis, bool isVertical)
        {
            float size = rectTransform.rect.size[axis];
            bool controlSize = (axis == 0 ? m_ChildControlWidth : m_ChildControlHeight);
            bool useScale = (axis == 0 ? m_ChildScaleWidth : m_ChildScaleHeight);
            bool childForceExpandSize = (axis == 0 ? m_ChildForceExpandWidth : m_ChildForceExpandHeight);
            float alignmentOnAxis = GetAlignmentOnAxis(axis);

            bool alongOtherAxis = (isVertical ^ (axis == 1));
            if (alongOtherAxis)
            {
                float innerSize = size - (axis == 0 ? padding.horizontal : padding.vertical);
                for (int i = 0; i < rectChildren.Count; i++)
                {
                    RectTransform child = rectChildren[i];
                    float min, preferred, flexible;
                    GetChildSizes(child, axis, controlSize, childForceExpandSize, out min, out preferred, out flexible);
                    float scaleFactor = useScale ? child.localScale[axis] : 1f;

                    float requiredSpace = Mathf.Clamp(innerSize, min, flexible > 0 ? size : preferred);
                    float startOffset = GetStartOffset(axis, requiredSpace * scaleFactor);
                    if (controlSize)
                    {
                        SetChildAlongAxisWithScale(child, axis, startOffset, requiredSpace, scaleFactor);
                    }
                    else
                    {
                        float offsetInCell = (requiredSpace - child.sizeDelta[axis]) * alignmentOnAxis;
                        SetChildAlongAxisWithScale(child, axis, startOffset + offsetInCell, scaleFactor);
                    }
                }
            }
            else
            {
                // pos表示第一个子节点（即将放置的子节点）的起始位置，后边遍历的过程中这个值会逐渐递增
                float pos = (axis == 0 ? padding.left : padding.top);
                // itemFlexibleMultiplier表示如果LayoutGroup尺寸真的比preferred更大时，
                // 将对各子节点的尺寸方法的系数（前提是子节点的flexible为正数）
                float itemFlexibleMultiplier = 0;
                float surplusSpace = size - GetTotalPreferredSize(axis);

                if (surplusSpace > 0)
                {
                    if (GetTotalFlexibleSize(axis) == 0)
                        pos = GetStartOffset(axis, GetTotalPreferredSize(axis) - (axis == 0 ? padding.horizontal : padding.vertical));
                    else if (GetTotalFlexibleSize(axis) > 0)
                        itemFlexibleMultiplier = surplusSpace / GetTotalFlexibleSize(axis);
                }

                // minMaxLerp表示当preferred大于min时，将会根据该LayoutGroup的尺寸和preferred作比较，
                // 得出在min和preferred之间插值的系数，同时又有Clamp01以确保不会超过preferred；
                float minMaxLerp = 0;
                if (GetTotalMinSize(axis) != GetTotalPreferredSize(axis))
                    minMaxLerp = Mathf.Clamp01((size - GetTotalMinSize(axis)) / (GetTotalPreferredSize(axis) - GetTotalMinSize(axis)));

                for (int i = 0; i < rectChildren.Count; i++)
                {
                    RectTransform child = rectChildren[i];
                    float min, preferred, flexible;
                    GetChildSizes(child, axis, controlSize, childForceExpandSize, out min, out preferred, out flexible);
                    float scaleFactor = useScale ? child.localScale[axis] : 1f;

                    float childSize = Mathf.Lerp(min, preferred, minMaxLerp);
                    childSize += flexible * itemFlexibleMultiplier;
                    if (controlSize)
                    {
                        SetChildAlongAxisWithScale(child, axis, pos, childSize, scaleFactor);
                    }
                    else
                    {
                        float offsetInCell = (childSize - child.sizeDelta[axis]) * alignmentOnAxis;
                        SetChildAlongAxisWithScale(child, axis, pos + offsetInCell, scaleFactor);
                    }
                    pos += childSize * scaleFactor + spacing;
                }
            }
        }

        /// <summary>
        /// 主要是分了两种情况，是否controlSize，如果是false，那么就取子节点真实的尺寸，min即是sizeDelta对应维度的值，preferred同min，flexible为0；
        /// 如果是true，情况就比较复杂了。这里把获取这三个尺寸的逻辑封装到了LayoutUtility的静态方法里。
        /// 最后注意的一点就是，如果childForceExpand，flexible会取到一个最大为1的值（这里应该就是把它设置为正数）。
        /// </summary>
        /// <param name="child"></param>
        /// <param name="axis"></param>
        /// <param name="controlSize"></param>
        /// <param name="childForceExpand"></param>
        /// <param name="min"></param>
        /// <param name="preferred"></param>
        /// <param name="flexible"></param>
        private void GetChildSizes(RectTransform child, int axis, bool controlSize, bool childForceExpand,
            out float min, out float preferred, out float flexible)
        {
            if (!controlSize)
            {
                min = child.sizeDelta[axis];
                preferred = min;
                flexible = 0;
            }
            else
            {
                min = LayoutUtility.GetMinSize(child, axis);
                preferred = LayoutUtility.GetPreferredSize(child, axis);
                flexible = LayoutUtility.GetFlexibleSize(child, axis);
            }

            if (childForceExpand)
                flexible = Mathf.Max(flexible, 1);
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();

            // For new added components we want these to be set to false,
            // so that the user's sizes won't be overwritten before they
            // have a chance to turn these settings off.
            // However, for existing components that were added before this
            // feature was introduced, we want it to be on be default for
            // backwardds compatibility.
            // Hence their default value is on, but we set to off in reset.
            m_ChildControlWidth = false;
            m_ChildControlHeight = false;
        }

        private int m_Capacity = 10;
        private Vector2[] m_Sizes = new Vector2[10];

        protected virtual void Update()
        {
            if (Application.isPlaying)
                return;

            int count = transform.childCount;

            if (count > m_Capacity)
            {
                if (count > m_Capacity * 2)
                    m_Capacity = count;
                else
                    m_Capacity *= 2;

                m_Sizes = new Vector2[m_Capacity];
            }

            // If children size change in editor, update layout (case 945680 - Child GameObjects in a Horizontal/Vertical Layout Group don't display their correct position in the Editor)
            bool dirty = false;
            for (int i = 0; i < count; i++)
            {
                RectTransform t = transform.GetChild(i) as RectTransform;
                if (t != null && t.sizeDelta != m_Sizes[i])
                {
                    dirty = true;
                    m_Sizes[i] = t.sizeDelta;
                }
            }

            if (dirty)
                LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
        }

#endif
    }
}
