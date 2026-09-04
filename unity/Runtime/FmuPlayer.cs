// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// FmuPlayer — plays a single baked AnimationClip via a PlayableGraph, with an
// explicit clock so playback is deterministic (no AnimatorController needed).
// Added automatically by FmuImporter to the imported prefab's root. Playback options
// (mode, speed, autoplay, delay) are exposed for per-instance control in the Inspector.
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Fmu
{
    public enum FmuPlayMode { Loop, Once, PingPong }

    [RequireComponent(typeof(Animator))]
    public class FmuPlayer : MonoBehaviour
    {
        public AnimationClip clip;
        public float duration;                       // seconds; from the Figma timeline

        [Header("Playback")]
        public FmuPlayMode playMode = FmuPlayMode.Loop;
        [Range(0f, 4f)] public float speed = 1f;
        public bool autoPlay = true;
        [Min(0f)] public float startDelay = 0f;

        public bool IsPlaying { get; private set; }
        public float Time01 => duration > 0f ? Mathf.Clamp01(EvalTime() / duration) : 0f;

        PlayableGraph graph;
        AnimationClipPlayable playable;
        float time, delayLeft;

        void OnEnable()
        {
            if (clip == null) return;
            if (duration <= 0f) duration = clip.length;

            var animator = GetComponent<Animator>();
            graph = PlayableGraph.Create("FmuPlayer:" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); // we drive the clock
            var output = AnimationPlayableOutput.Create(graph, "out", animator);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable);
            graph.Play(); // Manual mode: only Evaluate advances time

            time = 0f;
            delayLeft = startDelay;
            IsPlaying = autoPlay;
            Sample();
        }

        void Update()
        {
            if (!graph.IsValid() || !IsPlaying) return;

            if (delayLeft > 0f)
            {
                delayLeft -= UnityEngine.Time.deltaTime;
                if (delayLeft > 0f) return;
            }

            time += UnityEngine.Time.deltaTime * speed;
            if (duration > 0f)
            {
                switch (playMode)
                {
                    case FmuPlayMode.Loop: time = Mathf.Repeat(time, duration); break;
                    case FmuPlayMode.PingPong: time = Mathf.Repeat(time, 2f * duration); break;
                    case FmuPlayMode.Once:
                        if (time >= duration) { time = duration; IsPlaying = false; }
                        break;
                }
            }
            Sample();
        }

        float EvalTime()
        {
            if (duration <= 0f) return 0f;
            if (playMode == FmuPlayMode.PingPong) return Mathf.PingPong(time, duration);
            return Mathf.Clamp(time, 0f, duration);
        }

        void Sample()
        {
            if (!graph.IsValid()) return;
            playable.SetTime(EvalTime());
            graph.Evaluate(0f); // apply pose at the current playable time
        }

        // --- runtime controls (callable from other scripts / UnityEvents) ---
        public void Play() { IsPlaying = true; }
        public void Pause() { IsPlaying = false; }
        public void Restart() { time = 0f; delayLeft = startDelay; IsPlaying = true; Sample(); }
        public void Stop() { IsPlaying = false; time = 0f; Sample(); }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
