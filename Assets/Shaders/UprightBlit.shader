// Copies the raw camera texture into an upright, unmirrored frame.
// _Row0/_Row1: affine transform from output UV (0..1) to source UV (0..1).
Shader "Hidden/PostureCheck/UprightBlit"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Row0;
            float4 _Row1;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = float2(dot(_Row0.xy, i.uv) + _Row0.z, dot(_Row1.xy, i.uv) + _Row1.z);
                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
