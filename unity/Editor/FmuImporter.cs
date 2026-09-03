// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// FmuImporter — turns a .fmu file (Figma Motion export) into a native Unity UI
// prefab + a baked AnimationClip. Drop the imported prefab under a Canvas and
// press Play; the attached FmuPlayer drives the clip.
//
// Fidelity strategy: every easing segment is BAKED by sampling the cubic-bezier
// timing function at the clip frame rate and writing linear keyframes. This makes
// the Unity motion match Figma exactly for any easing (and will cover baked
// springs later) without approximating bezier->Hermite tangents.

using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Fmu
{
    [ScriptedImporter(1, "fmu")]
    public class FmuImporter : ScriptedImporter
    {
        [Tooltip("Keyframes baked per second when sampling easing curves.")]
        public int sampleRate = 60;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = File.ReadAllText(ctx.assetPath);
            var doc = JsonConvert.DeserializeObject<FmuDoc>(text);
            if (doc == null || doc.node == null)
            {
                ctx.LogImportError("FMU: could not parse " + ctx.assetPath);
                return;
            }

            // Figma layers often share a name ("Rectangle" x3). Animation curves bind
            // by hierarchy path, so duplicate sibling names collide and only the first
            // twin animates. Make names unique BEFORE building hierarchy AND curves so
            // both agree on the same path.
            EnsureUniqueNames(doc.node);

            // --- shared SDF material for crisp shapes ---
            var shader = Shader.Find("FMU/UI SDF");
            Material sdfMat = null;
            if (shader != null)
            {
                sdfMat = new Material(shader) { name = "FmuSDF" };
            }
            else
            {
                ctx.LogImportWarning("FMU: shader 'FMU/UI SDF' not found. Make sure " +
                    "FmuSDF.shader is in the project, then reimport this .fmu.");
            }

            // --- hierarchy ---
            var root = BuildNode(doc.node, isRoot: true, sdfMat);
            if (sdfMat != null) ctx.AddObjectToAsset("sdfMat", sdfMat);

            // --- animation clip ---
            var clip = new AnimationClip
            {
                name = doc.node.name + "_clip",
                frameRate = Mathf.Max(1, sampleRate)
            };
            BuildCurves(doc.node, "", clip);

            // The clip itself is NOT looping: its keyframes may end before the
            // timeline duration (a hold tail). FmuPlayer owns looping over the full
            // duration, so times past the last key hold the final pose (the hold),
            // instead of the clip wrapping early and eating the tail.
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // --- runtime player ---
            var animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;
            var player = root.AddComponent<FmuPlayer>();
            player.clip = clip;
            player.loop = doc.timeline == null || doc.timeline.loop;
            player.duration = doc.timeline != null ? doc.timeline.duration : ClipLength(clip);

            // --- register assets ---
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
            ctx.AddObjectToAsset("clip", clip);
        }

        // ------------------------------------------------------------------
        // Hierarchy
        // ------------------------------------------------------------------

        static GameObject BuildNode(FmuNode n, bool isRoot, Material sdfMat)
        {
            var go = new GameObject(n.name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;

            // Figma top-left model: anchor + pivot to the parent's top-left,
            // y grows downward -> negate for Unity's y-up.
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(n.rect.w, n.rect.h);
            rt.anchoredPosition = isRoot ? Vector2.zero : new Vector2(n.rect.x, -n.rect.y);
            rt.localScale = Vector3.one; // RectTransforms built in a ScriptedImporter can
                                         // serialize with scale 0 -> invisible; force 1.

            AddVisual(go, n, sdfMat);

            // Opacity: nodes carrying an OPACITY track (or a non-opaque base) get
            // a CanvasGroup so alpha animates independently of color.
            if (HasTrack(n, "OPACITY") || n.opacity < 0.999f)
            {
                var cg = go.AddComponent<CanvasGroup>();
                cg.alpha = n.opacity;
            }

            if (n.children != null)
                foreach (var c in n.children)
                    BuildNode(c, false, sdfMat).transform.SetParent(rt, false);

            return go;
        }

        static void AddVisual(GameObject go, FmuNode n, Material sdfMat)
        {
            // Frames are transparent containers.
            if (n.type == "FRAME" || n.type == "GROUP") return;

            var g = go.AddComponent<FmuShapeGraphic>();
            if (sdfMat != null) g.material = sdfMat;

            // Prefer fill; fall back to stroke color for ring outlines. FmuPaint and
            // FmuStroke are distinct types, so pick the (color, opacity) pair explicitly.
            string colorHex = null;
            float colorOpacity = 1f;
            if (n.fill != null) { colorHex = n.fill.color; colorOpacity = n.fill.opacity; }
            else if (n.stroke != null) { colorHex = n.stroke.color; colorOpacity = n.stroke.opacity; }
            g.color = ParseColor(colorHex, colorOpacity);

            if (n.type == "ELLIPSE")
            {
                g.shape = FmuShapeType.Ellipse;
            }
            else
            {
                g.shape = FmuShapeType.RoundedRect;
                g.cornerRadius = n.cornerRadius; // real Figma corner radius, crisp via SDF
            }
        }

        static Color ParseColor(string hex, float opacity)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) c = Color.white;
            c.a = opacity;
            return c;
        }

        // Rename siblings so no two share a name: first keeps its name, the rest get
        // a "-2", "-3" suffix (in child/z order, deterministic). Mutates FmuNode.name
        // so hierarchy and curve paths use the identical unique name.
        static void EnsureUniqueNames(FmuNode n)
        {
            if (n.children == null) return;
            var counts = new Dictionary<string, int>();
            foreach (var c in n.children)
            {
                var baseName = string.IsNullOrEmpty(c.name) ? c.type : c.name;
                if (counts.TryGetValue(baseName, out int seen))
                {
                    seen++;
                    counts[baseName] = seen;
                    c.name = baseName + "-" + seen;
                }
                else
                {
                    counts[baseName] = 1;
                    c.name = baseName;
                }
                EnsureUniqueNames(c);
            }
        }

        // ------------------------------------------------------------------
        // Curves
        // ------------------------------------------------------------------

        static void BuildCurves(FmuNode n, string path, AnimationClip clip)
        {
            if (n.tracks != null)
                foreach (var t in n.tracks)
                    BindTrack(n, t, path, clip);

            if (n.children != null)
                foreach (var c in n.children)
                {
                    var childPath = string.IsNullOrEmpty(path) ? c.name : path + "/" + c.name;
                    BuildCurves(c, childPath, clip);
                }
        }

        static void BindTrack(FmuNode n, FmuTrack t, string path, AnimationClip clip)
        {
            System.Type type;
            string prop;
            System.Func<float, float> map; // fmu value -> unity property value

            switch (t.property)
            {
                case "OPACITY":
                    type = typeof(CanvasGroup); prop = "m_Alpha";
                    map = v => v;
                    break;
                case "HEIGHT":
                    type = typeof(RectTransform); prop = "m_SizeDelta.y";
                    map = v => v; // pivot is top -> height grows downward like Figma
                    break;
                case "WIDTH":
                    type = typeof(RectTransform); prop = "m_SizeDelta.x";
                    map = v => v;
                    break;
                case "TRANSLATION_X":
                    type = typeof(RectTransform); prop = "m_AnchoredPosition.x";
                    map = v => t.@base + (t.relative ? v : 0f);
                    break;
                case "TRANSLATION_Y":
                    type = typeof(RectTransform); prop = "m_AnchoredPosition.y";
                    // Figma y-down -> Unity y-up: negate the absolute position.
                    map = v => -(t.@base + (t.relative ? v : 0f));
                    break;
                case "ROTATION":
                    // Figma rotates CCW-positive in its y-down space; negate for Unity's
                    // y-up so visual direction matches. (Untested — no rotation in sample.)
                    type = typeof(RectTransform); prop = "localEulerAnglesRaw.z";
                    map = v => -(t.@base * (t.relative ? 0f : 1f) + v);
                    break;
                case "SCALE":
                case "SCALE_X":
                case "SCALE_Y":
                    // Uniform/axis scale as a multiplier (1 = 100%). (Untested.)
                    type = typeof(RectTransform);
                    prop = t.property == "SCALE_Y" ? "m_LocalScale.y" : "m_LocalScale.x";
                    map = v => t.relative ? t.@base + v : v;
                    if (t.property == "SCALE") // uniform: also bind Y below
                        BindScaleUniformY(t, path, clip);
                    break;
                default:
                    return; // unsupported (FILL_COLOR/gradients, springs) — see SCHEMA.md
            }

            var curve = BakeCurve(t, map, clip.frameRate);
            var binding = new EditorCurveBinding { path = path, type = type, propertyName = prop };
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        // Uniform SCALE also drives Y (BindTrack handles X).
        static void BindScaleUniformY(FmuTrack t, string path, AnimationClip clip)
        {
            System.Func<float, float> map = v => t.relative ? t.@base + v : v;
            var curve = BakeCurve(t, map, clip.frameRate);
            var binding = new EditorCurveBinding
            {
                path = path,
                type = typeof(RectTransform),
                propertyName = "m_LocalScale.y"
            };
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        // Sample each segment's cubic-bezier easing into dense linear keyframes.
        static AnimationCurve BakeCurve(FmuTrack t, System.Func<float, float> map, float frameRate)
        {
            var keys = new List<Keyframe>();
            var src = t.keys;

            for (int i = 0; i < src.Count - 1; i++)
            {
                var a = src[i];
                var b = src[i + 1];
                float t0 = a.t, t1 = b.t;
                float va = map(a.v), vb = map(b.v);
                var bez = new UnitBezier(a.ease);

                float dt = Mathf.Max(0.0001f, t1 - t0);
                int steps = Mathf.Max(1, Mathf.RoundToInt(dt * frameRate));
                // Include the segment start; the segment end is emitted by the next
                // iteration's start (or the final key below).
                for (int s = 0; s < steps; s++)
                {
                    float x = s / (float)steps;        // normalized time in segment
                    float y = bez.SampleY(x);          // eased progress 0..1
                    keys.Add(new Keyframe(t0 + x * dt, Mathf.Lerp(va, vb, y)));
                }
            }
            // final key
            var last = src[src.Count - 1];
            keys.Add(new Keyframe(last.t, map(last.v)));

            var curve = new AnimationCurve(keys.ToArray());
            // Linear tangents between baked samples => exact playback of the samples.
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        static bool HasTrack(FmuNode n, string prop)
        {
            if (n.tracks == null) return false;
            foreach (var t in n.tracks) if (t.property == prop) return true;
            return false;
        }

        static float ClipLength(AnimationClip clip)
        {
            return clip.length;
        }
    }

    // CSS-style cubic-bezier timing solver (WebKit UnitBezier): given x in [0,1]
    // returns the eased y. Control points p0=(0,0), p3=(1,1).
    public struct UnitBezier
    {
        readonly float ax, bx, cx, ay, by, cy;

        public UnitBezier(float[] e)
        {
            float x1 = e != null && e.Length == 4 ? e[0] : 0f;
            float y1 = e != null && e.Length == 4 ? e[1] : 0f;
            float x2 = e != null && e.Length == 4 ? e[2] : 1f;
            float y2 = e != null && e.Length == 4 ? e[3] : 1f;
            cx = 3f * x1; bx = 3f * (x2 - x1) - cx; ax = 1f - cx - bx;
            cy = 3f * y1; by = 3f * (y2 - y1) - cy; ay = 1f - cy - by;
        }

        float SampleCurveX(float t) => ((ax * t + bx) * t + cx) * t;
        float SampleCurveY(float t) => ((ay * t + by) * t + cy) * t;
        float SampleCurveDerivativeX(float t) => (3f * ax * t + 2f * bx) * t + cx;

        float SolveCurveX(float x)
        {
            float t = x;
            // Newton-Raphson
            for (int i = 0; i < 8; i++)
            {
                float xt = SampleCurveX(t) - x;
                if (Mathf.Abs(xt) < 1e-5f) return t;
                float d = SampleCurveDerivativeX(t);
                if (Mathf.Abs(d) < 1e-6f) break;
                t -= xt / d;
            }
            // Bisection fallback
            float lo = 0f, hi = 1f; t = x;
            while (lo < hi)
            {
                float xt = SampleCurveX(t);
                if (Mathf.Abs(xt - x) < 1e-5f) return t;
                if (x > xt) lo = t; else hi = t;
                t = (hi - lo) * 0.5f + lo;
            }
            return t;
        }

        public float SampleY(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            return SampleCurveY(SolveCurveX(x));
        }
    }
}
