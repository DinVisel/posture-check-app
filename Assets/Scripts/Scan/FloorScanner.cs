using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace PostureCheck
{
    /// <summary>
    /// Floor scan with the back camera (step 1.5). Starts an AR session, finds horizontal planes,
    /// and casts a ray from the screen centre onto the floor; a preview star shows where the child
    /// will stand. The parent taps "Use this spot" or "Skip".
    /// The AR objects are built here at runtime and removed again by <see cref="Shutdown"/>, so the
    /// front camera can open afterwards. XR starts only here (not at app start).
    /// </summary>
    public class FloorScanner : MonoBehaviour
    {
        public enum Outcome { None, Found, Skipped, Unavailable }

        /// <summary>The scan result, in the coordinates of the phone's back camera at the moment the parent confirmed.</summary>
        public struct FloorResult
        {
            public Outcome outcome;
            /// <summary>The chosen spot (metres) relative to the back camera: x right, y up, z forward.</summary>
            public Vector3 spotInCamera;
            /// <summary>Floor "up" direction relative to the back camera.</summary>
            public Vector3 floorUpInCamera;
            /// <summary>Seconds from the start of the scan until the first floor plane appeared; -1 if none.</summary>
            public float secondsToFindFloor;
        }

        [SerializeField] Texture2D floorMarker;
        [Tooltip("Width of the star on the floor, metres.")]
        [SerializeField] float starSize = 0.5f;
        [Tooltip("Planes higher than the lowest one by more than this are ignored (tables, sofas).")]
        [SerializeField] float floorTolerance = 0.25f;
        [SerializeField] float noFloorHintSeconds = 5f;
        [Tooltip("Seconds to wait for the AR session to start (includes installing ARCore on Android).")]
        [SerializeField] float sessionStartTimeout = 30f;

        // Text lines from config.json ("scan.*"); step 1.8 reads them from the file.
        [SerializeField] string linePointAtFloor = "Point the camera at the floor where your child will stand.";
        [SerializeField] string lineNoFloor = "Point at the floor in a lit area.";
        [SerializeField] string lineFound = "Floor found!";
        [SerializeField] string lineUnavailable = "Floor scan is not available on this phone. Skipping it.";

        public FloorResult Result { get; private set; }
        public bool Running { get; private set; }

        GameObject m_ArRoot;
        Camera m_ArCamera;
        ARPlaneManager m_Planes;
        ARRaycastManager m_Raycaster;
        Transform m_Star;
        Canvas m_Canvas;
        Text m_Hint;
        Button m_UseButton;
        Outcome m_Choice;
        bool m_LoaderStartedHere;
        readonly List<ARRaycastHit> m_Hits = new();

        /// <summary>Runs the whole scan; finishes when the parent confirms or skips, or when AR is unavailable.</summary>
        public IEnumerator Run()
        {
            Running = true;
            Result = new FloorResult { outcome = Outcome.None, secondsToFindFloor = -1f };
            Ui.EnsureEventSystem();
            BuildUi();
            m_Hint.text = linePointAtFloor;

            yield return StartAr();
            if (m_ArRoot == null || !SessionStarted())
            {
                yield return Unavailable();
                yield break;
            }

            float start = Time.realtimeSinceStartup;
            float found = -1f;
            m_Choice = Outcome.None;
            while (m_Choice != Outcome.Skipped)
            {
                bool anyFloor = LowestFloorHeight(out float floorY);
                if (anyFloor && found < 0f)
                {
                    found = Time.realtimeSinceStartup - start;
                    Debug.Log($"FloorScanner: floor found after {found:0.0} s");
                }

                Pose pose = default;
                Vector3 up = Vector3.up;
                bool hit = anyFloor && RaycastFloor(floorY, out pose, out up);
                m_Star.gameObject.SetActive(hit);
                m_UseButton.interactable = hit;
                if (hit) PlaceStar(pose.position, up);

                if (hit) m_Hint.text = lineFound;
                else if (!anyFloor && Time.realtimeSinceStartup - start > noFloorHintSeconds) m_Hint.text = lineNoFloor;
                else m_Hint.text = linePointAtFloor;

                if (m_Choice == Outcome.Found && hit)
                {
                    var cam = m_ArCamera.transform;
                    Result = new FloorResult
                    {
                        outcome = Outcome.Found,
                        spotInCamera = cam.InverseTransformPoint(pose.position),
                        floorUpInCamera = cam.InverseTransformDirection(up).normalized,
                        secondsToFindFloor = found,
                    };
                    Debug.Log($"FloorScanner: spot {Result.spotInCamera} up {Result.floorUpInCamera} (camera space)");
                    break;
                }
                if (m_Choice == Outcome.Found) m_Choice = Outcome.None; // lost the floor at the moment of the tap
                yield return null;
            }

            if (m_Choice == Outcome.Skipped)
                Result = new FloorResult { outcome = Outcome.Skipped, secondsToFindFloor = found };
            Running = false;
        }

        IEnumerator Unavailable()
        {
            Debug.LogWarning("FloorScanner: AR is not available; skipping the floor scan.");
            m_Hint.text = lineUnavailable;
            m_UseButton.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(2f);
            Result = new FloorResult { outcome = Outcome.Unavailable, secondsToFindFloor = -1f };
            Running = false;
        }

        /// <summary>Removes the AR objects and stops XR so the camera is free for the front camera.</summary>
        public IEnumerator Shutdown()
        {
            if (m_Canvas != null) Destroy(m_Canvas.gameObject);
            if (m_ArRoot != null) Destroy(m_ArRoot);
            m_ArRoot = null;
            yield return null; // let the AR components disable and stop their subsystems

            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (m_LoaderStartedHere && manager != null && manager.activeLoader != null)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            m_LoaderStartedHere = false;
            yield return null;
        }

        IEnumerator StartAr()
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null) yield break;
            if (manager.activeLoader == null)
            {
                yield return manager.InitializeLoader();
                if (manager.activeLoader == null) yield break;
                m_LoaderStartedHere = true;
            }

            BuildAr();
            // ARSession starts itself and installs "Google Play Services for AR" if needed; wait for that.
            float deadline = Time.realtimeSinceStartup + sessionStartTimeout;
            while (!SessionStarted() && ARSession.state != ARSessionState.Unsupported &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        static bool SessionStarted() =>
            ARSession.state == ARSessionState.SessionInitializing || ARSession.state == ARSessionState.SessionTracking;

        bool LowestFloorHeight(out float y)
        {
            y = float.MaxValue;
            foreach (var plane in m_Planes.trackables)
                if (plane.alignment == PlaneAlignment.HorizontalUp && plane.trackingState != TrackingState.None)
                    y = Mathf.Min(y, plane.center.y);
            return y < float.MaxValue;
        }

        bool RaycastFloor(float floorY, out Pose pose, out Vector3 up)
        {
            pose = default;
            up = Vector3.up;
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!m_Raycaster.Raycast(centre, m_Hits, TrackableType.PlaneWithinPolygon)) return false;
            foreach (var hit in m_Hits) // sorted nearest first
            {
                var plane = m_Planes.GetPlane(hit.trackableId);
                if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp) continue;
                if (hit.pose.position.y > floorY + floorTolerance) continue; // table, sofa, bed
                pose = hit.pose;
                up = plane.normal;
                return true;
            }
            return false;
        }

        void PlaceStar(Vector3 position, Vector3 up)
        {
            // Lie flat on the floor, with the star's top pointing away from the phone.
            var away = Vector3.ProjectOnPlane(position - m_ArCamera.transform.position, up);
            if (away.sqrMagnitude < 1e-4f) away = Vector3.ProjectOnPlane(m_ArCamera.transform.forward, up);
            m_Star.SetPositionAndRotation(position + up * 0.005f, Quaternion.LookRotation(-up, away));
        }

        void BuildAr()
        {
            m_ArRoot = new GameObject("AR");
            m_ArRoot.SetActive(false);
            m_ArRoot.transform.SetParent(transform, false);

            var session = new GameObject("AR Session");
            session.transform.SetParent(m_ArRoot.transform, false);
            session.AddComponent<ARSession>();
            session.AddComponent<ARInputManager>();

            var originGo = new GameObject("XR Origin");
            originGo.transform.SetParent(m_ArRoot.transform, false);
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);
            var camGo = new GameObject("AR Camera");
            camGo.transform.SetParent(offset.transform, false);

            m_ArCamera = camGo.AddComponent<Camera>();
            m_ArCamera.clearFlags = CameraClearFlags.SolidColor;
            m_ArCamera.backgroundColor = Color.black;
            m_ArCamera.nearClipPlane = 0.05f;
            m_ArCamera.farClipPlane = 30f;
            camGo.AddComponent<ARCameraManager>();
            camGo.AddComponent<ARCameraBackground>();
            ConfigurePoseDriver(camGo.AddComponent<TrackedPoseDriver>());

            var origin = originGo.AddComponent<XROrigin>();
            origin.Origin = originGo;
            origin.CameraFloorOffsetObject = offset;
            origin.Camera = m_ArCamera;
            m_Planes = originGo.AddComponent<ARPlaneManager>();
            m_Planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            m_Raycaster = originGo.AddComponent<ARRaycastManager>();

            m_Star = BuildStar(originGo.transform);
            m_Star.gameObject.SetActive(false);

            m_ArRoot.SetActive(true);
        }

        static void ConfigurePoseDriver(TrackedPoseDriver driver)
        {
            // Same bindings as AR Foundation's "XR Origin (Mobile AR)": phones report a handheld AR device.
            var position = new InputAction("Position", expectedControlType: "Vector3");
            position.AddBinding("<XRHMD>/centerEyePosition");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotation = new InputAction("Rotation", expectedControlType: "Quaternion");
            rotation.AddBinding("<XRHMD>/centerEyeRotation");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            var state = new InputAction("Tracking State", expectedControlType: "Integer");
            state.AddBinding("<XRHMD>/trackingState");
            state.AddBinding("<HandheldARInputDevice>/trackingState");

            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.positionInput = new InputActionProperty(position);
            driver.rotationInput = new InputActionProperty(rotation);
            driver.trackingStateInput = new InputActionProperty(state);
        }

        Transform BuildStar(Transform parent)
        {
            // A world-space UI image: the UI shader is always in the build, unlike sprite/unlit shaders.
            var go = new GameObject("Star Preview", typeof(Canvas));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = m_ArCamera;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = Vector2.one;
            rt.localScale = Vector3.one * starSize;

            var img = new GameObject("Star", typeof(Image));
            img.transform.SetParent(go.transform, false);
            var image = img.GetComponent<Image>();
            image.raycastTarget = false;
            if (floorMarker != null)
                image.sprite = Sprite.Create(floorMarker, new Rect(0, 0, floorMarker.width, floorMarker.height), new Vector2(0.5f, 0.5f));
            else
                image.color = new Color(1f, 0.85f, 0.2f);
            image.rectTransform.sizeDelta = Vector2.one;
            return go.transform;
        }

        void BuildUi()
        {
            m_Canvas = Ui.Canvas("ScanCanvas", transform, 20);
            var root = m_Canvas.transform;

            Ui.Panel(root, "HintBackground", new Vector2(0.5f, 0.9f), new Vector2(1000, 220), new Color(0, 0, 0, 0.55f));
            m_Hint = Ui.Text(root, "Hint", new Vector2(0.5f, 0.9f), new Vector2(940, 200), 52);

            // Aiming ring in the centre: the spot under it is the one that gets chosen.
            Ui.Panel(root, "Reticle", new Vector2(0.5f, 0.5f), new Vector2(24, 24), Color.white);

            var skip = Ui.Button(root, "Skip", "Skip", new Vector2(0.25f, 0.08f), new Vector2(400, 140));
            skip.onClick.AddListener(() => m_Choice = Outcome.Skipped);
            m_UseButton = Ui.Button(root, "UseSpot", "Use this spot", new Vector2(0.72f, 0.08f), new Vector2(520, 140));
            m_UseButton.onClick.AddListener(() => m_Choice = Outcome.Found);
            m_UseButton.interactable = false;
        }

        void OnDestroy()
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (m_LoaderStartedHere && manager != null && manager.activeLoader != null)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
        }
    }
}
