// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// FMU/UI SDF — resolution-independent UI shape shader.
// Renders a rounded box / ellipse from a signed distance field with derivative
// anti-aliasing (fwidth), so edges stay crisp at any scale — ideal for VR/world-space.
//
// Per-vertex data (written by FmuShapeGraphic):
//   TEXCOORD0 = (px, py, halfW, halfH)  pixel position relative to rect center + half-size
//   TEXCOORD1 = (radius, _, _, _)        corner radius in pixels
// The parent Canvas must expose TexCoord1; FmuShapeGraphic enables it automatically.
Shader "FMU/UI SDF"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float4 uv0    : TEXCOORD0; // (px, py, halfW, halfH)
                float4 uv1    : TEXCOORD1; // (radius, _, _, _)
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float4 uv0    : TEXCOORD0;
                float4 uv1    : TEXCOORD1;
            };

            fixed4 _Color;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                return o;
            }

            // Signed distance to a rounded box (Inigo Quilez).
            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // Signed distance to an arc / annular sector, symmetric about +y (Inigo Quilez).
            // sc = (sin, cos) of the half-aperture; ra = mid radius; rb = half thickness.
            float sdArc(float2 p, float2 sc, float ra, float rb)
            {
                p.x = abs(p.x);
                return ((sc.y * p.x > sc.x * p.y) ? length(p - sc * ra) : abs(length(p) - ra)) - rb;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv0.xy;
                float2 b = i.uv0.zw;
                float innerFrac = i.uv1.y;
                float arcCenter = i.uv1.z;
                float arcHalf   = i.uv1.w;

                float d;
                if (innerFrac <= 0.0001 && arcHalf >= 3.14159)
                {
                    // Solid rounded box / disc.
                    float r = min(i.uv1.x, min(b.x, b.y));
                    d = sdRoundBox(p, b, r);
                }
                else
                {
                    // Ring / arc: circle of radius R = min(b), carved to [Rin, R].
                    float R = min(b.x, b.y);
                    float Rin = innerFrac * R;
                    float ra = 0.5 * (R + Rin);
                    float rb = 0.5 * (R - Rin);
                    if (arcHalf >= 3.14159)
                    {
                        d = abs(length(p) - ra) - rb;               // full ring
                    }
                    else
                    {
                        float rot = 1.5707963 - arcCenter;          // bring arcCenter dir to +y
                        float cs = cos(rot), sn = sin(rot);
                        float2 pr = float2(cs * p.x - sn * p.y, sn * p.x + cs * p.y);
                        d = sdArc(pr, float2(sin(arcHalf), cos(arcHalf)), ra, rb);
                    }
                }

                float aa = fwidth(d) * 0.75 + 1e-5;
                float alpha = 1.0 - smoothstep(-aa, aa, d);

                fixed4 col = i.color;
                col.a *= alpha;
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
