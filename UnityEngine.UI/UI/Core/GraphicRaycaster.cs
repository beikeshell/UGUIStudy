using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace UnityEngine.UI
{
    [AddComponentMenu("Event/Graphic Raycaster")]
    [RequireComponent(typeof(Canvas))]
    /// <summary>
    /// A derived BaseRaycaster to raycast against Graphic elements.
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
            /// </summary>
            None = 0,
            /// <summary>
            /// Perform a 2D raycast check to check for blocking 2D elements
            /// </summary>
            TwoD = 1,
            /// <summary>
            /// Perform a 3D raycast check to check for blocking 3D elements
            /// </summary>
            ThreeD = 2,
            /// <summary>
            /// Perform a 2D and a 3D raycasts to check for blocking 2D and 3D elements.
            /// </summary>
            All = 3,
        }

        /// <summary>
        /// Priority of the raycaster based upon sort order.
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

                return base.sortOrderPriority;
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

                return base.renderOrderPriority;
            }
        }

        [FormerlySerializedAs("ignoreReversedGraphics")]
        [SerializeField]
        private bool m_IgnoreReversedGraphics = true;
        [FormerlySerializedAs("blockingObjects")]
        [SerializeField]
        private BlockingObjects m_BlockingObjects = BlockingObjects.None;

        public bool ignoreReversedGraphics { get {return m_IgnoreReversedGraphics; } set { m_IgnoreReversedGraphics = value; } }

        /// <summary>
        /// Type of objects that will be check for to determine if they are blocking block graphic raycasts.
        /// </summary>
        public BlockingObjects blockingObjects { get {return m_BlockingObjects; } set { m_BlockingObjects = value; } }

        // m_BlockingMask是LayerMask用于按层筛选投射目标
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
        /// </summary>
        /// <param name="eventData">Current event data</param>
        /// <param name="resultAppendList">List of hit objects to append new results to.</param>
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (canvas == null)
                return;

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
        /// 首先是做了一些是否有效的判断，然后会在层级结构中自下而上地遍历，直到没有更多的父级节点或者由overrideSorting的Canvas为止。
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
                if (graphic.depth == -1 || !graphic.raycastTarget || graphic.canvasRenderer.cull)
                    continue;

                // pointer位置不在graphic中
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
