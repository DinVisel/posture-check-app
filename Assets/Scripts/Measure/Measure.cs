using UnityEngine;
using UnityEngine.InputSystem;

namespace PostureCheck
{
    /// <summary>
    /// Per-frame body measurements and the whole-body check.
    /// Live (Phase 1): shoulder and hip tilt against true horizontal (gravity sensor; screen horizontal
    /// in the Editor), smoothed over ~0.5 s, with hysteresis to colour the lines orange.
    /// Check (Phase 2): every pose frame's raw values feed the running <see cref="MeasureSession"/>,
    /// which turns them into medians over a still window. A value is used only when its landmarks
    /// are visible above the threshold.
    /// </summary>
    public class Measure : MonoBehaviour
    {
        [SerializeField] FrontCamera frontCamera;
        [SerializeField] PoseSmoother pose;
        [SerializeField] Overlay overlay;

        /// <summary>Smoothed live angle with on/off hysteresis.</summary>
        public class LiveTilt
        {
            public bool Valid { get; private set; }
            public float Deg { get; private set; }
            public bool OutOfRange { get; private set; }
            bool m_Has;

            public void Update(float raw, float dt, float smoothing, float threshold, float margin)
            {
                Valid = !float.IsNaN(raw);
                if (!Valid)
                {
                    m_Has = false;
                    OutOfRange = false;
                    return;
                }
                float k = !m_Has || smoothing <= 0f ? 1f : 1f - Mathf.Exp(-dt / smoothing);
                Deg = Mathf.Lerp(Deg, raw, k);
                m_Has = true;
                float abs = Mathf.Abs(Deg);
                if (OutOfRange) { if (abs < threshold - margin) OutOfRange = false; }
                else if (abs > threshold) OutOfRange = true;
            }
        }

        public LiveTilt Shoulders { get; } = new();
        public LiveTilt Hips { get; } = new();

        /// <summary>True while both shoulders are visible enough to measure.</summary>
        public bool Valid => Shoulders.Valid;
        /// <summary>Smoothed shoulder tilt in degrees; positive = the person's left shoulder is higher.</summary>
        public float TiltDeg => Shoulders.Deg;
        public bool OutOfRange => Shoulders.OutOfRange;
        /// <summary>Where "down" comes from: "gravity", "accelerometer" or "screen" (no sensor).</summary>
        public string DownSource { get; private set; } = "screen";

        /// <summary>Latest raw (unsmoothed) value per <see cref="MeasureKind"/>; NaN = not measurable this frame.</summary>
        public float[] Raw { get; } = NewNaNs(System.Enum.GetValues(typeof(MeasureKind)).Length);

        /// <summary>The running check session, if any. Fed with every new pose frame.</summary>
        public MeasureSession Session { get; private set; }
        /// <summary>Set by the flow: the session ignores frames (and its timers stop) while true.</summary>
        public bool SessionPaused { get; set; }

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
        /// <summary>Debug: why the body check passes or fails, per point.</summary>
        public string BodyDebug { get; private set; } = "";

        bool m_RawInView;
        SetupHint m_RawHint = SetupHint.StepIn;
        float m_InViewTimer, m_HintTimer;

        int m_LastVersion = -1;
        float m_LastTime = -1f;
        Vector2 m_Down = Vector2.down;

        // Points watched for movement during a still window.
        static readonly BodyPoint[] StillPoints =
        {
            BodyPoint.Nose, BodyPoint.LeftShoulder, BodyPoint.RightShoulder, BodyPoint.LeftHip, BodyPoint.RightHip,
            BodyPoint.LeftKnee, BodyPoint.RightKnee, BodyPoint.LeftAnkle, BodyPoint.RightAnkle,
        };
        readonly Vector2[] m_StillPoints = new Vector2[StillPoints.Length];

        static float[] NewNaNs(int n)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = float.NaN;
            return a;
        }

        /// <summary>Starts a new check session for these values (replaces any running one).</summary>
        public MeasureSession BeginSession(params MeasureKind[] kinds)
        {
            Session = new MeasureSession(kinds, AppConfig.Current.measurement);
            return Session;
        }

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

            float now = Time.realtimeSinceStartup;
            float dt = m_LastTime < 0f ? 0f : now - m_LastTime;
            m_LastTime = now;

            var t = AppConfig.Current.thresholds;
            bool hasPose = pose.HasPose && frontCamera.Frame != null;
            for (int i = 0; i < Raw.Length; i++) Raw[i] = float.NaN;
            if (hasPose)
            {
                float minVis = t.minVisibility;
                Raw[(int)MeasureKind.ShoulderTilt] = Tilt(BodyPoint.LeftShoulder, BodyPoint.RightShoulder, minVis);
                Raw[(int)MeasureKind.HipTilt] = Tilt(BodyPoint.LeftHip, BodyPoint.RightHip, minVis);
                Raw[(int)MeasureKind.KneeLeft] = Knee(BodyPoint.LeftHip, BodyPoint.LeftKnee, BodyPoint.LeftAnkle, BodyPoint.RightHip, minVis);
                Raw[(int)MeasureKind.KneeRight] = Knee(BodyPoint.RightHip, BodyPoint.RightKnee, BodyPoint.RightAnkle, BodyPoint.LeftHip, minVis);
            }

            Shoulders.Update(Raw[(int)MeasureKind.ShoulderTilt], dt, t.smoothingSeconds, t.shoulderTiltDeg, t.shoulderTiltMarginDeg);
            Hips.Update(Raw[(int)MeasureKind.HipTilt], dt, t.smoothingSeconds, t.hipTiltDeg, t.hipTiltMarginDeg);
            overlay.ShoulderOutOfRange = Shoulders.OutOfRange;
            overlay.HipOutOfRange = Hips.OutOfRange;

            if (Session != null && !Session.Done)
            {
                float torso = hasPose ? TorsoLength(t.minVisibility) : 0f;
                for (int i = 0; i < StillPoints.Length; i++)
                {
                    var lm = hasPose ? pose[StillPoints[i]] : default;
                    m_StillPoints[i] = hasPose && lm.visibility >= t.minVisibility
                        ? frontCamera.FrameToScreen(lm.position)
                        : new Vector2(float.NaN, float.NaN);
                }
                Session.Add(now, m_StillPoints, torso, Raw, SessionPaused || !hasPose);
            }
        }

        Vector2 S(BodyPoint p) => frontCamera.FrameToScreen(pose[p].position);
        bool Visible(BodyPoint p, float minVis) => pose[p].visibility >= minVis;

        /// <summary>Angle of the line from the right point to the left point against true horizontal; positive = left higher.</summary>
        float Tilt(BodyPoint left, BodyPoint right, float minVis)
        {
            if (!Visible(left, minVis) || !Visible(right, minVis)) return float.NaN;
            // Screen space, so the angle matches what the user sees (mirroring does not change heights).
            var v = S(left) - S(right);
            if (v.sqrMagnitude < 1f) return float.NaN;
            float rise = Vector2.Dot(v, -m_Down);
            return Mathf.Asin(Mathf.Clamp(rise / v.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Front-view knee bend: 180° minus the hip-knee-ankle angle. Positive when the knee sits on the
        /// body's midline side of the hip-ankle line (knock-knee), negative when outside (bow-leg).
        /// </summary>
        float Knee(BodyPoint hip, BodyPoint knee, BodyPoint ankle, BodyPoint otherHip, float minVis)
        {
            if (!Visible(hip, minVis) || !Visible(knee, minVis) || !Visible(ankle, minVis) || !Visible(otherHip, minVis))
                return float.NaN;
            Vector2 h = S(hip), k = S(knee), a = S(ankle), o = S(otherHip);
            Vector2 up = h - k, down = a - k;
            if (up.sqrMagnitude < 1f || down.sqrMagnitude < 1f) return float.NaN;
            float bend = 180f - Vector2.Angle(up, down);
            var axis = a - h;
            float kneeSide = Cross(axis, k - h);
            float midSide = Cross(axis, o - h);
            if (Mathf.Abs(midSide) < 1e-3f) return float.NaN;
            return Mathf.Sign(kneeSide) == Mathf.Sign(midSide) ? bend : -bend;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Screen distance from mid-shoulders to mid-hips; 0 if not visible.</summary>
        float TorsoLength(float minVis)
        {
            if (!Visible(BodyPoint.LeftShoulder, minVis) || !Visible(BodyPoint.RightShoulder, minVis) ||
                !Visible(BodyPoint.LeftHip, minVis) || !Visible(BodyPoint.RightHip, minVis)) return 0f;
            var shoulders = (S(BodyPoint.LeftShoulder) + S(BodyPoint.RightShoulder)) * 0.5f;
            var hips = (S(BodyPoint.LeftHip) + S(BodyPoint.RightHip)) * 0.5f;
            return Vector2.Distance(shoulders, hips);
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
            BodyDebug = "no person";
            if (pose.HasPose && frontCamera.Frame != null)
            {
                float minVis = AppConfig.Current.thresholds.minVisibility;
                float margin = edgeMargin * Screen.height;
                var debug = new System.Text.StringBuilder();
                bool OnScreen(BodyPoint p, string label)
                {
                    var lm = pose[p];
                    var s = frontCamera.FrameToScreen(lm.position);
                    bool visible = lm.visibility >= minVis;
                    bool inside = s.x > margin && s.x < Screen.width - margin && s.y > margin && s.y < Screen.height - margin;
                    debug.Append($"{label} {lm.visibility:0.00}{(visible ? "" : " low")}{(inside ? "" : s.y <= margin ? " below" : s.y >= Screen.height - margin ? " above" : " side")}  ");
                    return visible && inside;
                }
                bool nose = OnScreen(BodyPoint.Nose, "nose");
                bool leftAnkle = OnScreen(BodyPoint.LeftAnkle, "L ankle");
                bool rightAnkle = OnScreen(BodyPoint.RightAnkle, "R ankle");
                inView = nose && leftAnkle && rightAnkle;
                BodyDebug = debug.ToString();

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
