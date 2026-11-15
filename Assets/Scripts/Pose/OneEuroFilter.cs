using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// One Euro filter (Casiez et al., 2012) for a 2D point: strong smoothing when the point is
    /// still (removes jitter), little smoothing when it moves fast (keeps up with movement).
    /// </summary>
    public class OneEuroFilter
    {
        public float MinCutoff;   // Hz. Lower = smoother when still.
        public float Beta;        // How fast the cutoff rises with speed. Higher = less lag when moving.
        public float DerivativeCutoff = 1f;

        Vector2 m_Value, m_Derivative;
        bool m_HasValue;

        public OneEuroFilter(float minCutoff, float beta)
        {
            MinCutoff = minCutoff;
            Beta = beta;
        }

        public void Reset() => m_HasValue = false;

        public Vector2 Filter(Vector2 value, float dt)
        {
            if (!m_HasValue || dt <= 0f)
            {
                m_Value = value;
                m_Derivative = Vector2.zero;
                m_HasValue = true;
                return value;
            }

            var derivative = (value - m_Value) / dt;
            m_Derivative = Vector2.Lerp(m_Derivative, derivative, Alpha(DerivativeCutoff, dt));
            float cutoff = MinCutoff + Beta * m_Derivative.magnitude;
            m_Value = Vector2.Lerp(m_Value, value, Alpha(cutoff, dt));
            return m_Value;
        }

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }
}
