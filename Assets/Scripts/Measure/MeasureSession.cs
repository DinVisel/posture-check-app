using System.Collections.Generic;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// Turns per-frame values into reliable results (plan section 6):
    /// a value is the median over a still window (default 2 s) in which the body did not move,
    /// using only frames where that value was measurable (landmarks visible).
    /// A try lasts up to <c>tryTimeoutSeconds</c> of active time; after <c>maxTries</c> failed tries,
    /// the values still missing become "could not measure".
    /// Time does not count while the session is paused (child out of view, not on the star).
    /// </summary>
    public class MeasureSession
    {
        struct Sample
        {
            public float time;
            public float[] values; // per kind in m_Kinds; NaN = not measurable in this frame
        }

        readonly MeasureKind[] m_Kinds;
        readonly AppConfig.MeasurementSettings m_Settings;
        readonly List<Sample> m_Window = new();
        readonly Dictionary<MeasureKind, MeasureValue> m_Results = new();
        Vector2[] m_Anchor; // key points when the current still window started
        float m_LastTime = -1f;
        float m_TryTime;

        public MeasureSession(MeasureKind[] kinds, AppConfig.MeasurementSettings settings)
        {
            m_Kinds = (MeasureKind[])kinds.Clone();
            m_Settings = settings;
        }

        public IReadOnlyList<MeasureKind> Kinds => m_Kinds;
        /// <summary>Finished values so far; complete (every kind) once <see cref="Done"/>.</summary>
        public IReadOnlyDictionary<MeasureKind, MeasureValue> Results => m_Results;
        public bool Done { get; private set; }
        public int FailedTries { get; private set; }
        /// <summary>How full the current still window is, 0..1 (for a hold-still indicator).</summary>
        public float WindowProgress { get; private set; }
        /// <summary>Active seconds spent in the current try.</summary>
        public float TryTime => m_TryTime;

        /// <summary>
        /// Adds one pose frame. <paramref name="keyPoints"/> are screen positions used to detect movement
        /// (NaN for points not visible); <paramref name="scale"/> is the body size they are compared to
        /// (torso length in pixels); <paramref name="values"/> has one entry per kind (NaN = not measurable).
        /// </summary>
        public void Add(float time, Vector2[] keyPoints, float scale, float[] values, bool paused)
        {
            if (Done) return;
            float dt = m_LastTime < 0f ? 0f : Mathf.Clamp(time - m_LastTime, 0f, 0.25f);
            m_LastTime = time;
            if (paused || scale <= 0f)
            {
                ResetWindow();
                return;
            }

            m_TryTime += dt;
            if (m_Anchor == null || Moved(keyPoints, scale))
            {
                ResetWindow();
                m_Anchor = (Vector2[])keyPoints.Clone();
            }

            var sample = new Sample { time = time, values = new float[m_Kinds.Length] };
            for (int i = 0; i < m_Kinds.Length; i++) sample.values[i] = values[(int)m_Kinds[i]];
            m_Window.Add(sample);

            float span = time - m_Window[0].time;
            WindowProgress = Mathf.Clamp01(span / m_Settings.stillWindowSeconds);
            if (span >= m_Settings.stillWindowSeconds) Evaluate(time);

            if (!Done && m_TryTime >= m_Settings.tryTimeoutSeconds) FailTry();
        }

        bool Moved(Vector2[] points, float scale)
        {
            float limit = m_Settings.stillTolerance * scale;
            for (int i = 0; i < points.Length && i < m_Anchor.Length; i++)
            {
                var p = points[i];
                var a = m_Anchor[i];
                if (float.IsNaN(p.x) || float.IsNaN(a.x)) continue;
                if ((p - a).sqrMagnitude > limit * limit) return true;
            }
            return false;
        }

        /// <summary>The window spans a full still period: take the median of every value not yet done.</summary>
        void Evaluate(float now)
        {
            var valid = new List<float>(m_Window.Count);
            for (int k = 0; k < m_Kinds.Length; k++)
            {
                if (m_Results.ContainsKey(m_Kinds[k])) continue;
                valid.Clear();
                foreach (var s in m_Window)
                    if (!float.IsNaN(s.values[k])) valid.Add(s.values[k]);
                if (valid.Count >= 3 && valid.Count >= m_Settings.minValidFraction * m_Window.Count)
                    m_Results[m_Kinds[k]] = MeasureValue.Of(Median(valid));
            }

            if (m_Results.Count == m_Kinds.Length)
            {
                Done = true;
                return;
            }

            // Slide on: keep only the last window's worth of samples, so the missing values can still
            // be found while the child keeps standing still.
            while (m_Window.Count > 1 && now - m_Window[1].time >= m_Settings.stillWindowSeconds) m_Window.RemoveAt(0);
        }

        void FailTry()
        {
            FailedTries++;
            m_TryTime = 0f;
            ResetWindow();
            if (FailedTries < m_Settings.maxTries) return;
            foreach (var kind in m_Kinds)
                if (!m_Results.ContainsKey(kind)) m_Results[kind] = MeasureValue.CouldNotMeasure;
            Done = true;
        }

        void ResetWindow()
        {
            m_Window.Clear();
            m_Anchor = null;
            WindowProgress = 0f;
        }

        static float Median(List<float> values)
        {
            values.Sort();
            int n = values.Count;
            return n % 2 == 1 ? values[n / 2] : 0.5f * (values[n / 2 - 1] + values[n / 2]);
        }
    }
}
