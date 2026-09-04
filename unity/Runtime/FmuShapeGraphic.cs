// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// FmuShapeGraphic — a UI Graphic that draws a rounded rectangle or ellipse via the
// FMU/UI SDF shader. Resolution-independent (crisp at any zoom), and rebuilds its mesh
// when the RectTransform resizes, so animated HEIGHT/WIDTH keep correct corners.
using UnityEngine;
using UnityEngine.UI;

namespace Fmu
{
    public enum FmuShapeType { RoundedRect, Ellipse }

    [RequireComponent(typeof(CanvasRenderer))]
    public class FmuShapeGraphic : MaskableGraphic
    {
        public FmuShapeType shape = FmuShapeType.RoundedRect;
        public float cornerRadius = 0f;   // pixels; ignored for Ellipse (uses min half-extent)

        // Ring / arc (ellipse only). innerRadius is a 0..1 fraction of the radius; arcHalf
        // >= PI means a full disc/ring. arcCenter/arcHalf are in Unity angle space (radians).
        [Range(0f, 1f)] public float innerRadius = 0f;
        public float arcCenter = 0f;
        public float arcHalf = Mathf.PI;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureCanvasChannels();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnsureCanvasChannels();
        }

        // The SDF shader reads TEXCOORD1; a Canvas strips extra channels unless asked.
        void EnsureCanvasChannels()
        {
            var c = canvas;
            if (c != null)
                c.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty(); // resize (e.g. animated HEIGHT) -> rebuild the SDF quad
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = GetPixelAdjustedRect();
            float halfW = r.width * 0.5f;
            float halfH = r.height * 0.5f;
            float radius = shape == FmuShapeType.Ellipse
                ? Mathf.Min(halfW, halfH)
                : Mathf.Clamp(cornerRadius, 0f, Mathf.Min(halfW, halfH));

            float cx = r.center.x, cy = r.center.y;
            Color32 col = color;
            var arc = new Vector4(radius, innerRadius, arcCenter, arcHalf);

            AddVert(vh, r.xMin, r.yMin, cx, cy, halfW, halfH, arc, col);
            AddVert(vh, r.xMin, r.yMax, cx, cy, halfW, halfH, arc, col);
            AddVert(vh, r.xMax, r.yMax, cx, cy, halfW, halfH, arc, col);
            AddVert(vh, r.xMax, r.yMin, cx, cy, halfW, halfH, arc, col);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        static void AddVert(VertexHelper vh, float x, float y, float cx, float cy,
                            float halfW, float halfH, Vector4 arc, Color32 col)
        {
            var v = UIVertex.simpleVert;
            v.position = new Vector3(x, y, 0f);
            v.color = col;
            v.uv0 = new Vector4(x - cx, y - cy, halfW, halfH); // pos rel. center + half-size
            v.uv1 = arc;                                        // (radius, innerFrac, arcCenter, arcHalf)
            vh.AddVert(v);
        }
    }
}
