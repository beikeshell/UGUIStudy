using System;
#if UNITY_EDITOR
using System.Reflection;
#endif
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI.CoroutineTween;

namespace UnityEngine.UI
{
    /// <summary>
    /// Base class for all UI components that should be derived from when creating new Graphic types.
    /// 1、不支持同一个GameObject上挂载多个此组件
    /// 2、需要CanvasRenderer组件支持
    /// 3、需要RectTransform组件支持
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    /// <summary>
    ///   Base class for all visual UI Component.
    ///   When creating visual UI components you should inherit from this class.
    /// </summary>
    /// <example>
    /// Below is a simple example that draws a colored quad inside the Rect Transform area.
    /// <code>
    /// using UnityEngine;
    /// using UnityEngine.UI;
    ///
    /// [ExecuteInEditMode]
    /// public class SimpleImage : Graphic
    /// {
    ///     protected override void OnPopulateMesh(VertexHelper vh)
    ///     {
    ///         Vector2 corner1 = Vector2.zero;
    ///         Vector2 corner2 = Vector2.zero;
    ///
    ///         corner1.x = 0f;
    ///         corner1.y = 0f;
    ///         corner2.x = 1f;
    ///         corner2.y = 1f;
    ///
    ///         corner1.x -= rectTransform.pivot.x;
    ///         corner1.y -= rectTransform.pivot.y;
    ///         corner2.x -= rectTransform.pivot.x;
    ///         corner2.y -= rectTransform.pivot.y;
    ///
    ///         corner1.x *= rectTransform.rect.width;
    ///         corner1.y *= rectTransform.rect.height;
    ///         corner2.x *= rectTransform.rect.width;
    ///         corner2.y *= rectTransform.rect.height;
    ///
    ///         vh.Clear();
    ///
    ///         UIVertex vert = UIVertex.simpleVert;
    ///
    ///         vert.position = new Vector2(corner1.x, corner1.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vert.position = new Vector2(corner1.x, corner2.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vert.position = new Vector2(corner2.x, corner2.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vert.position = new Vector2(corner2.x, corner1.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vh.AddTriangle(0, 1, 2);
    ///         vh.AddTriangle(2, 3, 0);
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class Graphic
        : UIBehaviour,
          ICanvasElement
    {
        static protected Material s_DefaultUI = null;
        static protected Texture2D s_WhiteTexture = null;

        /// <summary>
        /// Default material used to draw UI elements if no explicit material was specified.
        /// </summary>

        static public Material defaultGraphicMaterial
        {
            get
            {
                if (s_DefaultUI == null)
                    s_DefaultUI = Canvas.GetDefaultCanvasMaterial();
                return s_DefaultUI;
            }
        }

        // Cached and saved values
        [FormerlySerializedAs("m_Mat")]
        [SerializeField] protected Material m_Material;

        [SerializeField] private Color m_Color = Color.white;

        [NonSerialized] protected bool m_SkipLayoutUpdate;
        [NonSerialized] protected bool m_SkipMaterialUpdate;

        /// <summary>
        /// Base color of the Graphic.
        /// </summary>
        /// <remarks>
        /// The builtin UI Components use this as their vertex color.
        /// Use this to fetch or change the Color of visual UI elements, such as an Image.
        /// </remarks>
        /// <example>
        /// <code>
        /// //Place this script on a GameObject with a Graphic component attached e.g. a visual UI element (Image).
        ///
        /// using UnityEngine;
        /// using UnityEngine.UI;
        ///
        /// public class Example : MonoBehaviour
        /// {
        ///     Graphic m_Graphic;
        ///     Color m_MyColor;
        ///
        ///     void Start()
        ///     {
        ///         //Fetch the Graphic from the GameObject
        ///         m_Graphic = GetComponent<Graphic>();
        ///         //Create a new Color that starts as red
        ///         m_MyColor = Color.red;
        ///         //Change the Graphic Color to the new Color
        ///         m_Graphic.color = m_MyColor;
        ///     }
        ///
        ///     // Update is called once per frame
        ///     void Update()
        ///     {
        ///         //When the mouse button is clicked, change the Graphic Color
        ///         if (Input.GetKey(KeyCode.Mouse0))
        ///         {
        ///             //Change the Color over time between blue and red while the mouse button is pressed
        ///             m_MyColor = Color.Lerp(Color.red, Color.blue, Mathf.PingPong(Time.time, 1));
        ///         }
        ///         //Change the Graphic Color to the new Color
        ///         m_Graphic.color = m_MyColor;
        ///     }
        /// }
        /// </code>
        /// </example>
        public virtual Color color { get { return m_Color; } set { if (SetPropertyUtility.SetColor(ref m_Color, value)) SetVerticesDirty(); } }

        [SerializeField] private bool m_RaycastTarget = true;

        /// <summary>
        /// Should this graphic be considered a target for raycasting?
        /// </summary>
        public virtual bool raycastTarget { get { return m_RaycastTarget; } set { m_RaycastTarget = value; } }

        [NonSerialized] private RectTransform m_RectTransform;
        [NonSerialized] private CanvasRenderer m_CanvasRenderer;
        [NonSerialized] private Canvas m_Canvas;

        [NonSerialized] private bool m_VertsDirty;
        [NonSerialized] private bool m_MaterialDirty;

        [NonSerialized] protected UnityAction m_OnDirtyLayoutCallback;
        [NonSerialized] protected UnityAction m_OnDirtyVertsCallback;
        [NonSerialized] protected UnityAction m_OnDirtyMaterialCallback;

        [NonSerialized] protected static Mesh s_Mesh;
        [NonSerialized] private static readonly VertexHelper s_VertexHelper = new VertexHelper();

        [NonSerialized] protected Mesh m_CachedMesh;
        [NonSerialized] protected Vector2[] m_CachedUvs;
        // Tween controls for the Graphic
        [NonSerialized]
        private readonly TweenRunner<ColorTween> m_ColorTweenRunner;

        protected bool useLegacyMeshGeneration { get; set; }

        // Called by Unity prior to deserialization,
        // should not be called by users
        protected Graphic()
        {
            if (m_ColorTweenRunner == null)
                m_ColorTweenRunner = new TweenRunner<ColorTween>();
            m_ColorTweenRunner.Init(this);
            useLegacyMeshGeneration = true;
        }

        /// <summary>
        /// Set all properties of the Graphic dirty and needing rebuilt.
        /// Dirties Layout, Vertices, and Materials.
        /// 设置Graphic所有属性（包括layout， vertices， materials）为脏标记，表示自己需要被重建
        /// </summary>
        public virtual void SetAllDirty()
        {
            // Optimization: Graphic layout doesn't need recalculation if
            // the underlying(底层的) Sprite is the same size with the same texture.
            // (e.g. Sprite sheet texture animation)

            //m_SkipLayoutUpdate 和 m_SkipMaterialUpdate标记会在子类Image中更新，更新的时机为重新给sprite属性赋值时
            if (m_SkipLayoutUpdate)
            {
                m_SkipLayoutUpdate = false;
            }
            else
            {
                SetLayoutDirty();
            }

            if (m_SkipMaterialUpdate)
            {
                m_SkipMaterialUpdate = false;
            }
            else
            {
                SetMaterialDirty();
            }

            SetVerticesDirty();
        }

        /// <summary>
        /// Mark the layout as dirty and needing rebuilt.
        /// </summary>
        /// <remarks>
        /// Send a OnDirtyLayoutCallback notification if any elements are registered. See RegisterDirtyLayoutCallback
        /// </remarks>
        public virtual void SetLayoutDirty()
        {
            if (!IsActive())
                return;

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);

            m_OnDirtyLayoutCallback?.Invoke();
        }

        /// <summary>
        /// Mark the vertices as dirty and needing rebuilt.
        /// </summary>
        /// <remarks>
        /// Send a OnDirtyVertsCallback notification if any elements are registered. See RegisterDirtyVerticesCallback
        /// </remarks>
        public virtual void SetVerticesDirty()
        {
            if (!IsActive())
                return;

            m_VertsDirty = true;
            CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);

            if (m_OnDirtyVertsCallback != null)
                m_OnDirtyVertsCallback();
        }

        /// <summary>
        /// Mark the material as dirty and needing rebuilt.
        /// </summary>
        /// <remarks>
        /// Send a OnDirtyMaterialCallback notification if any elements are registered. See RegisterDirtyMaterialCallback
        /// </remarks>
        public virtual void SetMaterialDirty()
        {
            if (!IsActive())
                return;

            m_MaterialDirty = true;
            CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);

            if (m_OnDirtyMaterialCallback != null)
                m_OnDirtyMaterialCallback();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            if (gameObject.activeInHierarchy)
            {
                // prevent double dirtying...
                if (CanvasUpdateRegistry.IsRebuildingLayout())
                    SetVerticesDirty();
                else
                {
                    SetVerticesDirty();
                    SetLayoutDirty();
                }
            }
        }

        protected override void OnBeforeTransformParentChanged()
        {
            GraphicRegistry.UnregisterGraphicForCanvas(canvas, this);
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();

            m_Canvas = null;

            if (!IsActive())
                return;

            CacheCanvas();
            GraphicRegistry.RegisterGraphicForCanvas(canvas, this);
            SetAllDirty();
        }

        /// <summary>
        /// Absolute depth of the graphic, used by rendering and events -- lowest to highest.
        /// </summary>
        /// <example>
        /// The depth is relative to the first root canvas.
        ///
        /// Canvas
        ///  Graphic - 1
        ///  Graphic - 2
        ///  Nested Canvas
        ///     Graphic - 3
        ///     Graphic - 4
        ///  Graphic - 5
        ///
        /// This value is used to determine draw and event ordering.
        /// </example>
        public int depth { get { return canvasRenderer.absoluteDepth; } }

        /// <summary>
        /// The RectTransform component used by the Graphic. Cached for speed.
        /// </summary>
        public RectTransform rectTransform
        {
            get
            {
                // The RectTransform is a required component that must not be destroyed.
                // Based on this assumption, a null-reference check is sufficient.
                if (ReferenceEquals(m_RectTransform, null))
                {
                    m_RectTransform = GetComponent<RectTransform>();
                }
                return m_RectTransform;
            }
        }

        /// <summary>
        /// A reference to the Canvas this Graphic is rendering to.
        /// </summary>
        /// <remarks>
        /// In the situation where the Graphic is used in a hierarchy with multiple Canvases,
        /// the Canvas closest to the root will be used.
        /// </remarks>
        public Canvas canvas
        {
            get
            {
                if (m_Canvas == null)
                    CacheCanvas();
                return m_Canvas;
            }
        }

        private void CacheCanvas()
        {
            var list = ListPool<Canvas>.Get();
            gameObject.GetComponentsInParent(false, list);
            if (list.Count > 0)
            {
                // Find the first active and enabled canvas.
                for (int i = 0; i < list.Count; ++i)
                {
                    if (list[i].isActiveAndEnabled)
                    {
                        m_Canvas = list[i];
                        break;
                    }
                }
            }
            else
            {
                m_Canvas = null;
            }

            ListPool<Canvas>.Release(list);
        }

        /// <summary>
        /// A reference to the CanvasRenderer populated by this Graphic.
        /// </summary>
        public CanvasRenderer canvasRenderer
        {
            get
            {
                // The CanvasRenderer is a required component that must not be destroyed. Based on this assumption, a
                // null-reference check is sufficient.
                if (ReferenceEquals(m_CanvasRenderer, null))
                {
                    m_CanvasRenderer = GetComponent<CanvasRenderer>();
                }
                return m_CanvasRenderer;
            }
        }

        /// <summary>
        /// Returns the default material for the graphic.
        /// </summary>
        public virtual Material defaultMaterial
        {
            get { return defaultGraphicMaterial; }
        }

        /// <summary>
        /// The Material set by the user
        /// </summary>
        public virtual Material material
        {
            get
            {
                return (m_Material != null) ? m_Material : defaultMaterial;
            }
            set
            {
                if (m_Material == value)
                    return;

                m_Material = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// The material that will be sent for Rendering (Read only).
        /// </summary>
        /// <remarks>
        /// This is the material that actually gets sent to the CanvasRenderer.
        /// By default it's the same as [[Graphic.material]].
        /// When extending Graphic you can override this to send a different material to the CanvasRenderer
        /// than the one set by Graphic.material.
        /// This is useful if you want to modify the user set material in a non destructive manner.
        /// </remarks>
        public virtual Material materialForRendering
        {
            get
            {
                var components = ListPool<Component>.Get();
                GetComponents(typeof(IMaterialModifier), components);

                var currentMat = material;
                for (var i = 0; i < components.Count; i++)
                    currentMat = (components[i] as IMaterialModifier).GetModifiedMaterial(currentMat);
                ListPool<Component>.Release(components);
                return currentMat;
            }
        }

        /// <summary>
        /// The graphic's texture. (Read Only).
        /// </summary>
        /// <remarks>
        /// This is the Texture that gets passed to the CanvasRenderer, Material and then Shader _MainTex.
        ///
        /// When implementing your own Graphic you can override this to control which texture goes through the UI Rendering pipeline.
        ///
        /// Bear in mind that Unity tries to batch UI elements together to improve performance,
        /// so its ideal to work with atlas to reduce the number of draw calls.
        /// </remarks>
        public virtual Texture mainTexture
        {
            get
            {
                return s_WhiteTexture;
            }
        }

        /// <summary>
        /// Mark the Graphic and the canvas as having been changed.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            CacheCanvas();
            GraphicRegistry.RegisterGraphicForCanvas(canvas, this);

#if UNITY_EDITOR
            GraphicRebuildTracker.TrackGraphic(this);
#endif
            if (s_WhiteTexture == null)
                s_WhiteTexture = Texture2D.whiteTexture;

            SetAllDirty();
        }

        /// <summary>
        /// Clear references.
        /// </summary>
        protected override void OnDisable()
        {
#if UNITY_EDITOR
            GraphicRebuildTracker.UnTrackGraphic(this);
#endif
            GraphicRegistry.UnregisterGraphicForCanvas(canvas, this);
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

            if (canvasRenderer != null)
                canvasRenderer.Clear();

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (m_CachedMesh)
                Destroy(m_CachedMesh);
            m_CachedMesh = null;

            base.OnDestroy();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            // Use m_Cavas so we dont auto call CacheCanvas
            Canvas currentCanvas = m_Canvas;

            // Clear the cached canvas. Will be fetched below if active.
            m_Canvas = null;

            if (!IsActive())
                return;

            CacheCanvas();

            if (currentCanvas != m_Canvas)
            {
                GraphicRegistry.UnregisterGraphicForCanvas(currentCanvas, this);

                // Only register if we are active and enabled as OnCanvasHierarchyChanged can get called
                // during object destruction and we dont want to register ourself and then become null.
                if (IsActive())
                    GraphicRegistry.RegisterGraphicForCanvas(canvas, this);
            }
        }

        /// <summary>
        /// This method must be called when <c>CanvasRenderer.cull</c> is modified.
        /// </summary>
        /// <remarks>
        /// This can be used to perform operations that were previously skipped because the <c>Graphic</c> was culled.
        /// </remarks>
        public virtual void OnCullingChanged()
        {
            if (!canvasRenderer.cull && (m_VertsDirty || m_MaterialDirty))
            {
                /// When we were culled, we potentially skipped calls to <c>Rebuild</c>.
                CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
            }
        }

        /// <summary>
        /// Rebuilds the graphic geometry and its material on the PreRender cycle.
        /// </summary>
        /// <param name="update">The current step of the rendering CanvasUpdate cycle.</param>
        /// <remarks>
        /// See CanvasUpdateRegistry for more details on the canvas update cycle.
        /// </remarks>
        public virtual void Rebuild(CanvasUpdate update)
        {
            if (canvasRenderer == null || canvasRenderer.cull)
                return;

            switch (update)
            {
                case CanvasUpdate.PreRender:
                    if (m_VertsDirty)
                    {
                        UpdateGeometry();
                        m_VertsDirty = false;
                    }
                    if (m_MaterialDirty)
                    {
                        UpdateMaterial();
                        m_MaterialDirty = false;
                    }
                    break;
            }
        }

        public virtual void LayoutComplete()
        {}

        public virtual void GraphicUpdateComplete()
        {}

        /// <summary>
        /// Call to update the Material of the graphic onto the CanvasRenderer.
        /// </summary>
        protected virtual void UpdateMaterial()
        {
            if (!IsActive())
                return;

            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(materialForRendering, 0);
            canvasRenderer.SetTexture(mainTexture);
        }

        /// <summary>
        /// Call to update the geometry of the Graphic onto the CanvasRenderer.
        /// </summary>
        protected virtual void UpdateGeometry()
        {
            if (useLegacyMeshGeneration)
            {
                DoLegacyMeshGeneration();
            }
            else
            {
                DoMeshGeneration();
            }
        }

        private void DoMeshGeneration()
        {
            if (rectTransform != null && rectTransform.rect.width >= 0 && rectTransform.rect.height >= 0)
                OnPopulateMesh(s_VertexHelper);
            else
                s_VertexHelper.Clear(); // clear the vertex helper so invalid graphics dont draw.

            var components = ListPool<Component>.Get();
            GetComponents(typeof(IMeshModifier), components);

            for (var i = 0; i < components.Count; i++)
                ((IMeshModifier)components[i]).ModifyMesh(s_VertexHelper);

            ListPool<Component>.Release(components);

            s_VertexHelper.FillMesh(workerMesh);
            canvasRenderer.SetMesh(workerMesh);
        }

        private void DoLegacyMeshGeneration()
        {
            if (rectTransform != null && rectTransform.rect.width >= 0 && rectTransform.rect.height >= 0)
            {
#pragma warning disable 618
                OnPopulateMesh(workerMesh);
#pragma warning restore 618
            }
            else
            {
                workerMesh.Clear();
            }

            var components = ListPool<Component>.Get();
            GetComponents(typeof(IMeshModifier), components);

            for (var i = 0; i < components.Count; i++)
            {
#pragma warning disable 618
                ((IMeshModifier)components[i]).ModifyMesh(workerMesh);
#pragma warning restore 618
            }

            ListPool<Component>.Release(components);
            canvasRenderer.SetMesh(workerMesh);
        }

        protected static Mesh workerMesh
        {
            get
            {
                if (s_Mesh == null)
                {
                    s_Mesh = new Mesh();
                    s_Mesh.name = "Shared UI Mesh";
                    s_Mesh.hideFlags = HideFlags.HideAndDontSave;
                }
                return s_Mesh;
            }
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use OnPopulateMesh instead.", true)]
        protected virtual void OnFillVBO(System.Collections.Generic.List<UIVertex> vbo) {}

        [Obsolete("Use OnPopulateMesh(VertexHelper vh) instead.", false)]
        /// <summary>
        /// Callback function when a UI element needs to generate vertices. Fills the vertex buffer data.
        /// </summary>
        /// <param name="m">Mesh to populate with UI data.</param>
        /// <remarks>
        /// Used by Text, UI.Image, and RawImage for example to generate vertices specific to their use case.
        /// </remarks>
        protected virtual void OnPopulateMesh(Mesh m)
        {
            OnPopulateMesh(s_VertexHelper);
            s_VertexHelper.FillMesh(m);
        }

        /// <summary>
        /// Callback function when a UI element needs to generate vertices. Fills the vertex buffer data.
        /// </summary>
        /// <param name="vh">VertexHelper utility.</param>
        /// <remarks>
        /// Used by Text, UI.Image, and RawImage for example to generate vertices specific to their use case.
        /// </remarks>
        protected virtual void OnPopulateMesh(VertexHelper vh)
        {
            var r = GetPixelAdjustedRect();
            var v = new Vector4(r.x, r.y, r.x + r.width, r.y + r.height);

            Color32 color32 = color;
            vh.Clear();
            vh.AddVert(new Vector3(v.x, v.y), color32, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(v.x, v.w), color32, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(v.z, v.w), color32, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(v.z, v.y), color32, new Vector2(1f, 0f));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only callback that is issued by Unity if a rebuild of the Graphic is required.
        /// Currently sent when an asset is reimported.
        /// </summary>
        public virtual void OnRebuildRequested()
        {
            // when rebuild is requested we need to rebuild all the graphics /
            // and associated components... The correct way to do this is by
            // calling OnValidate... Because MB's don't have a common base class
            // we do this via reflection. It's nasty and ugly... Editor only.
            var mbs = gameObject.GetComponents<MonoBehaviour>();
            foreach (var mb in mbs)
            {
                if (mb == null)
                    continue;
                var methodInfo = mb.GetType().GetMethod("OnValidate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (methodInfo != null)
                    methodInfo.Invoke(mb, null);
            }
        }

        protected override void Reset()
        {
            SetAllDirty();
        }

#endif

        // Call from unity if animation properties have changed

        protected override void OnDidApplyAnimationProperties()
        {
            SetAllDirty();
        }

        /// <summary>
        /// Make the Graphic have the native size of its content.
        /// </summary>
        public virtual void SetNativeSize() {}

        /// <summary>
        /// When a Graphic Raycaster is raycasting into the scene it does two things.
        /// First it filters the elements using their RectTransform rect.
        /// Then it uses this Raycast function to determine the elements hit by the raycast.
        ///
        /// 对 Canvas 下所有的 graphic 遍历，满足条件则进行射线检测。Graphic 射线检测过程如下:
        /// 整个检测过程是在一个循环中实现的，从当前 Graphic 所在节点开始往祖先节点不断递归，
        /// 直至向上再没有节点或者节点绑定的组件中有被射线检测出不合法而返回。
        /// 对于节点对象，首先获取其绑定的所有组件，依次遍历判断组件:
        /// ->若组件不是 Canvas 或者是 其 Canvas 但是其属性 overrideSorting 为 false，此时的检测过程如下:
        ///   判断组件是否是 ICanvasRaycastFilter，如不是则继续下一个组件判断；
        ///   若是则调用 ICanvasRaycastFilter 的 IsRaycastLocationValid 方法判断事件发生位置相对这个节点对象是否是合法的，
        ///   如果不合法直接跳出循环和遍历，Raycast 返回 false，表示用于检测的 Graphic 不需要接收此事件；
        ///   若所有的组件检测都合法且 IsRaycastLocationValid 都则返回 true，则继续遍历下一个父节点对象。
        ///     上一步的检测过程中，当节点遍历完成还没有返回那么就 Raycast 方法返回 true 表示用于检测的 Graphic 可以作为事件接收对象。
        ///     另一种遍历完成的条件为当遍历到某个节点的某个组件，这个组件是 Canvas 并且其 overrideSorting 为 true，
        ///     在这种情况下会将 continueTraversal 局部变量设置为 false 表示到这个节点遍历就可以完成了；
        ///     当前这个节点的判断计算过程同第一步中相同: 在当前节点绑定的一系列实现了 ICanvasRaycastFilter 接口的组件上调用
        ///     IsRaycastLocationValid 方法判断事件发生位置相对这个 Graphic 是否是合法的，如果不合法 Raycast 方法直接返回 false，
        ///     表示当前 Graphic 不需要接收此事件，否则所有的组件检测都合法返回 true，表示当前 Graphic 需要作为事件接收对象。
        ///     在上面对实现了 ICanvasRaycastFilter 接口的组件判断计算过程中，还会判断组件是否是 CanvasGroup。
        ///     若是 CanvasGroup 且设置了 ignoreParentGroups 为 false，那么会调用 IsRaycastLocationValid 计算判断；
        ///     若是 CanvasGroup 但是设置了 ignoreParentGroups 为 true，那么依旧会调用 IsRaycastLocationValid 计算判断一次，
        ///     但是对接下来后面所有的 CanvasGroup 组件将不会调用 IsRaycastLocationValid 方法检测(忽略这些父 CanvasGrpup 的判断)；
        ///     如果不是 CanvasGroup，直接调用 IsRaycastLocationValid 方法判断事件发生位置相对这个 Graphic 是否是合法的。
        /// 从整个 Graphic.Raycast 检测过程可以看出，检测是自当前 graphic 所在节点开始，
        /// 一旦检测到某个节点添加实现了 ICanvasRaycastFilter 接口且 IsRaycastLocationValid 方法返回 false
        /// 则此 graphic 检测失败并结束检测；否则还会继续向上递归检测父节点，当所有节点(绑定了 Canvas 组件并设置了
        /// Canvas.overrideSorting 为 true的节点会截止此次检测)都射线检测成功，则此次 Graphic.Raycast 成功。
        /// </summary>
        /// <param name="sp">Screen point being tested</param>
        /// <param name="eventCamera">Camera that is being used for the testing.</param>
        /// <returns>True if the provided point is a valid location for GraphicRaycaster raycasts.</returns>
        public virtual bool Raycast(Vector2 sp, Camera eventCamera)
        {
            if (!isActiveAndEnabled)
                return false;

            var t = transform;
            var components = ListPool<Component>.Get();

            bool ignoreParentGroups = false;
            bool continueTraversal = true;

            while (t != null)
            {
                t.GetComponents(components);
                for (var i = 0; i < components.Count; i++)
                {
                    var canvas = components[i] as Canvas;
                    if (canvas != null && canvas.overrideSorting)
                        continueTraversal = false;

                    var filter = components[i] as ICanvasRaycastFilter;

                    if (filter == null)
                        continue;

                    var raycastValid = true;

                    var group = components[i] as CanvasGroup;
                    if (group != null)
                    {
                        if (ignoreParentGroups == false && group.ignoreParentGroups)
                        {
                            ignoreParentGroups = true;
                            raycastValid = filter.IsRaycastLocationValid(sp, eventCamera);
                        }
                        else if (!ignoreParentGroups)
                            raycastValid = filter.IsRaycastLocationValid(sp, eventCamera);
                    }
                    else
                    {
                        raycastValid = filter.IsRaycastLocationValid(sp, eventCamera);
                    }

                    if (!raycastValid)
                    {
                        ListPool<Component>.Release(components);
                        return false;
                    }
                }
                t = continueTraversal ? t.parent : null;
            }
            ListPool<Component>.Release(components);
            return true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetAllDirty();
        }

#endif

        ///<summary>
        ///Adjusts the given pixel to be pixel perfect.
        ///</summary>
        ///<param name="point">Local space point.</param>
        ///<returns>Pixel perfect adjusted point.</returns>
        ///<remarks>
        ///Note: This is only accurate if the Graphic root Canvas is in Screen Space.
        ///</remarks>
        public Vector2 PixelAdjustPoint(Vector2 point)
        {
            if (!canvas || canvas.renderMode == RenderMode.WorldSpace || canvas.scaleFactor == 0.0f || !canvas.pixelPerfect)
                return point;
            else
            {
                return RectTransformUtility.PixelAdjustPoint(point, transform, canvas);
            }
        }

        /// <summary>
        /// Returns a pixel perfect Rect closest to the Graphic RectTransform.
        /// </summary>
        /// <remarks>
        /// Note: This is only accurate if the Graphic root Canvas is in Screen Space.
        /// </remarks>
        /// <returns>A Pixel perfect Rect.</returns>
        public Rect GetPixelAdjustedRect()
        {
            if (!canvas || canvas.renderMode == RenderMode.WorldSpace || canvas.scaleFactor == 0.0f || !canvas.pixelPerfect)
                return rectTransform.rect;
            else
                return RectTransformUtility.PixelAdjustRect(rectTransform, canvas);
        }

        ///<summary>
        ///Tweens the CanvasRenderer color associated with this Graphic.
        ///</summary>
        ///<param name="targetColor">Target color.</param>
        ///<param name="duration">Tween duration.</param>
        ///<param name="ignoreTimeScale">Should ignore Time.scale?</param>
        ///<param name="useAlpha">Should also Tween the alpha channel?</param>
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
        {
            CrossFadeColor(targetColor, duration, ignoreTimeScale, useAlpha, true);
        }

        ///<summary>
        ///Tweens the CanvasRenderer color associated with this Graphic.
        /// 指定了目标颜色和用时等参数，在指定的时间内变化到目标颜色。
        /// 内部使用到一个ColorTween的类，它是用协程实现的一个逐帧更新的颜色动画，指定初始和目标颜色，变化时间，
        /// 是否忽略TimeScale，变化模式（RGB变化/Alpha变化）。
        ///</summary>
        ///<param name="targetColor">Target color.</param>
        ///<param name="duration">Tween duration.</param>
        ///<param name="ignoreTimeScale">Should ignore Time.scale?</param>
        ///<param name="useAlpha">Should also Tween the alpha channel?</param>
        /// <param name="useRGB">Should the color or the alpha be used to tween</param>
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB)
        {
            if (canvasRenderer == null || (!useRGB && !useAlpha))
                return;

            Color currentColor = canvasRenderer.GetColor();
            if (currentColor.Equals(targetColor))
            {
                m_ColorTweenRunner.StopTween();
                return;
            }

            ColorTween.ColorTweenMode mode = (useRGB && useAlpha ?
                ColorTween.ColorTweenMode.All :
                (useRGB ? ColorTween.ColorTweenMode.RGB : ColorTween.ColorTweenMode.Alpha));

            var colorTween = new ColorTween {duration = duration, startColor = canvasRenderer.GetColor(), targetColor = targetColor};
            colorTween.AddOnChangedCallback(canvasRenderer.SetColor);
            colorTween.ignoreTimeScale = ignoreTimeScale;
            colorTween.tweenMode = mode;
            m_ColorTweenRunner.StartTween(colorTween);
        }

        static private Color CreateColorFromAlpha(float alpha)
        {
            var alphaColor = Color.black;
            alphaColor.a = alpha;
            return alphaColor;
        }

        ///<summary>
        ///Tweens the alpha of the CanvasRenderer color associated with this Graphic.
        ///</summary>
        ///<param name="alpha">Target alpha.</param>
        ///<param name="duration">Duration of the tween in seconds.</param>
        ///<param name="ignoreTimeScale">Should ignore [[Time.scale]]?</param>
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
        {
            CrossFadeColor(CreateColorFromAlpha(alpha), duration, ignoreTimeScale, true, false);
        }

        /// <summary>
        /// Add a listener to receive notification when the graphics layout is dirtied.
        /// </summary>
        /// <param name="action">The method to call when invoked.</param>
        public void RegisterDirtyLayoutCallback(UnityAction action)
        {
            m_OnDirtyLayoutCallback += action;
        }

        /// <summary>
        /// Remove a listener from receiving notifications when the graphics layout are dirtied
        /// </summary>
        /// <param name="action">The method to call when invoked.</param>
        public void UnregisterDirtyLayoutCallback(UnityAction action)
        {
            m_OnDirtyLayoutCallback -= action;
        }

        /// <summary>
        /// Add a listener to receive notification when the graphics vertices are dirtied.
        /// </summary>
        /// <param name="action">The method to call when invoked.</param>
        public void RegisterDirtyVerticesCallback(UnityAction action)
        {
            m_OnDirtyVertsCallback += action;
        }

        /// <summary>
        /// Remove a listener from receiving notifications when the graphics vertices are dirtied
        /// </summary>
        /// <param name="action">The method to call when invoked.</param>
        public void UnregisterDirtyVerticesCallback(UnityAction action)
        {
            m_OnDirtyVertsCallback -= action;
        }

        /// <summary>
        /// Add a listener to receive notification when the graphics material is dirtied.
        /// </summary>
        /// <param name="action">The method to call when invoked.</param>
        public void RegisterDirtyMaterialCallback(UnityAction action)
        {
            m_OnDirtyMaterialCallback += action;
        }

        /// <summary>
        /// Remove a listener from receiving notifications when the graphics material are dirtied
        /// </summary>
        /// <param name="action">The method to call when invoked.</param>
        public void UnregisterDirtyMaterialCallback(UnityAction action)
        {
            m_OnDirtyMaterialCallback -= action;
        }
    }
}
