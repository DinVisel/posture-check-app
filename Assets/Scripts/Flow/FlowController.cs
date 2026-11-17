using System.Collections;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Runs the check's states in order: floor scan (back camera) → turn the phone → setup (hints
    /// until the whole body is in view, star on the floor) → front scan (posture line).
    /// Step 1.8 adds the start screen.
    /// </summary>
    public class FlowController : MonoBehaviour
    {
        public enum State { FloorScan, TurnPhone, Setup, ScanFront }

        [SerializeField] FloorScanner floorScanner;
        [SerializeField] TurnTracker turnTracker;
        [SerializeField] FrontCamera frontCamera;
        [SerializeField] Measure measure;
        [SerializeField] FloorStar floorStar;
        [SerializeField] Mascot mascot;
        [Tooltip("The scene camera; switched off while the AR camera draws the back camera image.")]
        [SerializeField] Camera sceneCamera;
        [Tooltip("Editor only: go straight to the front camera (for testing pose and overlay on a webcam).")]
        [SerializeField] bool skipFloorScanInEditor;

        // Text lines from config.json ("setup.*"); step 1.8 reads them from the file.
        [SerializeField] string lineStepIn = "Step in front of the camera.";
        [SerializeField] string lineStepBack = "Step back";
        [SerializeField] string lineMoveLeft = "Move left";
        [SerializeField] string lineMoveRight = "Move right";
        [SerializeField] string lineStandOnStar = "Stand on the star!";

        public State Current { get; private set; }
        /// <summary>The scanned floor in the front camera's view; not valid if the scan was skipped.</summary>
        public TurnTracker.FloorInFront Floor { get; private set; }

        IEnumerator Start()
        {
            // Portrait only, so the back and front camera views and the sensors share the same axes.
            Screen.orientation = ScreenOrientation.Portrait;
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
                         : floorStar.Visible && !floorStar.FeetOnStar ? lineStandOnStar
                         : null);
                yield return null;
            }
        }

        string HintLine(Measure.SetupHint hint) => hint switch
        {
            Measure.SetupHint.StepBack => lineStepBack,
            Measure.SetupHint.MoveLeft => lineMoveLeft,
            Measure.SetupHint.MoveRight => lineMoveRight,
            Measure.SetupHint.None => floorStar.Visible ? lineStandOnStar : null,
            _ => lineStepIn,
        };
    }
}
