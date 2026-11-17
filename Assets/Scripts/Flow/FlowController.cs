using System.Collections;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Runs the check's states in order: floor scan (back camera) → turn the phone → front camera
    /// with pose tracking. Step 1.7 adds setup hints, step 1.8 the start screen.
    /// </summary>
    public class FlowController : MonoBehaviour
    {
        public enum State { FloorScan, TurnPhone, ScanFront }

        [SerializeField] FloorScanner floorScanner;
        [SerializeField] TurnTracker turnTracker;
        [SerializeField] FrontCamera frontCamera;
        [Tooltip("The scene camera; switched off while the AR camera draws the back camera image.")]
        [SerializeField] Camera sceneCamera;
        [Tooltip("Editor only: go straight to the front camera (for testing pose and overlay on a webcam).")]
        [SerializeField] bool skipFloorScanInEditor;

        public State Current { get; private set; }
        /// <summary>The scanned floor in the front camera's view; not valid if the scan was skipped.</summary>
        public TurnTracker.FloorInFront Floor { get; private set; }

        IEnumerator Start()
        {
            // Portrait only, so the back and front camera views and the sensors share the same axes.
            Screen.orientation = ScreenOrientation.Portrait;

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

            Current = State.ScanFront;
            frontCamera.Begin();
        }
    }
}
