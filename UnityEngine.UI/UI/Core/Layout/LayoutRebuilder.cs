using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    /// <summary>
    /// Wrapper class for managing layout rebuilding of CanvasElement.
    /// </summary>
    public class LayoutRebuilder : ICanvasElement
    {
        private RectTransform m_ToRebuild;
        //There are a few of reasons we need to cache the Hash fromt he transform:
        //  - This is a ValueType (struct) and .Net calculates Hash from the Value Type fields.
        //  - The key of a Dictionary should have a constant Hash value.
        //  - It's possible for the Transform to get nulled from the Native side.
        // We use this struct with the IndexedSet container, which uses a dictionary as part of it's implementation
        // So this struct gets used as a key to a dictionary, so we need to guarantee a constant Hash value.
        private int m_CachedHashFromTransform;

        static ObjectPool<LayoutRebuilder> s_Rebuilders = new ObjectPool<LayoutRebuilder>(null, x => x.Clear());

        private void Initialize(RectTransform controller)
        {
            m_ToRebuild = controller;
            m_CachedHashFromTransform = controller.GetHashCode();
        }

        private void Clear()
        {
            m_ToRebuild = null;
            m_CachedHashFromTransform = 0;
        }

        static LayoutRebuilder()
        {
            RectTransform.reapplyDrivenProperties += ReapplyDrivenProperties;
        }

        static void ReapplyDrivenProperties(RectTransform driven)
        {
            MarkLayoutForRebuild(driven);
        }

        public Transform transform { get { return m_ToRebuild; }}

        /// <summary>
        /// Has the native representation of this LayoutRebuilder been destroyed?
        /// </summary>
        public bool IsDestroyed()
        {
            return m_ToRebuild == null;
        }

        static void StripDisabledBehavioursFromList(List<Component> components)
        {
            components.RemoveAll(e => e is Behaviour && !((Behaviour)e).isActiveAndEnabled);
        }

        /// <summary>
        /// Forces an immediate rebuild of the layout element and child layout elements affected by the calculations.
        /// </summary>
        /// <param name="layoutRoot">The layout element to perform the layout rebuild on.</param>
        /// <remarks>
        /// Normal use of the layout system should not use this method.
        /// Instead MarkLayoutForRebuild should be used instead, which triggers a delayed layout rebuild during the next layout pass.
        /// The delayed rebuild automatically handles objects in the entire layout hierarchy in the correct order,
        /// and prevents multiple recalculations for the same layout elements.
        /// However, for special layout calculation needs,
        /// ::ref::ForceRebuildLayoutImmediate can be used to get the layout of a sub-tree resolved immediately.
        /// This can even be done from inside layout calculation methods such as
        /// ILayoutController.SetLayoutHorizontal orILayoutController.SetLayoutVertical.
        /// Usage should be restricted to cases where multiple layout passes are unavaoidable despite the extra cost in performance.
        /// </remarks>
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot)
        {
            var rebuilder = s_Rebuilders.Get();
            rebuilder.Initialize(layoutRoot);
            rebuilder.Rebuild(CanvasUpdate.Layout);
            s_Rebuilders.Release(rebuilder);
        }

        /// <summary>
        /// 只处理CanvasUpdate.Layout阶段的重建动作，核心内容四行代码，涉及到两个函数PerformLayoutCalculation和PerformLayoutControl，
        /// 分别用于计算参数和设置尺寸，先水平方向后竖直方向。
        /// 下边是PerformLayoutCalculation和PerformLayoutControl的定义，
        /// 前边的注释中提到了在这两个方法中会重复调用相同的GetComponents，虽然开销会比较大但无法避免，
        /// 【注意】把结果缓存起来（使用Dictionary等）会有更多的额外性能开销。
        /// </summary>
        /// <param name="executing"></param>
        public void Rebuild(CanvasUpdate executing)
        {
            switch (executing)
            {
                case CanvasUpdate.Layout:
                    // It's unfortunate that we'll perform the same GetComponents querys for the tree 2 times,
                    // but each tree have to be fully iterated before going to the next action,
                    // so reusing the results would entail storing results in a Dictionary or similar,
                    // which is probably a bigger overhead than performing GetComponents multiple times.
                    //
                    PerformLayoutCalculation(m_ToRebuild, e => (e as ILayoutElement).CalculateLayoutInputHorizontal());
                    PerformLayoutControl(m_ToRebuild, e => (e as ILayoutController).SetLayoutHorizontal());
                    PerformLayoutCalculation(m_ToRebuild, e => (e as ILayoutElement).CalculateLayoutInputVertical());
                    PerformLayoutControl(m_ToRebuild, e => (e as ILayoutController).SetLayoutVertical());
                    break;
            }
        }

        /// <summary>
        /// 在执行完CalculateLayoutInputXxx()之后，该ILayoutElement中的各数值都是计算和更新后的状态了，此时可以该调用ILayoutController的SetLayoutXxx方法来更新布局。
        ///
        /// PerformLayoutControl同样是传入两个参数，RectTransform和一个UnityAction<Component>，在方法中，获取rect所有的ILayoutController组件，
        /// 先对其中所有的ILayoutSelfController调用传入的action，接下来对其中的非ILayoutSelfController执行action，
        /// 最后对rect的子节点递归地调用PerformLayoutControl。真正的逻辑就藏在action里边
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="action"></param>
        private void PerformLayoutControl(RectTransform rect, UnityAction<Component> action)
        {
            if (rect == null)
                return;

            var components = ListPool<Component>.Get();
            rect.GetComponents(typeof(ILayoutController), components);
            StripDisabledBehavioursFromList(components);

            // If there are no controllers on this rect we can skip this entire sub-tree
            // We don't need to consider controllers on children deeper in the sub-tree either,
            // since they will be their own roots.
            if (components.Count > 0)
            {
                // Layout control needs to executed top down with parents being done before their children,
                // because the children rely on the sizes of the parents.

                // First call layout controllers that may change their own RectTransform
                for (int i = 0; i < components.Count; i++)
                    if (components[i] is ILayoutSelfController)
                        action(components[i]);

                // Then call the remaining, such as layout groups that change their children, taking their own RectTransform size into account.
                for (int i = 0; i < components.Count; i++)
                    if (!(components[i] is ILayoutSelfController))
                        action(components[i]);

                for (int i = 0; i < rect.childCount; i++)
                    PerformLayoutControl(rect.GetChild(i) as RectTransform, action);
            }

            ListPool<Component>.Release(components);
        }


        // PerformLayoutCalculation需要传入两个参数，一个RectTransform和一个UnityAction<Component>。
        // 函数内部也会对子节点递归调用PerformLayoutCalculation，在完成子孙结点的控制之后，最后再处理自己身上的各ILayoutElement组件
        // 深度优先
        private void PerformLayoutCalculation(RectTransform rect, UnityAction<Component> action)
        {
            if (rect == null)
                return;

            var components = ListPool<Component>.Get();
            rect.GetComponents(typeof(ILayoutElement), components);
            StripDisabledBehavioursFromList(components);

            // 如果当前rect包含有ILayoutElement元素或者本身就是ILayoutGroup，那么需要继续往下递归遍历，否则什么也不做
            if (components.Count > 0  || rect.GetComponent(typeof(ILayoutGroup)))
            {
                // Layout calculations needs to executed bottom up with children being done before their parents,
                // because the parent calculated sizes rely on the sizes of the children.

                for (int i = 0; i < rect.childCount; i++)
                    PerformLayoutCalculation(rect.GetChild(i) as RectTransform, action);

                for (int i = 0; i < components.Count; i++)
                    action(components[i]);
            }

            ListPool<Component>.Release(components);
        }

        /// <summary>
        /// Mark the given RectTransform as needing it's layout to be recalculated during the next layout pass.
        /// 传入的rect开始，自下而上寻找，直到找到根部的LayoutGroup（即该LayoutGroup没有直接的父级LayoutGroup控制）
        /// </summary>
        /// <param name="rect">Rect to rebuild.</param>
        public static void MarkLayoutForRebuild(RectTransform rect)
        {
            if (rect == null || rect.gameObject == null)
                return;

            var comps = ListPool<Component>.Get();
            bool validLayoutGroup = true;
            RectTransform layoutRoot = rect;
            var parent = layoutRoot.parent as RectTransform;

            //双重循环的目的是：找到rect被嵌套的ILayoutGroup根节点，因为rect的layout改变会影响到所有被嵌套的ILayoutGroup的layout计算。
            //所以，如果rect是ILayoutGroup的子孙节点，则需要更新全部的ILayoutGroup的Layout。
            while (validLayoutGroup && !(parent == null || parent.gameObject == null))
            {
                validLayoutGroup = false;
                parent.GetComponents(typeof(ILayoutGroup), comps);

                for (int i = 0; i < comps.Count; ++i)
                {
                    var cur = comps[i];
                    //如果ILayoutGroup是激活状态且可用，则认为是有效的ILayoutGroup
                    if (cur != null && cur is Behaviour && ((Behaviour)cur).isActiveAndEnabled)
                    {
                        validLayoutGroup = true;
                        layoutRoot = parent;
                        break;
                    }
                }

                parent = parent.parent as RectTransform;
            }

            // We know the layout root is valid if it's not the same as the rect,
            // since we checked that above. But if they're the same we still need to check.
            // 如果rect所在GameObject本身并不包含ILayoutController组件，则认为rect无效
            if (layoutRoot == rect && !ValidController(layoutRoot, comps))
            {
                ListPool<Component>.Release(comps);
                return;
            }

            MarkLayoutRootForRebuild(layoutRoot);
            ListPool<Component>.Release(comps);
        }


        private static bool ValidController(RectTransform layoutRoot, List<Component> comps)
        {
            if (layoutRoot == null || layoutRoot.gameObject == null)
                return false;

            layoutRoot.GetComponents(typeof(ILayoutController), comps);
            for (int i = 0; i < comps.Count; ++i)
            {
                var cur = comps[i];
                if (cur != null && cur is Behaviour && ((Behaviour)cur).isActiveAndEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MarkLayoutRootForRebuild(RectTransform controller)
        {
            if (controller == null)
                return;

            var rebuilder = s_Rebuilders.Get();
            rebuilder.Initialize(controller);
            if (!CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(rebuilder))
                s_Rebuilders.Release(rebuilder);
        }

        public void LayoutComplete()
        {
            // 把自己放回池子
            s_Rebuilders.Release(this);
        }

        public void GraphicUpdateComplete()
        {}

        public override int GetHashCode()
        {
            return m_CachedHashFromTransform;
        }

        /// <summary>
        /// Does the passed rebuilder point to the same CanvasElement.
        /// </summary>
        /// <param name="obj">The other object to compare</param>
        /// <returns>Are they equal</returns>
        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == GetHashCode();
        }

        public override string ToString()
        {
            return "(Layout Rebuilder for) " + m_ToRebuild;
        }
    }
}
