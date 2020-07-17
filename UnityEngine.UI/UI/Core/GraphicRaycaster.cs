using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

// https://blog.lujun.co/2019/07/20/unity_event_system_raycasters/

namespace UnityEngine.UI
{
    [AddComponentMenu("Event/Graphic Raycaster")]
    [RequireComponent(typeof(Canvas))]
    /// <summary>
    /// A derived BaseRaycaster to raycast against Graphic elements.
    /// GraphicRaycaster --> 用于处理UIelements，依存于一个Canvas并且会在Canvas内搜寻目标
    /// </summary>
    public class GraphicRaycaster : BaseRaycaster
    {
        protected const int kNoEventMaskSet = -1;

        /// <summary>
        /// Type of raycasters to check against to check for canvas blocking elements.
        /// </summary>
        public enum BlockingObjects
        {
            /// <summary>
            /// Perform no raycasts.
            /// 不执行任何射线检测
            /// </summary>
            None = 0,
            /// <summary>
            /// Perform a 2D raycast check to check for blocking 2D elements
            /// 只检测2D物体
            /// </summary>
            TwoD = 1,
            /// <summary>
            /// Perform a 3D raycast check to check for blocking 3D elements
            /// 只检测3D物体
            /// </summary>
            ThreeD = 2,
            /// <summary>
            /// Perform a 2D and a 3D raycasts to check for blocking 2D and 3D elements.
            /// 同时检测2D和3D
            /// </summary>
            All = 3,
        }

        /// <summary>
        /// Priority of the raycaster based upon sort order.
        /// 不同于 PhysicsRaycaster 和 Physics2DRaycaster 类中直接使用父类的 sortOrderPriority 方法和 renderOrderPriority，
        /// GraphicRaycaster 覆写了这两个方法，并且当 Canvas 的 render mode 设置为 RenderMode.ScreenSpaceOverlay 时，
        /// 上面两个方法分别返回 canvas 的 sortingOrder 以及 rootCanvas 的 renderOrder。
        /// </summary>
        /// <returns>
        /// The sortOrder priority.
        /// </returns>
        public override int sortOrderPriority
        {
            get
            {
                // We need to return the sorting order here as distance will all be 0 for overlay.
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return canvas.sortingOrder;

                return base.sortOrderPriority; //int.MinValue
            }
        }

        /// <summary>
        /// Priority of the raycaster based upon render order.
        /// </summary>
        /// <returns>
        /// The renderOrder priority.
        /// </returns>
        public override int renderOrderPriority
        {
            get
            {
                // We need to return the sorting order here as distance will all be 0 for overlay.
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return canvas.rootCanvas.renderOrder;

                return base.renderOrderPriority; //int.MinValue
            }
        }

        /// <summary>
        /// 射线检测时是否忽略背向的 Graphics
        /// </summary>
        [FormerlySerializedAs("ignoreReversedGraphics")]
        [SerializeField]
        private bool m_IgnoreReversedGraphics = true;


        /// <summary>
        /// 哪些类型的对象会阻挡 Graphic raycasts
        /// </summary>
        [FormerlySerializedAs("blockingObjects")]
        [SerializeField]
        private BlockingObjects m_BlockingObjects = BlockingObjects.None;

        public bool ignoreReversedGraphics { get {return m_IgnoreReversedGraphics; } set { m_IgnoreReversedGraphics = value; } }

        /// <summary>
        /// Type of objects that will be check for to determine if they are blocking block graphic raycasts.
        /// </summary>
        public BlockingObjects blockingObjects { get {return m_BlockingObjects; } set { m_BlockingObjects = value; } }

        // m_BlockingMask是LayerMask用于按层筛选投射目标
        /// <summary>
        /// 哪些 Layer 会阻挡 Graphic raycasts(对 Blocked Objects 指定的对象生效)
        /// </summary>
        [SerializeField]
        protected LayerMask m_BlockingMask = kNoEventMaskSet;

        private Canvas m_Canvas;

        protected GraphicRaycaster()
        {}

        private Canvas canvas
        {
            get
            {
                if (m_Canvas != null)
                    return m_Canvas;

                m_Canvas = GetComponent<Canvas>();
                return m_Canvas;
            }
        }

        [NonSerialized] private List<Graphic> m_RaycastResults = new List<Graphic>();

        /// <summary>
        /// Perform the raycast against the list of graphics associated with the Canvas.
        /// GraphicRaycaster内围绕复写的基类的Raycast方法来展开（注意在GraphicRaycaster内部有另一个名为Raycast的私有静态方法，不要搞混）。
        ///
        /// 整个函数代码很多，但是做的事情很简单，对于给定的PointerEventData，射线投射得出一些投射结果RaycastResult，
        /// 追加到传入的参数List<RaycastResult>之后。
        /// </summary>
        /// <param name="eventData">Current event data</param>
        /// <param name="resultAppendList">List of hit objects to append new results to.</param>
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (canvas == null)
                return;

            // 获取当前 Canvas 下所有的 Graphic(canvasGraphics，这些 Graphics 在进行射线检测的时候会用到)。
            var canvasGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
            if (canvasGraphics == null || canvasGraphics.Count == 0)
                return;

            int displayIndex;
            var currentEventCamera = eventCamera; // Property can call Camera.main, so cache the reference

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || currentEventCamera == null)
                //Canvas.targetDisplay => For Overlay mode, display index on which the UI canvas will appear.
                displayIndex = canvas.targetDisplay;
            else
                displayIndex = currentEventCamera.targetDisplay;

            // Display.RelativeMouseAt => Query relative mouse coordinates.
            // RelativeMouseAt can be used to query relative mouse input coordinates and the screen in which Mouse Input is recorded.
            // This is only valid on the Windows Desktop platforms with Multiple Displays.
            // x, y returns the coordinates in relative space and z returns the screen in which Mouse Input is handled.

            // Display.RelativeMouseAt(...)方法用于获取鼠标的相对位置，在PC平台下可能会有多个Display窗口，返回的z表示的是Display的id。
            // 如果不支持多个Display则返回值的z为0，此时获取到的就是eventData.position；
            var eventPosition = Display.RelativeMouseAt(eventData.position);
            if (eventPosition != Vector3.zero)
            {
                // We support multiple display and display identification based on event position.

                int eventDisplayIndex = (int)eventPosition.z;

                // Discard events that are not part of this display so the user does not interact with multiple displays at once.
                // 当平台支持 MultiDisplay 时，如果用户操作的不是当前的 Display，那么所有的其他 Display 上产生的事件都会被舍弃。
                if (eventDisplayIndex != displayIndex)
                    return;
            }
            else
            {
                // The multiple display system is not supported on all platforms, when it is not supported the returned position
                // will be all zeros so when the returned index is 0 we will default to the event data to be safe.
                eventPosition = eventData.position;

                // We dont really know in which display the event occured. We will process the event assuming it occured in our display.
            }

            // Convert to view space
            // 获取正确的pos：将上一步的屏幕坐标eventPosition转换成ViewPort坐标pos，这里同样也处理了多Display的情况。
            // 获取eventCamera不为null则直接调用相机的坐标转换方法ScreenToViewportPoint
            Vector2 pos;
            if (currentEventCamera == null)
            {
                // Multiple display support only when not the main display. For display 0 the reported
                // resolution is always the desktops resolution since its part of the display API,
                // so we use the standard none multiple display method. (case 741751)
                float w = Screen.width;
                float h = Screen.height;
                if (displayIndex > 0 && displayIndex < Display.displays.Length)
                {
                    w = Display.displays[displayIndex].systemWidth;
                    h = Display.displays[displayIndex].systemHeight;
                }
                pos = new Vector2(eventPosition.x / w, eventPosition.y / h);
            }
            else
                pos = currentEventCamera.ScreenToViewportPoint(eventPosition);

            // If it's outside the camera's viewport, do nothing
            if (pos.x < 0f || pos.x > 1f || pos.y < 0f || pos.y > 1f)
                return;

            // Distance from camera to target point.
            // 获取正确的hitDistance：这里分别处理blockingObjects 为不同的枚举值时的情况（默认值为None）。
            // hitDistance表示相机到投射目标点的距离。后边会根据这个距离值筛选掉一部分Graphic；
            // m_BlockingMask是LayerMask用于按层筛选投射目标；
            // 这里的ReflectionMethodsCache.Singleton.raycast3D这样的方法实际上调用的就是Physics.Raycast(...)方法
            float hitDistance = float.MaxValue;

            Ray ray = new Ray();

            if (currentEventCamera != null)
                // Camera.ScreenPointToRay => Returns a ray going from camera through a screen point.
                // Resulting ray is in world space, starting on the near plane of the camera
                // and going through position's (x,y) pixel coordinates on the screen (position.z is ignored).
                // Screen space is defined in pixels. The bottom-left of the screen is (0,0); the right-top is (pixelWidth -1,pixelHeight -1).
                ray = currentEventCamera.ScreenPointToRay(eventPosition);

            // Canvas renderMode 不为 RenderMode.ScreenSpaceOverlay 并且设置了 blockingObjects，
            // 此时就会 Blocked Objects 和 Blocked Mask 就会生效。
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && blockingObjects != BlockingObjects.None)
            {
                float distanceToClipPlane = 100.0f;

                if (currentEventCamera != null)
                {
                    float projectionDirection = ray.direction.z;
                    distanceToClipPlane = Mathf.Approximately(0.0f, projectionDirection)
                        ? Mathf.Infinity
                        : Mathf.Abs((currentEventCamera.farClipPlane - currentEventCamera.nearClipPlane) / projectionDirection);
                }

                if (blockingObjects == BlockingObjects.ThreeD || blockingObjects == BlockingObjects.All)
                {
                    if (ReflectionMethodsCache.Singleton.raycast3D != null)
                    {
                        // ReflectionMethodsCache.Singleton.raycast3D这样的方法实际上调用的就是Physics.Raycast(...)
                        // raycast3DAll 时指定了射线检测层 m_BlockingMask，这个参数就是自定义设定的 Blocking Mask，
                        // 属于 block mask 的对象在这里就会就行射线检测，并得到最小的一个 hitDistance；
                        // 后面对所有的 Graphics 进行射线检测时，如果检测结果 distance 大于 hitDistance，那么那个结果会被舍弃。
                        // 如此一来，Blocking Mask 就起到了阻挡的作用，属于这个 layer 的所有对象的一旦被射线检测成功并得到 hitDistance，
                        // PhysicsRaycaster 最后的射线检测结果都只会包含这个 hitDistance 距离以内的对象。
                        var hits = ReflectionMethodsCache.Singleton.raycast3DAll(ray, distanceToClipPlane, (int)m_BlockingMask);
                        if (hits.Length > 0)
                            hitDistance = hits[0].distance;
                    }
                }

                if (blockingObjects == BlockingObjects.TwoD || blockingObjects == BlockingObjects.All)
                {
                    if (ReflectionMethodsCache.Singleton.raycast2D != null)
                    {
                        var hits = ReflectionMethodsCache.Singleton.getRayIntersectionAll(ray, distanceToClipPlane, (int)m_BlockingMask);
                        if (hits.Length > 0)
                            hitDistance = hits[0].distance;
                    }
                }
            }

            m_RaycastResults.Clear();

            Raycast(canvas, currentEventCamera, eventPosition, canvasGraphics, m_RaycastResults);

            // 判断对应的GameObject是否有效appendGraphic，即是否继续处理追加该Graphic到射线投射结果的列表
            int totalCount = m_RaycastResults.Count;
            for (var index = 0; index < totalCount; index++)
            {
                var go = m_RaycastResults[index].gameObject;
                bool appendGraphic = true;

                // 如果ignoreReversedGraphics ，则只是追加朝向为正的Graphic
                if (ignoreReversedGraphics)
                {
                    if (currentEventCamera == null)
                    {
                        // If we dont have a camera we know that we should always be facing forward
                        var dir = go.transform.rotation * Vector3.forward;
                        appendGraphic = Vector3.Dot(Vector3.forward, dir) > 0;
                    }
                    else
                    {
                        // If we have a camera compare the direction against the cameras forward.
                        var cameraFoward = currentEventCamera.transform.rotation * Vector3.forward;
                        var dir = go.transform.rotation * Vector3.forward;
                        appendGraphic = Vector3.Dot(cameraFoward, dir) > 0;
                    }
                }

                if (appendGraphic)
                {
                    float distance = 0;
                    Transform trans = go.transform;
                    Vector3 transForward = trans.forward;

                    if (currentEventCamera == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        distance = 0;
                    else
                    {
                        // http://geomalgorithms.com/a06-_intersect-2.html
                        // 计算 Graphic 和 Camera 之间的向量在 Graphic 正方向上的投影以及计算射线方向在 Graphic 正方向上的投影，
                        // 两者相除就得到最终的 distance。
                        distance = (Vector3.Dot(transForward, trans.position - ray.origin) / Vector3.Dot(transForward, ray.direction));

                        // Check to see if the go is behind the camera.
                        if (distance < 0)
                            continue;
                    }

                    if (distance >= hitDistance)
                        continue;

                    var castResult = new RaycastResult
                    {
                        gameObject = go,
                        module = this,
                        distance = distance,
                        screenPosition = eventPosition,
                        index = resultAppendList.Count,
                        depth = m_RaycastResults[index].depth,
                        sortingLayer = canvas.sortingLayerID,
                        sortingOrder = canvas.sortingOrder,
                        worldPosition = ray.origin + ray.direction * distance,
                        worldNormal = -transForward
                    };
                    resultAppendList.Add(castResult);
                }
            }
        }

        /// <summary>
        /// The camera that will generate rays for this raycaster.
        /// </summary>
        /// <returns>
        /// - Null if Camera mode is ScreenSpaceOverlay or ScreenSpaceCamera and has no camera.
        /// - canvas.worldCanvas if not null
        /// - Camera.main.
        /// </returns>
        public override Camera eventCamera
        {
            get
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null))
                    return null;

                return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
        }

        /// <summary>
        /// Perform a raycast into the screen and collect all graphics underneath it.
        /// 首先是做了一些是否有效的判断，然后会在层级结构中自下而上地遍历，直到没有更多的父级节点或者有overrideSorting的Canvas为止。
        /// 遍历过程中，如果遇到有ICanvasRaycastFilter（Image实现有此接口）和CanvasGroup则要根据二者的实现及参数做出一些判断，
        /// 如果都通过了检测，则认为这次投射有效返回true。
        /// </summary>
        [NonSerialized] static readonly List<Graphic> s_SortedGraphics = new List<Graphic>();
        private static void Raycast(Canvas canvas, Camera eventCamera, Vector2 pointerPosition, IList<Graphic> foundGraphics, List<Graphic> results)
        {
            // Necessary for the event system
            int totalCount = foundGraphics.Count;
            for (int i = 0; i < totalCount; ++i)
            {
                Graphic graphic = foundGraphics[i];

                // -1 means it hasn't been processed by the canvas, which means it isn't actually drawn
                // depth 为 -1 说明没有被 canvas 处理(未被绘制)
                // raycastTarget 为 false 说明当前 graphic 不需要被射线检测
                // graphic.canvasRenderer.cull 为 true，忽略当前 graphic 的 CanvasRender 渲染的物体
                if (graphic.depth == -1 || !graphic.raycastTarget || graphic.canvasRenderer.cull)
                    continue;

                // pointer位置不在graphic中
                // 从指定的 eventCamera 计算 pointerPosition 是否在 graphic 的 Rectangle 区域内
                if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, pointerPosition, eventCamera))
                    continue;

                // graph不在eventCamera的裁剪空间中
                if (eventCamera != null && eventCamera.WorldToScreenPoint(graphic.rectTransform.position).z > eventCamera.farClipPlane)
                    continue;

                // 执行射线检测
                if (graphic.Raycast(pointerPosition, eventCamera))
                {
                    s_SortedGraphics.Add(graphic);
                }
            }

            s_SortedGraphics.Sort((g1, g2) => g2.depth.CompareTo(g1.depth));
            totalCount = s_SortedGraphics.Count;
            for (int i = 0; i < totalCount; ++i)
                results.Add(s_SortedGraphics[i]);

            s_SortedGraphics.Clear();
        }
    }
}
