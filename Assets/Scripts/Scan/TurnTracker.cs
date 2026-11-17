using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Gyro = UnityEngine.InputSystem.Gyroscope;

namespace PostureCheck
{
    /// <summary>
    /// The "turn the phone around" step (1.6). Shows the instruction, measures the turn with the
    /// gyroscope, waits until the phone has been put down and is still, then carries the scanned
    /// floor spot from the back camera's view into the front camera's view.
    ///
    /// Device coordinates (used for <see cref="FloorInFront"/>), with the phone in portrait:
    /// x = screen right, y = screen up, z = out of the screen towards the room. Because the screen
    /// shows the front camera mirrored, a point with x &gt; 0 appears on the right of the screen.
    /// Assumption: the phone is put down where it was held during the scan (small error accepted;
    /// step 1.7 snaps the star to the feet).
    /// </summary>
    public class TurnTracker : MonoBehaviour
    {
        public struct FloorInFront
        {
            public bool valid;
            /// <summary>The scanned spot in device coordinates, metres.</summary>
            public Vector3 spot;
            /// <summary>Floor "up" in device coordinates.</summary>
            public Vector3 up;
        }

        [Tooltip("Turn (degrees about the vertical) needed before stillness counts as 'put down'.")]
        [SerializeField] float minTurnDeg = 120f;
        [SerializeField] float stillSeconds = 1f;
        [Tooltip("rad/s below which the phone counts as still.")]
        [SerializeField] float stillRate = 0.15f;
        [Tooltip("Measured turns further than this from 180° are ignored and 180° is assumed.")]
        [SerializeField] float maxTurnErrorDeg = 45f;


        public FloorInFront Floor { get; private set; }
        /// <summary>Measured turn about the vertical, degrees (counter-clockwise seen from above). NaN without a gyroscope.</summary>
        public float MeasuredTurnDeg { get; private set; } = float.NaN;

        bool m_Continue;

        /// <summary>Shows the turn screen and returns once the phone is down (or the parent taps Continue).</summary>
        public IEnumerator Run(FloorScanner.FloorResult scan)
        {
            Floor = default;
            m_Continue = false;
            var gyro = Gyro.current;
            var gravity = GravitySensor.current;
            if (gyro != null) InputSystem.EnableDevice(gyro);
            if (gravity != null) InputSystem.EnableDevice(gravity);

            Ui.EnsureEventSystem();
            var canvas = BuildUi();

            float yaw = 0f, still = 0f;
            while (!m_Continue)
            {
                if (gyro != null && gyro.enabled && gravity != null && gravity.enabled)
                {
                    float dt = Time.unscaledDeltaTime;
                    var w = gyro.angularVelocity.ReadValue();
                    var up = -gravity.gravity.ReadValue();
                    if (up.sqrMagnitude > 1e-4f) yaw += Vector3.Dot(w, up.normalized) * dt * Mathf.Rad2Deg;
                    still = w.magnitude < stillRate ? still + dt : 0f;
                    if (Mathf.Abs(yaw) >= minTurnDeg && still >= stillSeconds) break;
                }
                yield return null;
            }

            if (gyro != null && gyro.enabled) MeasuredTurnDeg = yaw;
            Destroy(canvas.gameObject);

            if (scan.outcome == FloorScanner.Outcome.Found)
            {
                var up1 = gravity != null && gravity.enabled ? -gravity.gravity.ReadValue() : Vector3.up;
                Floor = Carry(scan, up1, MeasuredTurnDeg, maxTurnErrorDeg);
            }
            Debug.Log($"TurnTracker: turn {MeasuredTurnDeg:0} deg, floor valid {Floor.valid}, spot {Floor.spot}, up {Floor.up}");
        }

        /// <summary>
        /// Rebuilds the spot in the front view. Both views share a floor frame (up, forward along the
        /// floor towards the spot side, right); only the device axes differ: before the turn
        /// "forward" is out of the back, after it out of the screen (corrected by the measured turn).
        /// </summary>
        public static FloorInFront Carry(FloorScanner.FloorResult scan, Vector3 upAfter, float turnDeg, float maxErrorDeg)
        {
            // AR camera space (x right, y up, z out of the back) → device coordinates (z out of the screen).
            var spot0 = new Vector3(scan.spotInCamera.x, scan.spotInCamera.y, -scan.spotInCamera.z);
            var up0 = new Vector3(scan.floorUpInCamera.x, scan.floorUpInCamera.y, -scan.floorUpInCamera.z).normalized;
            var fwd0 = Vector3.ProjectOnPlane(Vector3.back, up0); // out of the back, along the floor
            if (fwd0.sqrMagnitude < 1e-4f || upAfter.sqrMagnitude < 1e-4f) return default;
            fwd0.Normalize();
            var right0 = Vector3.Cross(up0, fwd0);
            float a = Vector3.Dot(spot0, fwd0), b = Vector3.Dot(spot0, right0), c = Vector3.Dot(spot0, up0);

            var up1 = upAfter.normalized;
            var fwd1 = Vector3.ProjectOnPlane(Vector3.forward, up1); // out of the screen, along the floor
            if (fwd1.sqrMagnitude < 1e-4f) return default;
            fwd1.Normalize();
            // A turn of 180° + d leaves the screen facing d away from where the back faced; undo it.
            if (!float.IsNaN(turnDeg))
            {
                float d = Mathf.DeltaAngle(180f * Mathf.Sign(turnDeg), turnDeg);
                if (Mathf.Abs(d) <= maxErrorDeg) fwd1 = Quaternion.AngleAxis(-d, up1) * fwd1;
            }
            var right1 = Vector3.Cross(up1, fwd1);

            return new FloorInFront { valid = true, spot = a * fwd1 + b * right1 + c * up1, up = up1 };
        }

        Canvas BuildUi()
        {
            var canvas = Ui.Canvas("TurnCanvas", transform, 20);
            var root = canvas.transform;
            Ui.Panel(root, "Background", new Vector2(0.5f, 0.5f), new Vector2(4000, 4000), new Color(0.12f, 0.12f, 0.12f));
            var text = Ui.Text(root, "Instruction", new Vector2(0.5f, 0.75f), new Vector2(900, 400), 60);
            text.text = AppConfig.Current.Line("turn.instruction");

            // Placeholder "turn around" arrow: a U-turn made of bars.
            var arrow = new GameObject("Arrow", typeof(RectTransform)).transform;
            arrow.SetParent(root, false);
            Ui.Place((RectTransform)arrow, new Vector2(0.5f, 0.45f), new Vector2(400, 400));
            Bar(arrow, new Vector2(-120, 0), new Vector2(24, 260), 0);      // left side going up
            Bar(arrow, new Vector2(0, 130), new Vector2(264, 24), 0);       // top
            Bar(arrow, new Vector2(120, 40), new Vector2(24, 200), 0);      // right side going down
            Bar(arrow, new Vector2(85, -40), new Vector2(24, 110), 45);     // arrow head
            Bar(arrow, new Vector2(155, -40), new Vector2(24, 110), -45);

            var button = Ui.Button(root, "Continue", AppConfig.Current.Line("turn.continue"), new Vector2(0.5f, 0.12f), new Vector2(520, 140));
            button.onClick.AddListener(() => m_Continue = true);
            return canvas;
        }

        static void Bar(Transform parent, Vector2 pos, Vector2 size, float angle)
        {
            var img = Ui.Panel(parent, "Bar", new Vector2(0.5f, 0.5f), size, Color.white);
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
