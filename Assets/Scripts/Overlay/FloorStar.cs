using UnityEngine;
using UnityEngine.UI;

namespace PostureCheck
{
    /// <summary>
    /// The star on the floor in the front camera view (step 1.7).
    /// - With a scanned floor: the star is a square on the floor plane, projected with the front
    ///   camera's field of view (config), so it shrinks with distance like a real object.
    ///   When the ankles are seen near the star, the star moves under them: the feet are on the
    ///   real floor, which corrects the small error from carrying the floor over after the turn.
    /// - Without a scanned floor (skipped or failed): a flattened star under the ankles.
    /// Drawn between the camera image and the posture line.
    /// </summary>
    public class FloorStar : MonoBehaviour
    {
        [SerializeField] FrontCamera frontCamera;
        [SerializeField] PoseSmoother pose;
        [SerializeField] FlowController flow;
        [Tooltip("Star width on the floor, metres.")]
        [SerializeField] float starSize = 0.5f;
        [Tooltip("Ankles closer than this to the star (metres, along the floor) pull the star under them.")]
        [SerializeField] float snapDistance = 0.6f;
        [Tooltip("Ankles closer than this to the star count as standing on it.")]
        [SerializeField] float onStarDistance = 0.3f;
        [Tooltip("Without a scanned floor: star width as a fraction of the body height on screen.")]
        [SerializeField] float fallbackWidthOfBody = 0.35f;

        /// <summary>True once the star is on screen.</summary>
        public bool Visible { get; private set; }
        /// <summary>True while the child's feet are on the star.</summary>
        public bool FeetOnStar { get; private set; }

        StarGraphic m_Graphic;
        Vector3 m_Spot;      // current star centre in device coordinates (scanned floor only)
        bool m_HasSpot;
        Vector2 m_FallbackCentre;
        float m_FallbackWidth;
        bool m_HasFallback;

        void Start()
        {
            var canvasGo = new GameObject("StarCanvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0; // above the camera image (-100), below the posture line (10)

            var go = new GameObject("Star", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            m_Graphic = go.AddComponent<StarGraphic>();
            m_Graphic.raycastTarget = false;
            m_Graphic.texture = AppConfig.LoadContentImage(AppConfig.Current.assets.floorMarker);
            var rt = m_Graphic.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; // local coordinates = screen pixels
        }

        void LateUpdate()
        {
            var frame = frontCamera.Frame;
            var floor = flow.Floor;
            if (frame == null || frontCamera.State != FrontCamera.CameraState.Running)
            {
                Show(false);
                return;
            }

            if (floor.valid) UpdateScanned(frame, floor);
            else UpdateFallback(frame);
        }

        void UpdateScanned(Texture frame, TurnTracker.FloorInFront floor)
        {
            if (!m_HasSpot) { m_Spot = floor.spot; m_HasSpot = true; }
            var up = floor.up;
            float f = FocalPx(frame);

            FeetOnStar = false;
            if (AnklesVisible(out var ankleFramePx))
            {
                // Ray from the camera through the ankles, hit with the floor plane.
                var dir = new Vector3(-(ankleFramePx.x - frame.width * 0.5f) / f, (ankleFramePx.y - frame.height * 0.5f) / f, 1f);
                float denom = Vector3.Dot(dir, up);
                if (Mathf.Abs(denom) > 1e-4f)
                {
                    float t = Vector3.Dot(floor.spot, up) / denom;
                    var feet = dir * t;
                    float dist = Vector3.ProjectOnPlane(feet - m_Spot, up).magnitude;
                    if (t > 0f && dist < snapDistance)
                    {
                        m_Spot = Vector3.Lerp(m_Spot, feet, 1f - Mathf.Exp(-Time.deltaTime / 0.3f));
                        FeetOnStar = dist < onStarDistance;
                    }
                }
            }

            // Square on the floor, the star's top pointing away from the phone.
            var away = Vector3.ProjectOnPlane(m_Spot, up);
            if (away.sqrMagnitude < 1e-4f || m_Spot.z <= 0.1f) { Show(false); return; }
            away.Normalize();
            var side = Vector3.Cross(up, away);
            var spot = m_Spot;
            float half = starSize * 0.5f;
            m_Graphic.SetProjected((u, v) =>
            {
                var p = spot + side * ((u - 0.5f) * 2f * half) + away * ((v - 0.5f) * 2f * half);
                if (p.z <= 0.05f) p.z = 0.05f;
                var framePx = new Vector2(frame.width * 0.5f - f * p.x / p.z, frame.height * 0.5f + f * p.y / p.z);
                return frontCamera.FrameToScreen(framePx);
            });
            Show(true);
        }

        void UpdateFallback(Texture frame)
        {
            if (AnklesVisible(out var ankleFramePx))
            {
                var centre = frontCamera.FrameToScreen(ankleFramePx);
                float bodyHeight = (frontCamera.FrameToScreen(pose[BodyPoint.Nose].position) - centre).magnitude;
                float width = Mathf.Max(40f, bodyHeight * fallbackWidthOfBody);
                float k = m_HasFallback ? 1f - Mathf.Exp(-Time.deltaTime / 0.3f) : 1f;
                m_FallbackCentre = Vector2.Lerp(m_FallbackCentre, centre, k);
                m_FallbackWidth = Mathf.Lerp(m_FallbackWidth, width, k);
                m_HasFallback = true;
            }
            FeetOnStar = m_HasFallback && AnklesVisible(out _);
            if (!m_HasFallback) { Show(false); return; }

            var c = m_FallbackCentre;
            float w = m_FallbackWidth, h = w * 0.3f; // flattened: seen at a low angle
            m_Graphic.SetProjected((u, v) => c + new Vector2((u - 0.5f) * w, (v - 0.5f) * h));
            Show(true);
        }

        bool AnklesVisible(out Vector2 midFramePx)
        {
            midFramePx = default;
            if (!pose.HasPose) return false;
            float minVis = AppConfig.Current.thresholds.minVisibility;
            var l = pose[BodyPoint.LeftAnkle];
            var r = pose[BodyPoint.RightAnkle];
            if (l.visibility < minVis || r.visibility < minVis) return false;
            midFramePx = (l.position + r.position) * 0.5f;
            return true;
        }

        /// <summary>Focal length in pixels of the upright front camera image.</summary>
        static float FocalPx(Texture frame) =>
            frame.height * 0.5f / Mathf.Tan(AppConfig.Current.camera.frontVerticalFovDeg * 0.5f * Mathf.Deg2Rad);

        void Show(bool show)
        {
            Visible = show;
            if (m_Graphic != null) m_Graphic.enabled = show;
            if (!show) FeetOnStar = false;
        }
    }

    /// <summary>
    /// A textured quad drawn as a grid, each grid point placed by a function: a fine grid makes a
    /// perspective-projected square look right, which a plain 4-corner UI image cannot.
    /// </summary>
    public class StarGraphic : RawImage
    {
        const int N = 8;
        readonly Vector2[] m_Points = new Vector2[(N + 1) * (N + 1)];

        /// <summary>Sets every grid point from (u, v) in 0..1 to a screen position.</summary>
        public void SetProjected(System.Func<float, float, Vector2> place)
        {
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
                m_Points[j * (N + 1) + i] = place(i / (float)N, j / (float)N) / scale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var c = (Color32)color;
            for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
                vh.AddVert(m_Points[j * (N + 1) + i], c, new Vector2(i / (float)N, j / (float)N));
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int a = j * (N + 1) + i, b = a + 1, d = a + N + 1, e = d + 1;
                vh.AddTriangle(a, d, e);
                vh.AddTriangle(a, e, b);
            }
        }
    }
}
