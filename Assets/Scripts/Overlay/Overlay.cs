using UnityEngine;
using UnityEngine.UI;

namespace PostureCheck
{
    /// <summary>
    /// Draws the posture line over the camera image: a centre line (head → mid-shoulders →
    /// mid-hips → mid-ankles) plus the shoulder and hip lines, with dots at the joints.
    /// A segment is drawn only when its points are visible enough.
    /// Later steps add the floor star (1.7), the ghost outline and the stepping stones (Phase 2).
    /// </summary>
    public class Overlay : MonoBehaviour
    {
        [SerializeField] FrontCamera frontCamera;
        [SerializeField] PoseSmoother pose;
        Color lineColor, outOfRangeColor; // from config.json "colors"
        [Range(0, 1)] [SerializeField] float minVisibility = 0.5f;
        [Tooltip("Line thickness as a fraction of the screen's short side.")]
        [SerializeField] float thickness = 0.012f;

        /// <summary>Set by the measurement step: colours the shoulder line orange.</summary>
        public bool ShoulderOutOfRange { get; set; }

        struct Point
        {
            public Vector2 screen;
            public bool ok;
        }

        class Shape
        {
            public RectTransform rect;
            public Image image;
        }

        Shape m_Neck, m_Torso, m_Legs, m_Shoulders, m_Hips;
        Shape m_HeadDot, m_ShoulderMidDot, m_HipMidDot, m_AnkleMidDot;
        Shape m_LShoulderDot, m_RShoulderDot, m_LHipDot, m_RHipDot;
        Transform m_Root;
        Sprite m_Circle;

        void Start()
        {
            lineColor = AppConfig.Current.PostureLineColor;
            outOfRangeColor = AppConfig.Current.PostureLineOutOfRangeColor;
            var canvasGo = new GameObject("OverlayCanvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the camera image, below debug text
            m_Root = canvasGo.transform;
            m_Circle = CreateCircleSprite(64);

            // Lines first so the dots draw on top.
            m_Neck = NewShape("Neck", null);
            m_Torso = NewShape("Torso", null);
            m_Legs = NewShape("Legs", null);
            m_Shoulders = NewShape("ShoulderLine", null);
            m_Hips = NewShape("HipLine", null);
            m_HeadDot = NewShape("Head", m_Circle);
            m_ShoulderMidDot = NewShape("ShoulderMid", m_Circle);
            m_HipMidDot = NewShape("HipMid", m_Circle);
            m_AnkleMidDot = NewShape("AnkleMid", m_Circle);
            m_LShoulderDot = NewShape("LeftShoulder", m_Circle);
            m_RShoulderDot = NewShape("RightShoulder", m_Circle);
            m_LHipDot = NewShape("LeftHip", m_Circle);
            m_RHipDot = NewShape("RightHip", m_Circle);
        }

        void LateUpdate()
        {
            bool show = pose.HasPose && frontCamera.Frame != null;
            m_Root.gameObject.SetActive(show);
            if (!show) return;

            float width = thickness * Mathf.Min(Screen.width, Screen.height);
            float dot = width * 1.8f;
            var shoulderColor = ShoulderOutOfRange ? outOfRangeColor : lineColor;

            var head = P(BodyPoint.Nose);
            var lShoulder = P(BodyPoint.LeftShoulder);
            var rShoulder = P(BodyPoint.RightShoulder);
            var lHip = P(BodyPoint.LeftHip);
            var rHip = P(BodyPoint.RightHip);
            var shoulderMid = Mid(lShoulder, rShoulder);
            var hipMid = Mid(lHip, rHip);
            var ankleMid = Mid(P(BodyPoint.LeftAnkle), P(BodyPoint.RightAnkle));

            Line(m_Neck, head, shoulderMid, width, lineColor);
            Line(m_Torso, shoulderMid, hipMid, width, lineColor);
            Line(m_Legs, hipMid, ankleMid, width, lineColor);
            Line(m_Shoulders, lShoulder, rShoulder, width, shoulderColor);
            Line(m_Hips, lHip, rHip, width, lineColor);

            Dot(m_HeadDot, head, dot, lineColor);
            Dot(m_ShoulderMidDot, shoulderMid, dot, lineColor);
            Dot(m_HipMidDot, hipMid, dot, lineColor);
            Dot(m_AnkleMidDot, ankleMid, dot, lineColor);
            Dot(m_LShoulderDot, lShoulder, dot, shoulderColor);
            Dot(m_RShoulderDot, rShoulder, dot, shoulderColor);
            Dot(m_LHipDot, lHip, dot, lineColor);
            Dot(m_RHipDot, rHip, dot, lineColor);
        }

        Point P(BodyPoint p)
        {
            var lm = pose[p];
            return new Point { screen = frontCamera.FrameToScreen(lm.position), ok = lm.visibility >= minVisibility };
        }

        static Point Mid(Point a, Point b) => new() { screen = (a.screen + b.screen) * 0.5f, ok = a.ok && b.ok };

        static void Line(Shape s, Point a, Point b, float width, Color color)
        {
            bool ok = a.ok && b.ok;
            s.image.enabled = ok;
            if (!ok) return;
            var d = b.screen - a.screen;
            s.rect.anchoredPosition = a.screen;
            s.rect.sizeDelta = new Vector2(d.magnitude, width);
            s.rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            s.image.color = color;
        }

        static void Dot(Shape s, Point p, float size, Color color)
        {
            s.image.enabled = p.ok;
            if (!p.ok) return;
            s.rect.anchoredPosition = p.screen;
            s.rect.sizeDelta = new Vector2(size, size);
            s.image.color = color;
        }

        Shape NewShape(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(m_Root, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; // anchoredPosition = screen pixels
            rt.pivot = sprite == null ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f); // lines grow from their start point
            return new Shape { rect = rt, image = img };
        }

        static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OverlayCircle" };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255); // 1-pixel soft edge
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        void OnDestroy()
        {
            if (m_Circle != null)
            {
                Destroy(m_Circle.texture);
                Destroy(m_Circle);
            }
        }
    }
}
