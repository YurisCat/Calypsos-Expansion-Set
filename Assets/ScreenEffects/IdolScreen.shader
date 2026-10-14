Shader "CalypsoExpansionSet/IdolScreen"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Mode ("Effect", Float) = 0
        _PixelGrid ("Pixel grid", Vector) = (160,90,0,0)
        _Strength ("Strength", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _PixelGrid;
            float _Mode, _Strength;

            float luminance(float2 uv)
            {
                float2 blur = abs(_MainTex_TexelSize.xy) * 1.25;
                float3 color = tex2D(_MainTex, uv + blur).rgb
                    + tex2D(_MainTex, uv - blur).rgb
                    + tex2D(_MainTex, uv + blur * float2(1,-1)).rgb
                    + tex2D(_MainTex, uv + blur * float2(-1,1)).rgb;
                return dot(color * 0.25, float3(0.2126,0.7152,0.0722));
            }
            float4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                // This pass reads the final native backbuffer through a
                // CommandBuffer copy. D3D's backbuffer and shader UV origins
                // differ; compensate once when sampling that copy.
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1 - uv.y;
                #endif
                if (_Mode < 0.5)
                {
                    float2 grid = max(_PixelGrid.xy, float2(1,1));
                    return float4(tex2D(_MainTex, (floor(uv * grid) + 0.5) / grid).rgb, 1);
                }
                if (_Mode < 1.5)
                {
                    float2 t = abs(_MainTex_TexelSize.xy) * max(1, _Strength);
                    float a = luminance(uv + t * float2(-1, 1));
                    float b = luminance(uv + t * float2( 0, 1));
                    float c = luminance(uv + t * float2( 1, 1));
                    float d = luminance(uv + t * float2(-1, 0));
                    float f = luminance(uv + t * float2( 1, 0));
                    float g = luminance(uv + t * float2(-1,-1));
                    float h = luminance(uv + t * float2( 0,-1));
                    float i = luminance(uv + t * float2( 1,-1));
                    float2 edge = float2(c+2*f+i-a-2*d-g, a+2*b+c-g-2*h-i);
                    float white = smoothstep(0.12, 0.45, length(edge));
                    return float4(white,white,white,1);
                }
                // Radial barrel distortion fitted to a rectangular frame.
                // The frame boundary stays fixed, so all of the HUD is retained
                // without stretching clamped border pixels into long streaks.
                float2 p = uv * 2 - 1;
                float strength = clamp(_Strength, 0.01, 2.4);
                float boundary = (1 - p.x * p.x) * (1 - p.y * p.y);
                float2 distorted = p / (1 + strength * boundary);
                return float4(tex2D(_MainTex, distorted * 0.5 + 0.5).rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
