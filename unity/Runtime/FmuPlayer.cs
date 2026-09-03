// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// FmuPlayer — plays a single baked AnimationClip via a PlayableGraph, with an
// explicit clock so looping is deterministic (no AnimatorController needed).
// Added automatically by FmuImporter to the imported prefab's root.
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Fmu
{
    [RequireComponent(typeof(Animator))]
    public class FmuPlayer : MonoBehaviour
    {
        public AnimationClip clip;
        public bool loop = true;
        public float duration;            // seconds; from the Figma timeline
        [Range(0f, 4f)] public float speed = 1f;

        PlayableGraph graph;
        AnimationClipPlayable playable;
        float time;

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
            graph.Play(); // outputs active; Manual mode means only Evaluate advances time

            time = 0f;
            Sample();
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            time += Time.deltaTime * speed;
            if (duration > 0f)
            {
                if (loop) time = Mathf.Repeat(time, duration);
                else time = Mathf.Min(time, duration);
            }
            Sample();
        }

        void Sample()
        {
            playable.SetTime(time);
            graph.Evaluate(0f); // apply pose at the current playable time
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
