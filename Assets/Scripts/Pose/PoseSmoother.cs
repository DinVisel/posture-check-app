using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Smooths the landmark positions from <see cref="PoseTracker"/> with a One Euro filter per point.
    /// Filtering happens in units of frame height, so the settings work for any camera resolution.
    /// Visibility and presence are passed through unchanged.
    /// </summary>
    public class PoseSmoother : MonoBehaviour
    {
        [SerializeField] PoseTracker poseTracker;
        [SerializeField] FrontCamera frontCamera;
        [Tooltip("Hz. Lower = steadier when the body is still.")]
        [SerializeField] float minCutoff = 1f;
        [Tooltip("Higher = less lag during fast movement.")]
        [SerializeField] float beta = 20f;

        public Landmark[] Landmarks { get; } = new Landmark[PoseTracker.LandmarkCount];
        public bool HasPose { get; private set; }
        /// <summary>Increases by one every time <see cref="Landmarks"/> is updated.</summary>
        public int Version { get; private set; }

        OneEuroFilter[] m_Filters;
        int m_LastTrackerVersion = -1;
        float m_LastTime;

        void Awake()
        {
            m_Filters = new OneEuroFilter[PoseTracker.LandmarkCount];
            for (int i = 0; i < m_Filters.Length; i++) m_Filters[i] = new OneEuroFilter(minCutoff, beta);
        }

        void Update()
        {
            if (poseTracker.Version == m_LastTrackerVersion) return;
            m_LastTrackerVersion = poseTracker.Version;

            if (!poseTracker.HasPose || frontCamera.Frame == null)
            {
                if (HasPose) foreach (var f in m_Filters) f.Reset();
                HasPose = false;
                Version++;
                return;
            }

            float now = Time.realtimeSinceStartup;
            float dt = HasPose ? now - m_LastTime : 0f;
            m_LastTime = now;
            float unit = frontCamera.Frame.height;

            for (int i = 0; i < Landmarks.Length; i++)
            {
                var f = m_Filters[i];
                f.MinCutoff = minCutoff;
                f.Beta = beta;
                var raw = poseTracker.Landmarks[i];
                raw.position = f.Filter(raw.position / unit, dt) * unit;
                Landmarks[i] = raw;
            }
            HasPose = true;
            Version++;
        }

        public Landmark this[BodyPoint p] => Landmarks[(int)p];
    }
}
