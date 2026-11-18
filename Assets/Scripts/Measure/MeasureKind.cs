namespace PostureCheck
{
    /// <summary>The values the check measures (plan section 6).</summary>
    public enum MeasureKind
    {
        /// <summary>Degrees from horizontal; positive = the person's left shoulder is higher.</summary>
        ShoulderTilt,
        /// <summary>Degrees from horizontal; positive = the person's left hip is higher.</summary>
        HipTilt,
        /// <summary>Degrees the left leg bends at the knee in the front view; 0 = straight, positive = knee towards the midline (knock-knee), negative = outwards (bow-leg).</summary>
        KneeLeft,
        /// <summary>Same as <see cref="KneeLeft"/> for the right leg.</summary>
        KneeRight,
        /// <summary>Side view: how far the ear sits in front of the shoulder (horizontally), divided by torso length. Positive = forward.</summary>
        HeadForward,
    }

    /// <summary>One measurement result: a value, or "could not measure" (never a guess).</summary>
    public readonly struct MeasureValue
    {
        public readonly bool measured;
        public readonly float value;

        MeasureValue(bool measured, float value)
        {
            this.measured = measured;
            this.value = value;
        }

        public static MeasureValue Of(float value) => new(true, value);
        public static MeasureValue CouldNotMeasure => new(false, float.NaN);

        /// <summary>True when the value is past its (placeholder) threshold in config.json.</summary>
        public bool IsFlagged(MeasureKind kind, AppConfig.ThresholdSettings t)
        {
            if (!measured) return false;
            switch (kind)
            {
                case MeasureKind.ShoulderTilt: return System.Math.Abs(value) > t.shoulderTiltDeg;
                case MeasureKind.HipTilt: return System.Math.Abs(value) > t.hipTiltDeg;
                case MeasureKind.KneeLeft:
                case MeasureKind.KneeRight: return value < t.kneeMinDeg || value > t.kneeMaxDeg;
                case MeasureKind.HeadForward: return value > t.headForwardMax;
                default: return false;
            }
        }

        /// <summary>The value with its unit: degrees, or a ratio of torso length for head forward.</summary>
        public string Format(MeasureKind kind) => !measured ? "could not measure" : Format(kind, value);

        public static string Format(MeasureKind kind, float value) =>
            float.IsNaN(value) ? "-" : kind == MeasureKind.HeadForward ? $"{value:+0.00;-0.00}" : $"{value:+0.0;-0.0}°";

        public override string ToString() => measured ? value.ToString("0.00") : "could not measure";
    }
}
