using UnityEngine;
using UnityEngine.UI;

namespace PostureCheck
{
    /// <summary>
    /// Debug overlay for step 1.2: draws all 33 landmarks and shows camera FPS, pose FPS and latency.
    /// Green = visible (above the visibility threshold), red = low visibility.
    /// </summary>
    public class PoseDebugView : MonoBehaviour
    {
        [SerializeField] FrontCamera frontCamera;
        [SerializeField] PoseTracker poseTracker;
        [Range(0, 1)] [SerializeField] float visibilityThreshold = 0.6f;
        [SerializeField] float dotSize = 16f;

        static readonly Color Visible = new(0.2f, 1f, 0.3f);
        static readonly Color Hidden = new(1f, 0.3f, 0.3f, 0.6f);

        RectTransform[] m_Dots;
        Image[] m_DotImages;
        Text m_Stats;

        int m_LastCamFrames, m_LastPoseVersion;
        float m_StatsTimer;
        float m_CamFps, m_PoseFps;

        void Start()
        {
            var canvasGo = new GameObject("DebugCanvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            m_Dots = new RectTransform[PoseTracker.LandmarkCount];
            m_DotImages = new Image[PoseTracker.LandmarkCount];
            for (int i = 0; i < m_Dots.Length; i++)
            {
                var go = new GameObject($"Landmark{i}", typeof(Image));
                go.transform.SetParent(canvasGo.transform, false);
                m_DotImages[i] = go.GetComponent<Image>();
                m_DotImages[i].raycastTarget = false;
                m_Dots[i] = m_DotImages[i].rectTransform;
                m_Dots[i].anchorMin = m_Dots[i].anchorMax = Vector2.zero; // position = screen pixels
                m_Dots[i].sizeDelta = Vector2.one * dotSize;
            }

            var textGo = new GameObject("Stats", typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            m_Stats = textGo.GetComponent<Text>();
            m_Stats.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_Stats.fontSize = Mathf.RoundToInt(Screen.height / 45f);
            m_Stats.color = Color.yellow;
            m_Stats.raycastTarget = false;
            var rt = m_Stats.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(24, -48);
            rt.sizeDelta = new Vector2(-48, Screen.height * 0.25f);
        }

        void Update()
        {
            UpdateStats();

            bool show = poseTracker.HasPose && frontCamera.Frame != null;
            float scale = canvasScale;
            for (int i = 0; i < m_Dots.Length; i++)
            {
                m_DotImages[i].enabled = show;
                if (!show) continue;
                var lm = poseTracker.Landmarks[i];
                m_Dots[i].anchoredPosition = frontCamera.FrameToScreen(lm.position) / scale;
                m_DotImages[i].color = lm.visibility >= visibilityThreshold ? Visible : Hidden;
            }
        }

        float canvasScale => m_Stats != null && m_Stats.canvas != null ? m_Stats.canvas.scaleFactor : 1f;

        void UpdateStats()
        {
            m_StatsTimer += Time.unscaledDeltaTime;
            if (m_StatsTimer < 0.5f) return;
            m_CamFps = (frontCamera.FrameCount - m_LastCamFrames) / m_StatsTimer;
            m_PoseFps = (poseTracker.Version - m_LastPoseVersion) / m_StatsTimer;
            m_LastCamFrames = frontCamera.FrameCount;
            m_LastPoseVersion = poseTracker.Version;
            m_StatsTimer = 0;

            var frame = frontCamera.Frame;
            m_Stats.text =
                $"Camera: {frontCamera.State}" + (frame != null ? $" {frame.width}x{frame.height}" : "") + $"  {m_CamFps:0} fps\n" +
                $"Pose: {m_PoseFps:0} fps  latency {poseTracker.LatencyMs:0} ms\n" +
                $"Detection score: {poseTracker.DetectionScore:0.00}  {(poseTracker.HasPose ? "tracking" : "no person")}\n" +
                $"GPU: {SystemInfo.graphicsDeviceType}";
        }
    }
}
