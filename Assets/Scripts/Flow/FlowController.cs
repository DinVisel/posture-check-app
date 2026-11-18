using System.Collections;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Runs the check's states in order: start screen → floor scan (back camera) → turn the phone →
    /// setup (hints until the whole body is in view, star on the floor) → front scan (posture line,
    /// front measurements over a still window) → side scan (turn sideways, head forward) → done.
    /// </summary>
    public class FlowController : MonoBehaviour
    {
        public enum State { Start, FloorScan, TurnPhone, Setup, ScanFront, ScanSide, Done }

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
        static readonly MeasureKind[] SideKinds = { MeasureKind.HeadForward };

        float m_StarWait;

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
            // Front: the child stands still on the star.
            yield return RunScan(State.ScanFront, FrontKinds, FrontNotReady, "front", giveUpSeconds: 0f);

            // Side: the child turns sideways and stands still.
            measure.SideView = true;
            yield return RunScan(State.ScanSide, SideKinds, SideNotReady, "side",
                                 AppConfig.Current.measurement.sideTurnTimeoutSeconds);
            LogResults();

            // Guided poses and walking come next (Phase 2 steps 2.4–2.6); until then the live line stays on.
            while (true)
            {
                Current = measure.BodyInView ? State.Done : State.Setup;
                mascot.Say(Current == State.Setup ? HintLine(measure.Hint) : L("side.done"));
                yield return null;
            }
        }

        /// <summary>
        /// One measuring step: setup hints until the whole body is in view, then <paramref name="notReady"/>'s
        /// line until the child is in place (null = ready), then hold still until every value has a median
        /// or is "could not measure". Leaving the view or the position pauses the measuring.
        /// Lines come from "<paramref name="lines"/>.holdStill" and "<paramref name="lines"/>.tryAgain".
        /// If the child is in view but not in place for <paramref name="giveUpSeconds"/> in total (0 = never),
        /// the missing values become "could not measure".
        /// </summary>
        IEnumerator RunScan(State state, MeasureKind[] kinds, System.Func<string> notReady, string lines, float giveUpSeconds)
        {
            MeasureSession session = null;
            float tryAgainUntil = 0f, notReadyTime = 0f;
            int seenFails = 0;
            while (session == null || !session.Done)
            {
                bool inView = measure.BodyInView;
                string block = inView ? notReady() : null;
                Current = inView ? state : State.Setup;
                if (block != null)
                {
                    notReadyTime += Time.deltaTime;
                    if (giveUpSeconds > 0f && notReadyTime >= giveUpSeconds)
                    {
                        Debug.Log($"Flow: {state} gave up waiting for the child to get in place");
                        break;
                    }
                }
                if (inView && block == null && session == null) session = measure.BeginSession(kinds);
                measure.SessionPaused = !inView || block != null;

                if (session != null && session.FailedTries > seenFails)
                {
                    seenFails = session.FailedTries;
                    tryAgainUntil = Time.time + 2.5f;
                }

                mascot.Say(!inView ? HintLine(measure.Hint)
                         : block ?? (Time.time < tryAgainUntil ? L(lines + ".tryAgain") : L(lines + ".holdStill")));
                yield return null;
            }

            foreach (var kind in kinds)
                m_Results[kind] = session != null && session.Results.TryGetValue(kind, out var v) ? v : MeasureValue.CouldNotMeasure;
        }

        /// <summary>Front scan: wait for the child to stand on the star, but not forever, since the star may be a little off.</summary>
        string FrontNotReady()
        {
            if (!floorStar.Visible || floorStar.FeetOnStar) return null;
            m_StarWait += Time.deltaTime;
            return m_StarWait < starWaitSeconds ? L("setup.standOnStar") : null;
        }

        string SideNotReady() => measure.IsSideways ? null : L("side.turn");

        void LogResults()
        {
            var t = AppConfig.Current.thresholds;
            var log = new System.Text.StringBuilder("Flow: check results");
            foreach (var kv in m_Results)
                log.Append($", {kv.Key} {kv.Value.Format(kv.Key)}{(kv.Value.IsFlagged(kv.Key, t) ? " (flag)" : "")}");
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
