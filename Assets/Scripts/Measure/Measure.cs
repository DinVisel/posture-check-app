using UnityEngine;
using UnityEngine.InputSystem;

namespace PostureCheck
{
    /// <summary>
    /// Measures shoulder tilt: the angle of the line between the shoulders (landmarks 11 and 12)
    /// from true horizontal. "Horizontal" comes from the phone's gravity sensor, so a slightly
    /// tilted phone does not count as a tilted child; in the Editor the screen's horizontal is used.
    /// The angle is smoothed over ~0.5 s and only measured when both shoulders are clearly visible.
    /// Out of range uses hysteresis: on above the threshold, off only below threshold − margin.
    /// </summary>
    public class Measure : MonoBehaviour
    {
        [SerializeField] FrontCamera frontCamera;
        [SerializeField] PoseSmoother pose;
        [SerializeField] Overlay overlay;

        /// <summary>True while both shoulders are visible enough to measure.</summary>
        public bool Valid { get; private set; }
        /// <summary>Smoothed shoulder tilt in degrees; positive = the person's left shoulder is higher.</summary>
        public float TiltDeg { get; private set; }
        public bool OutOfRange { get; private set; }
        /// <summary>Where "down" comes from: "gravity", "accelerometer" or "screen" (no sensor).</summary>
        public string DownSource { get; private set; } = "screen";

        public enum SetupHint { None, StepIn, StepBack, MoveLeft, MoveRight }

        [Tooltip("Head and ankles must be this far inside the screen edges (fraction of screen height).")]
        [SerializeField] float edgeMargin = 0.03f;
        [Tooltip("Body centre further than this from the screen centre (fraction of width) gives a move hint.")]
        [SerializeField] float centreTolerance = 0.2f;
        [Tooltip("Seconds a new body-in-view result must hold before it counts (stops flicker).")]
        [SerializeField] float bodySettleSeconds = 0.5f;

        /// <summary>True when the head and both ankles are visible and on screen (settled).</summary>
        public bool BodyInView { get; private set; }
        /// <summary>What the child should do to get the whole body in view (settled).</summary>
        public SetupHint Hint { get; private set; } = SetupHint.StepIn;

        bool m_RawInView;
        SetupHint m_RawHint = SetupHint.StepIn;
        float m_InViewTimer, m_HintTimer;

        int m_LastVersion = -1;
        float m_LastTime;
        bool m_HasSmoothed;
        Vector2 m_Down = Vector2.down;

        void OnEnable()
        {
            if (GravitySensor.current != null) InputSystem.EnableDevice(GravitySensor.current);
            else if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
        }

        void Update()
        {
            UpdateDown();
            UpdateBody();

            if (pose.Version == m_LastVersion) return;
            m_LastVersion = pose.Version;

            var t = AppConfig.Current.thresholds;
            var left = pose[BodyPoint.LeftShoulder];
            var right = pose[BodyPoint.RightShoulder];
            Valid = pose.HasPose && frontCamera.Frame != null &&
                    left.visibility >= t.minVisibility && right.visibility >= t.minVisibility;

            if (!Valid)
            {
                m_HasSmoothed = false;
                OutOfRange = false;
                overlay.ShoulderOutOfRange = false;
                return;
            }

            // Screen space, so the angle matches what the user sees (mirroring does not change heights).
            var l = frontCamera.FrameToScreen(left.position);
            var r = frontCamera.FrameToScreen(right.position);
            var shoulders = l - r;
            if (shoulders.sqrMagnitude < 1f) return;
            float rise = Vector2.Dot(shoulders, -m_Down);
            float tilt = Mathf.Asin(Mathf.Clamp(rise / shoulders.magnitude, -1f, 1f)) * Mathf.Rad2Deg;

            float now = Time.realtimeSinceStartup;
            if (!m_HasSmoothed)
            {
                TiltDeg = tilt;
                m_HasSmoothed = true;
            }
            else
            {
                float dt = now - m_LastTime;
                float k = t.smoothingSeconds > 0f ? 1f - Mathf.Exp(-dt / t.smoothingSeconds) : 1f;
                TiltDeg = Mathf.Lerp(TiltDeg, tilt, k);
            }
            m_LastTime = now;

            float abs = Mathf.Abs(TiltDeg);
            if (OutOfRange) { if (abs < t.shoulderTiltDeg - t.shoulderTiltMarginDeg) OutOfRange = false; }
            else if (abs > t.shoulderTiltDeg) OutOfRange = true;
            overlay.ShoulderOutOfRange = OutOfRange;
        }

        /// <summary>
        /// Whole-body check: nose (0) and both ankles (27, 28) visible and inside the visible screen
        /// area. Hints use screen positions, which are already mirrored: a child shown on the left of
        /// the screen is on their own left, so they are told to move right.
        /// </summary>
        void UpdateBody()
        {
            var raw = SetupHint.StepIn;
            bool inView = false;
            if (pose.HasPose && frontCamera.Frame != null)
            {
                float minVis = AppConfig.Current.thresholds.minVisibility;
                float margin = edgeMargin * Screen.height;
                bool OnScreen(BodyPoint p)
                {
                    var lm = pose[p];
                    var s = frontCamera.FrameToScreen(lm.position);
                    return lm.visibility >= minVis && s.x > margin && s.x < Screen.width - margin &&
                           s.y > margin && s.y < Screen.height - margin;
                }
                inView = OnScreen(BodyPoint.Nose) && OnScreen(BodyPoint.LeftAnkle) && OnScreen(BodyPoint.RightAnkle);

                var hips = (frontCamera.FrameToScreen(pose[BodyPoint.LeftHip].position) +
                            frontCamera.FrameToScreen(pose[BodyPoint.RightHip].position)) * 0.5f;
                float dx = hips.x / Screen.width - 0.5f;
                if (dx < -centreTolerance) raw = SetupHint.MoveRight;
                else if (dx > centreTolerance) raw = SetupHint.MoveLeft;
                else raw = inView ? SetupHint.None : SetupHint.StepBack;
            }

            float dt = Time.unscaledDeltaTime;
            m_InViewTimer = inView == m_RawInView ? m_InViewTimer + dt : 0f;
            m_RawInView = inView;
            if (m_InViewTimer >= bodySettleSeconds) BodyInView = inView;

            m_HintTimer = raw == m_RawHint ? m_HintTimer + dt : 0f;
            m_RawHint = raw;
            if (m_HintTimer >= bodySettleSeconds) Hint = raw;
        }

        /// <summary>Gravity direction on screen (x right, y up), unit length.</summary>
        void UpdateDown()
        {
            Vector3 g;
            if (GravitySensor.current != null && GravitySensor.current.enabled)
            {
                g = GravitySensor.current.gravity.ReadValue();
                DownSource = "gravity";
            }
            else if (Accelerometer.current != null && Accelerometer.current.enabled)
            {
                // Includes movement, but the phone stands still on the floor during a check.
                g = Accelerometer.current.acceleration.ReadValue();
                DownSource = "accelerometer";
            }
            else
            {
                m_Down = Vector2.down;
                DownSource = "screen";
                return;
            }

            if (!InputSystem.settings.compensateForScreenOrientation) g = ToScreen(g);
            var g2 = new Vector2(g.x, g.y);
            // Phone lying flat: gravity is almost all along z and its on-screen direction is noise. Keep the last one.
            if (g2.magnitude < 0.5f * g.magnitude || g2.sqrMagnitude < 1e-6f) return;
            m_Down = g2.normalized;
        }

        /// <summary>Rotates a vector from the device's natural (portrait) axes to the current screen axes.</summary>
        static Vector3 ToScreen(Vector3 v)
        {
            switch (Screen.orientation)
            {
                case ScreenOrientation.LandscapeLeft: return new Vector3(-v.y, v.x, v.z);
                case ScreenOrientation.LandscapeRight: return new Vector3(v.y, -v.x, v.z);
                case ScreenOrientation.PortraitUpsideDown: return new Vector3(-v.x, -v.y, v.z);
                default: return v;
            }
        }
    }
}
