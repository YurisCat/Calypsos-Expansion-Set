Shader "CalypsoExpansionSet/SharedPing"
{
    Properties { _MainTex ("Silhouette", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4x4 _CESPingVP;
        float4 _CESPingScreen, _CESPingPoint, _CESPingColor, _CESPingRect;
        float4 _CESPingDistortion1, _CESPingDistortion2;

        float2 distortedUV(float2 uv)
        {
            // Match URP UberPost's lens mapping using its actual material parameters.
            if (_CESPingDistortion2.w == 0) return uv;
            uv = (uv - .5) * _CESPingDistortion2.z + .5;
            float2 ruv = _CESPingDistortion1.zw * (uv - .5 - _CESPingDistortion1.xy);
            float ru = max(length(ruv), 1e-6);
            float scale = _CESPingDistortion2.w > 0
                ? tan(ru * _CESPingDistortion2.x) / (ru * _CESPingDistortion2.y + 0.00006103515625)
                : _CESPingDistortion2.x * atan(ru * _CESPingDistortion2.y) / ru;
            return uv + ruv * (scale - 1);
        }

        float silhouette(float2 uv)
        {
            uv = distortedUV(uv);
            if (any(uv < 0) || any(uv > 1)) return 0;
            return tex2D(_MainTex, uv).r;
        }

        v2f_img regionVertex(appdata_img input)
        {
            v2f_img output = vert_img(input);
            float2 uv = output.uv;
            float2 region = (_CESPingRect.xy + uv * _CESPingRect.zw) / _CESPingScreen.xy;
            // Keep the native Blit orientation for either a texture or the
            // backbuffer, while rasterizing only the marked screen region.
            output.pos.xy = (region * 2 - 1) * (output.pos.xy / (uv * 2 - 1));
            output.uv = region;
            return output;
        }

        float2 screenPoint(v2f_img input)
        {
            return input.uv * _CESPingScreen.xy;
        }
        float dilation(float2 uv, float radius)
        {
            float2 t = radius / _CESPingScreen.xy;
            float value = 0;
            [unroll] for (int x = -1; x <= 1; x++)
                [unroll] for (int y = -1; y <= 1; y++)
                    value = max(value, silhouette(uv + t * float2(x,y)));
            return value;
        }
        ENDHLSL
        // All surfaces enter this private mask regardless of scene depth.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex maskVertex
            #pragma fragment maskFragment
            #pragma target 3.0
            float4 maskVertex(float4 vertex : POSITION) : SV_POSITION
            {
                float4 clip = mul(_CESPingVP, mul(unity_ObjectToWorld, vertex));
                // Keep the real horizontal/vertical projection but ignore the
                // native near/far planes. No game camera fields are changed.
                clip.z = clip.w * 0.5;
                return clip;
            }
            float4 maskFragment() : SV_Target { return 1; }
            ENDHLSL
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment outlineFragment
            #pragma target 3.0
            float4 outlineFragment(v2f_img input) : SV_Target
            {
                // Blit UVs preserve texture orientation for both RTs and the backbuffer.
                float2 uv = input.uv;
                float center = silhouette(uv);
                float inner = saturate(dilation(uv, _CESPingScreen.z) - center);
                float outer = saturate(dilation(uv, _CESPingScreen.z + 1) - center);
                return float4(lerp(float3(.025,.025,.025), _CESPingColor.rgb, inner), outer * _CESPingColor.a);
            }
            ENDHLSL
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex regionVertex
            #pragma fragment iconFragment
            #pragma target 3.0
            float4 iconFragment(v2f_img input) : SV_Target
            {
                float2 p = (screenPoint(input) - _CESPingPoint.xy) / _CESPingScreen.w;
                float shape;
                if (_CESPingPoint.z > .5)
                {
                    float2 direction = normalize(_CESPingPoint.xy - _CESPingScreen.xy * .5);
                    p = float2(dot(p, float2(direction.y, -direction.x)), dot(p, direction));
                    // A chevron facing the offscreen target.
                    shape = max(abs(abs(p.x) + p.y - .45) - .17, abs(p.x) - .65);
                }
                else
                {
                    // Ring plus tapered tail; the tip touches the marked point.
                    float ring = abs(length(p - float2(0,.95)) - .48) - .17;
                    float tail = max(abs(p.x) - p.y * .46, max(-p.y, p.y - .75));
                    shape = min(ring, tail);
                }
                float aa = max(fwidth(shape), .02);
                float body = 1 - smoothstep(-aa, aa, shape);
                float rim = 1 - smoothstep(.07 - aa, .07 + aa, shape);
                return float4(lerp(float3(.025,.025,.025), _CESPingColor.rgb, body), rim * _CESPingColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
