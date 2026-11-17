using System.Collections;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Runs the check's states in order. Step 1.5 has two: the floor scan with the back camera,
    /// then the front camera with pose tracking. Step 1.6 adds "turn the phone" in between,
    /// step 1.8 the start screen and setup.
    /// </summary>
    public class FlowController : MonoBehaviour
    {
        public enum State { FloorScan, ScanFront }

        [SerializeField] FloorScanner floorScanner;
        [SerializeField] FrontCamera frontCamera;
        [Tooltip("The scene camera; switched off while the AR camera draws the back camera image.")]
        [SerializeField] Camera sceneCamera;
        [Tooltip("Editor only: go straight to the front camera (for testing pose and overlay on a webcam).")]
        [SerializeField] bool skipFloorScanInEditor;

        public State Current { get; private set; }

        IEnumerator Start()
        {
            bool scan = !(Application.isEditor && skipFloorScanInEditor);
            if (scan)
            {
                Current = State.FloorScan;
                if (sceneCamera != null) sceneCamera.enabled = false;
                yield return floorScanner.Run();
                var r = floorScanner.Result;
                Debug.Log($"Flow: floor scan {r.outcome}" + (r.secondsToFindFloor >= 0 ? $", floor after {r.secondsToFindFloor:0.0} s" : ""));
                yield return floorScanner.Shutdown();
                if (sceneCamera != null) sceneCamera.enabled = true;
            }

            Current = State.ScanFront;
            frontCamera.Begin();
        }
    }
}
