using System.Collections;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Runs the check's states in order: start screen → floor scan (back camera) → turn the phone →
    /// setup (hints until the whole body is in view, star on the floor) → front scan (posture line,
    /// front measurements over a still window).
    /// </summary>
    public class FlowController : MonoBehaviour
    {
        public enum State { Start, FloorScan, TurnPhone, Setup, ScanFront }

        [SerializeField] FloorScanner floorScanner;
        [SerializeField] TurnTracker turnTracker;
        [SerializeField] FrontCamera frontCamera;
        [SerializeField] Measure measure;
        [SerializeField] FloorStar floorStar;
        [SerializeField] Mascot mascot;
        [SerializeField] StartScreen startScreen;
        [Tooltip("The scene camera; switched off while the AR camera draws the back camera image.")]
        [SerializeField] Camera sceneCamera;
        [Tooltip("Editor only: go straight to the front camera (for testing pose and overlay on a webcam).")]
        [SerializeField] bool skipFloorScanInEditor;

        [Tooltip("Seconds in view to wait for the child to stand on the star before measuring anyway.")]
        [SerializeField] float starWaitSeconds = 6f;

        static readonly MeasureKind[] FrontKinds =
            { MeasureKind.ShoulderTilt, MeasureKind.HipTilt, MeasureKind.KneeLeft, MeasureKind.KneeRight };

        static string L(string key) => AppConfig.Current.Line(key);

        readonly System.Collections.Generic.Dictionary<MeasureKind, MeasureValue> m_Results = new();
        /// <summary>Every finished measurement of this check (shown to parents in Phase 3).</summary>
        public System.Collections.Generic.IReadOnlyDictionary<MeasureKind, MeasureValue> Results => m_Results;

        public State Current { get; private set; }
        /// <summary>The scanned floor in the front camera's view; not valid if the scan was skipped.</summary>
        public TurnTracker.FloorInFront Floor { get; private set; }

        IEnumerator Start()
        {
            // Portrait only, so the back and front camera views and the sensors share the same axes.
            Screen.orientation = ScreenOrientation.Portrait;

            Current = State.Start;
            mascot.SetVisible(true);
            mascot.Say(L("start.greeting"));
            yield return startScreen.Run();
            mascot.SetVisible(false);

            bool scan = !(Application.isEditor && skipFloorScanInEditor);
            if (scan)
            {
                Current = State.FloorScan;
                if (sceneCamera != null) sceneCamera.enabled = false;
                yield return floorScanner.Run();
                var r = floorScanner.Result;
                Debug.Log($"Flow: floor scan {r.outcome}" + (r.secondsToFindFloor >= 0 ? $", floor after {r.secondsToFindFloor:0.0} s" : ""));
                yield return floorScanner.Shutdown(); // AR fully stopped before the front camera opens
                if (sceneCamera != null) sceneCamera.enabled = true;

                Current = State.TurnPhone;
                yield return turnTracker.Run(r);
                Floor = turnTracker.Floor;
            }

            frontCamera.Begin();
            mascot.SetVisible(true);
            yield return RunFrontScan();

            // Side view, poses and walking come next (Phase 2 steps 2.3–2.6); until then the live line stays on.
            while (true)
            {
                Current = measure.BodyInView ? State.ScanFront : State.Setup;
                mascot.Say(Current == State.Setup ? HintLine(measure.Hint) : L("front.done"));
                yield return null;
            }
        }

        /// <summary>
        /// Setup hints until the whole body is in view, then the front measurements: the child stands
        /// still on the star until every value has a median or is "could not measure". Leaving the view
        /// goes back to setup and pauses the measuring.
        /// </summary>
        IEnumerator RunFrontScan()
        {
            MeasureSession session = null;
            float starWait = 0f, tryAgainUntil = 0f;
            int seenFails = 0;
            while (session == null || !session.Done)
            {
                bool inView = measure.BodyInView;
                bool offStar = floorStar.Visible && !floorStar.FeetOnStar;
                if (inView && offStar) starWait += Time.deltaTime;
                // Wait for the child to stand on the star, but not forever: the star may be a little off.
                bool waitForStar = offStar && starWait < starWaitSeconds;

                Current = inView ? State.ScanFront : State.Setup;
                if (inView && session == null) session = measure.BeginSession(FrontKinds);
                measure.SessionPaused = !inView || waitForStar;

                if (session != null && session.FailedTries > seenFails)
                {
                    seenFails = session.FailedTries;
                    tryAgainUntil = Time.time + 2.5f;
                }

                mascot.Say(!inView ? HintLine(measure.Hint)
                         : waitForStar ? L("setup.standOnStar")
                         : Time.time < tryAgainUntil ? L("front.tryAgain")
                         : L("front.holdStill"));
                yield return null;
            }

            foreach (var kv in session.Results) m_Results[kv.Key] = kv.Value;
            var t = AppConfig.Current.thresholds;
            var log = new System.Text.StringBuilder("Flow: front scan done");
            foreach (var kv in session.Results)
                log.Append($", {kv.Key} {kv.Value}{(kv.Value.IsFlagged(kv.Key, t) ? " (flag)" : "")}");
            Debug.Log(log.ToString());
        }

        string HintLine(Measure.SetupHint hint) => hint switch
        {
            Measure.SetupHint.StepBack => L("setup.stepBack"),
            Measure.SetupHint.MoveLeft => L("setup.moveLeft"),
            Measure.SetupHint.MoveRight => L("setup.moveRight"),
            Measure.SetupHint.None => floorStar.Visible ? L("setup.standOnStar") : null,
            _ => L("setup.stepIn"),
        };
    }
}
