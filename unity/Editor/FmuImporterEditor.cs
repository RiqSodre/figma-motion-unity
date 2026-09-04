// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// Custom inspector for .fmu assets: shows a summary + Unity-compatibility report,
// the bake sample rate, and a one-click "Add to Scene (under Canvas)" that avoids the
// classic gotcha of dropping a UI element at the scene root (where it never renders).
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fmu
{
    [CustomEditor(typeof(FmuImporter))]
    public class FmuImporterEditor : ScriptedImporterEditor
    {
        static readonly Dictionary<string, string> Support = new Dictionary<string, string>
        {
            { "OPACITY", "ok" }, { "HEIGHT", "ok" }, { "WIDTH", "ok" },
            { "TRANSLATION_X", "ok" }, { "TRANSLATION_Y", "ok" },
            { "SCALE_X", "ok" }, { "SCALE_Y", "ok" }, { "SCALE", "ok" },
            { "FILL_R", "ok" }, { "FILL_G", "ok" }, { "FILL_B", "ok" }, { "FILL_A", "ok" },
            { "ROTATION", "beta" },
        };

        FmuDoc _doc;
        string _parseError;

        public override void OnEnable()
        {
            base.OnEnable();
            TryParse();
        }

        void TryParse()
        {
            _doc = null; _parseError = null;
            try
            {
                var path = ((AssetImporter)target).assetPath;
                _doc = JsonConvert.DeserializeObject<FmuDoc>(File.ReadAllText(path));
            }
            catch (System.Exception e) { _parseError = e.Message; }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_parseError != null)
                EditorGUILayout.HelpBox("Não foi possível ler o .fmu: " + _parseError, MessageType.Error);
            else if (_doc != null)
                DrawSummary();

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sampleRate"),
                new GUIContent("Sample Rate", "Keyframes gerados por segundo ao amostrar o easing."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("springSettle"),
                new GUIContent("Spring Settle", "Menor = kick mais forte + settle (mais perto do Figma); 1 = mais suave."));

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(_doc == null))
            {
                if (GUILayout.Button("Add to Scene (under Canvas)", GUILayout.Height(28)))
                    AddToScene();
            }
            EditorGUILayout.LabelField("Instancia sob um Canvas (cria um se não houver).",
                EditorStyles.centeredGreyMiniLabel);

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        // --- summary ---------------------------------------------------------

        void DrawSummary()
        {
            int layers = 0, animated = 0, keyframes = 0;
            var tracks = new SortedDictionary<string, int>();
            var warnings = new List<string>();
            Walk(_doc.node, ref layers, ref animated, ref keyframes, tracks, warnings);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(_doc.node != null ? _doc.node.name : "(fmu)", EditorStyles.boldLabel);
            float dur = _doc.timeline != null ? _doc.timeline.duration : 0f;
            EditorGUILayout.LabelField($"Duração {dur:0.##}s   ·   {animated} camadas animadas   ·   {keyframes} keyframes",
                EditorStyles.miniLabel);

            if (tracks.Count > 0)
            {
                var sb = new System.Text.StringBuilder("Tracks: ");
                foreach (var kv in tracks) sb.Append(kv.Key).Append(" ×").Append(kv.Value).Append("   ");
                EditorGUILayout.LabelField(sb.ToString(), EditorStyles.miniLabel);
            }

            if (warnings.Count == 0)
                EditorGUILayout.HelpBox("Tudo compatível — reproduz fiel na Unity.", MessageType.Info);
            else
                foreach (var w in warnings)
                    EditorGUILayout.HelpBox(w, MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        void Walk(FmuNode n, ref int layers, ref int animated, ref int keyframes,
                  SortedDictionary<string, int> tracks, List<string> warnings)
        {
            if (n == null) return;
            layers++;
            bool anyTrack = false;
            if (n.tracks != null)
                foreach (var t in n.tracks)
                {
                    anyTrack = true;
                    tracks.TryGetValue(t.property, out int c);
                    tracks[t.property] = c + 1;
                    if (t.keys != null) keyframes += t.keys.Count;
                    if (!Support.ContainsKey(t.property))
                        warnings.Add($"'{t.property}' em '{n.name}' não é suportado (ignorado).");
                    else if (Support[t.property] == "beta")
                        warnings.Add($"'{t.property}' em '{n.name}' é suportado mas não testado.");
                }
            if (anyTrack) animated++;
            if (n.children != null)
                foreach (var c in n.children)
                    Walk(c, ref layers, ref animated, ref keyframes, tracks, warnings);
        }

        // --- add to scene ----------------------------------------------------

        void AddToScene()
        {
            var path = ((AssetImporter)target).assetPath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("FMU", "O .fmu ainda não gerou um prefab. Faça Reimport.", "OK");
                return;
            }

            var canvas = FindOrCreateCanvas();
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            Undo.RegisterCreatedObjectUndo(inst, "Add FMU animation");

            if (inst.transform is RectTransform rt)
            {
                // Center it on the canvas so it lands in view.
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            Selection.activeGameObject = inst;
            EditorGUIUtility.PingObject(inst);
            EditorApplication.ExecuteMenuItem("Window/General/Scene");
        }

        static Canvas FindOrCreateCanvas()
        {
            // Prefer a Canvas on/above the current selection.
            if (Selection.activeGameObject != null)
            {
                var c = Selection.activeGameObject.GetComponentInParent<Canvas>();
                if (c != null) return c;
            }
            var any = Object.FindFirstObjectByType<Canvas>();
            if (any != null) return any;

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
            return go.GetComponent<Canvas>();
        }
    }
}
