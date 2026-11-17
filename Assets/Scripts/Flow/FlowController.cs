using System.Collections;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Runs the check's states in order: start screen → floor scan (back camera) → turn the phone →
    /// setup (hints until the whole body is in view, star on the floor) → front scan (posture line).
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

        static string L(string key) => AppConfig.Current.Line(key);

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
            while (true)
            {
                // Setup until the whole body is in view; back to setup if the child leaves the view.
                Current = measure.BodyInView ? State.ScanFront : State.Setup;
                mascot.Say(Current == State.Setup ? HintLine(measure.Hint)
                         : floorStar.Visible && !floorStar.FeetOnStar ? L("setup.standOnStar")
                         : null);
                yield return null;
            }
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
