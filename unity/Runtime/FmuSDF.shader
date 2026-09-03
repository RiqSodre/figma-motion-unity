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

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv0.xy;
                float2 b = i.uv0.zw;
                float r = min(i.uv1.x, min(b.x, b.y));

                float d = sdRoundBox(p, b, r);
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
