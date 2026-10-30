using System;
using System.Collections;
using UnityEngine;

namespace PourDecisions {
    // Small coroutine helpers for slides and pops.
    public static class Tween {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }

        // Overshoots then settles; nice for pop-in text.
        public static float EaseOutBack(float t) {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;

            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // Moves a transform to a world position over time with the given easing.
        public static IEnumerator Move(Transform t, Vector3 to, float duration, Func<float, float> ease) {
            if (t == null) yield break;
            Vector3 from = t.position;
            float e = 0f;

            while (e < duration) {
                if (t == null) yield break;
                e += Time.deltaTime;
                t.position = Vector3.LerpUnclamped(from, to, ease(e / duration));
                yield return null;
            }

            if (t != null) t.position = to;
        }

        // Runs an action every frame with a 0..1 progress value (scaled time).
        public static IEnumerator Run(float duration, Action<float> step) {
            float e = 0f;

            while (e < duration) {
                e += Time.deltaTime;
                step(Mathf.Clamp01(e / duration));
                yield return null;
            }

            step(1f);
        }
    }
}