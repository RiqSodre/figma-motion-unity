// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// Custom inspector for FmuPlayer: playback options + an edit-mode preview (scrub the
// animation in the Scene without entering Play mode) using Unity's AnimationMode.
using UnityEditor;
using UnityEngine;

namespace Fmu
{
    [CustomEditor(typeof(FmuPlayer))]
    public class FmuPlayerEditor : Editor
    {
        float previewTime;
        bool playing;
        bool sampling;      // AnimationMode active
        double lastTick;

        void OnEnable() { EditorApplication.update += Tick; }
        void OnDisable() { EditorApplication.update -= Tick; StopPreview(); }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var p = (FmuPlayer)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Em Play mode o runtime controla a animação.", MessageType.None);
                return;
            }
            if (p.clip == null)
            {
                EditorGUILayout.HelpBox("Sem clip para pré-visualizar.", MessageType.None);
                return;
            }

            float dur = p.duration > 0f ? p.duration : p.clip.length;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pause" : "Play"))
                {
                    playing = !playing;
                    lastTick = EditorApplication.timeSinceStartup;
                    if (playing) StartPreview();
                }
                if (GUILayout.Button("Restart"))
                {
                    previewTime = 0f; playing = true;
                    lastTick = EditorApplication.timeSinceStartup;
                    StartPreview(); Sample(p, dur);
                }
                if (GUILayout.Button("Stop"))
                {
                    playing = false; previewTime = 0f;
                    StopPreview();
                }
            }

            EditorGUI.BeginChangeCheck();
            float t = EditorGUILayout.Slider("Time", previewTime, 0f, dur);
            if (EditorGUI.EndChangeCheck())
            {
                previewTime = t; playing = false;
                StartPreview(); Sample(p, dur);
            }

            EditorGUILayout.LabelField(
                sampling ? "● preview ativo (Stop restaura o estado)" : "scrub ou Play para pré-visualizar",
                EditorStyles.centeredGreyMiniLabel);
        }

        void Tick()
        {
            if (!playing || Application.isPlaying) return;
            var p = target as FmuPlayer;
            if (p == null || p.clip == null) { playing = false; return; }

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastTick);
            lastTick = now;

            float dur = p.duration > 0f ? p.duration : p.clip.length;
            previewTime += dt * Mathf.Max(0.0001f, p.speed);
            switch (p.playMode)
            {
                case FmuPlayMode.PingPong: previewTime = Mathf.Repeat(previewTime, 2f * dur); break;
                case FmuPlayMode.Once:
                    if (previewTime >= dur) { previewTime = dur; playing = false; }
                    break;
                default: previewTime = Mathf.Repeat(previewTime, dur); break;
            }
            Sample(p, dur);
            Repaint();
        }

        void StartPreview()
        {
            if (!sampling) { AnimationMode.StartAnimationMode(); sampling = true; }
        }

        void StopPreview()
        {
            if (sampling) { AnimationMode.StopAnimationMode(); sampling = false; }
        }

        void Sample(FmuPlayer p, float dur)
        {
            if (p.clip == null) return;
            StartPreview();
            float et = p.playMode == FmuPlayMode.PingPong
                ? Mathf.PingPong(previewTime, dur)
                : Mathf.Clamp(previewTime, 0f, dur);

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(p.gameObject, p.clip, et);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }
    }
}
