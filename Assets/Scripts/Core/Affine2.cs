using UnityEngine;

namespace PostureCheck
{
    /// <summary>
    /// 2D affine transform: x' = a*x + b*y + tx, y' = c*x + d*y + ty.
    /// Used to map between camera image space and model tensor space.
    /// </summary>
    public readonly struct Affine2
    {
        public readonly float a, b, c, d, tx, ty;

        public Affine2(float a, float b, float c, float d, float tx, float ty)
        {
            this.a = a; this.b = b; this.c = c; this.d = d; this.tx = tx; this.ty = ty;
        }

        public static Affine2 Identity => new(1, 0, 0, 1, 0, 0);
        public static Affine2 Translate(Vector2 t) => new(1, 0, 0, 1, t.x, t.y);
        public static Affine2 Scale(float sx, float sy) => new(sx, 0, 0, sy, 0, 0);

        /// <summary>Counter-clockwise rotation (y axis pointing up).</summary>
        public static Affine2 Rotate(float radians)
        {
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return new(cos, -sin, sin, cos, 0, 0);
        }

        /// <summary>Composition: (m * n)(p) == m(n(p)).</summary>
        public static Affine2 operator *(Affine2 m, Affine2 n) => new(
            m.a * n.a + m.b * n.c, m.a * n.b + m.b * n.d,
            m.c * n.a + m.d * n.c, m.c * n.b + m.d * n.d,
            m.a * n.tx + m.b * n.ty + m.tx, m.c * n.tx + m.d * n.ty + m.ty);

        public Vector2 Apply(Vector2 p) => new(a * p.x + b * p.y + tx, c * p.x + d * p.y + ty);

        /// <summary>Rows packed for shaders: (a, b, tx, 0) and (c, d, ty, 0).</summary>
        public Vector4 Row0 => new(a, b, tx, 0);
        public Vector4 Row1 => new(c, d, ty, 0);
    }
}
