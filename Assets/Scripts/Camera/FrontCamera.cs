using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace PostureCheck
{
    /// <summary>
    /// Opens the front camera and produces <see cref="Frame"/>: an upright, unmirrored copy of each
    /// camera image. Pose tracking reads <see cref="Frame"/>; the screen shows it mirrored like a
    /// selfie and scaled to fill the screen. <see cref="FrameToScreen"/> maps a pixel in
    /// <see cref="Frame"/> (y up) to a screen position (y up).
    /// </summary>
    public class FrontCamera : MonoBehaviour
    {
        [SerializeField] Shader uprightBlitShader;
        [SerializeField] Vector2Int requestedSize = new(1280, 720);
        [SerializeField] int requestedFps = 30;
        [Tooltip("Show the image mirrored, like a selfie.")]
        [SerializeField] bool mirrorDisplay = true;
        [Tooltip("Extra clockwise rotation (0/90/180/270) if a phone reports its camera angle wrongly.")]
        [SerializeField] int extraRotation;

        public enum CameraState { Idle, Starting, Running, PermissionDenied, NoCamera }

        public CameraState State { get; private set; } = CameraState.Idle;
        /// <summary>Upright, unmirrored camera image (sRGB). Null until the first frame.</summary>
        public RenderTexture Frame { get; private set; }
        /// <summary>Increases by one every time <see cref="Frame"/> gets a new camera image.</summary>
        public int FrameCount { get; private set; }
        public bool MirrorDisplay => mirrorDisplay;

        WebCamTexture m_Cam;
        Material m_Blit;
        RawImage m_View;
        int m_Rotation = -1;
        bool m_FlipY;

        /// <summary>Opens the front camera. Called by the flow once the back camera (AR) is released.</summary>
        public void Begin()
        {
            if (State != CameraState.Idle) return;
            State = CameraState.Starting;
            StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            yield return RequestPermission();
            if (State == CameraState.PermissionDenied) yield break;

            var device = FindFrontCamera();
            if (device == null) { State = CameraState.NoCamera; Debug.LogWarning("No camera found"); yield break; }

            m_Cam = new WebCamTexture(device.Value.name, requestedSize.x, requestedSize.y, requestedFps);
            m_Cam.Play();
            m_Blit = new Material(uprightBlitShader);
            CreateView();
            State = CameraState.Running;
        }

        IEnumerator RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string perm = UnityEngine.Android.Permission.Camera;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(perm))
            {
                bool? granted = null;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => granted = true;
                callbacks.PermissionDenied += _ => granted = false;
                UnityEngine.Android.Permission.RequestUserPermission(perm, callbacks);
                while (granted == null) yield return null;
                if (granted == false) State = CameraState.PermissionDenied;
            }
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                State = CameraState.PermissionDenied;
#endif
        }

        static WebCamDevice? FindFrontCamera()
        {
            var devices = WebCamTexture.devices;
            foreach (var d in devices) if (d.isFrontFacing) return d;
            // Editor / desktop: webcams usually don't report a facing, so use the first one.
            return devices.Length > 0 ? devices[0] : null;
        }

        void CreateView()
        {
            var canvasGo = new GameObject("CameraCanvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100; // behind every other UI

            var viewGo = new GameObject("CameraView", typeof(RawImage));
            viewGo.transform.SetParent(canvasGo.transform, false);
            m_View = viewGo.GetComponent<RawImage>();
            m_View.raycastTarget = false;
            var rt = m_View.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        }

        void Update()
        {
            if (m_Cam == null || !m_Cam.didUpdateThisFrame || m_Cam.width < 32) return;

            int rotation = (((m_Cam.videoRotationAngle + extraRotation) % 360) + 360) % 360;
            bool flipY = m_Cam.videoVerticallyMirrored;
            bool sideways = rotation == 90 || rotation == 270;
            int w = sideways ? m_Cam.height : m_Cam.width;
            int h = sideways ? m_Cam.width : m_Cam.height;

            if (Frame == null || Frame.width != w || Frame.height != h || rotation != m_Rotation || flipY != m_FlipY)
                Rebuild(w, h, rotation, flipY);

            Graphics.Blit(m_Cam, Frame, m_Blit);
            FrameCount++;
            LayoutView();
        }

        void Rebuild(int w, int h, int rotation, bool flipY)
        {
            m_Rotation = rotation;
            m_FlipY = flipY;
            if (Frame != null) Frame.Release();
            Frame = new RenderTexture(w, h, 0, GraphicsFormat.R8G8B8A8_SRGB) { name = "CameraFrame" };
            Frame.Create();
            m_View.texture = Frame;

            // Output UV -> source UV. The display convention (Unity docs) is: rotate the raw image
            // clockwise by videoRotationAngle and flip it vertically if videoVerticallyMirrored.
            // So raw = flipY * rotate(+angle) * upright, around the image centres.
            var srcSize = new Vector2(m_Cam.width, m_Cam.height);
            var dstSize = new Vector2(w, h);
            var m = Affine2.Scale(1f / srcSize.x, 1f / srcSize.y)
                    * Affine2.Translate(srcSize * 0.5f)
                    * Affine2.Scale(1, flipY ? -1 : 1)
                    * Affine2.Rotate(rotation * Mathf.Deg2Rad)
                    * Affine2.Translate(-dstSize * 0.5f)
                    * Affine2.Scale(dstSize.x, dstSize.y);
            m_Blit.SetVector("_Row0", m.Row0);
            m_Blit.SetVector("_Row1", m.Row1);
        }

        void LayoutView()
        {
            float k = FillScale();
            var rt = m_View.rectTransform;
            rt.sizeDelta = new Vector2(Frame.width, Frame.height) * k / m_View.canvas.scaleFactor;
            rt.localScale = new Vector3(mirrorDisplay ? -1 : 1, 1, 1);
        }

        float FillScale() => Mathf.Max((float)Screen.width / Frame.width, (float)Screen.height / Frame.height);

        /// <summary>Maps a pixel position in <see cref="Frame"/> (y up) to screen pixels (y up).</summary>
        public Vector2 FrameToScreen(Vector2 p)
        {
            if (Frame == null) return p;
            float k = FillScale();
            var d = (p - new Vector2(Frame.width, Frame.height) * 0.5f) * k;
            if (mirrorDisplay) d.x = -d.x;
            return new Vector2(Screen.width, Screen.height) * 0.5f + d;
        }

        /// <summary>Scale from <see cref="Frame"/> pixels to screen pixels.</summary>
        public float FrameToScreenScale => Frame == null ? 1f : FillScale();

        void OnDestroy()
        {
            if (m_Cam != null) m_Cam.Stop();
            if (Frame != null) Frame.Release();
            if (m_Blit != null) Destroy(m_Blit);
        }
    }
}
