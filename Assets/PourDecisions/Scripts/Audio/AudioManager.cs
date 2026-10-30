using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions {
    /*
    * Plays one-shot effects from a small pool and runs the continuous pour sound.
    * The pour loop is driven by gameplay every frame: louder with more liquid landing,
    * and higher pitched as the glass fills (the air column above the liquid gets shorter,
    * just like filling a real bottle). In the dim levels that pitch is the best cue you have.
    */
    public class AudioManager : MonoBehaviour {
        public static AudioManager I { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource[] pool;
        int nextVoice;
        AudioSource pour;
        float pourVolume, pourPitch = 1f;
        bool pourThick;
        float lastSplash, lastDrip;

        void Awake() {
            I = this;
            // A pool of 16 AudioSources lets many one-shot sounds overlap without cutting each other off.
            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++) {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            pour = gameObject.AddComponent<AudioSource>();
            pour.playOnAwake = false;
            pour.loop = true;
            pour.clip = Clip("pour_loop");
        }

        public AudioClip Clip(string name) {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) Debug.LogWarning("[Pour Decisions] Missing sound: " + name);
            clips[name] = c;

            return c;
        }

        // Plays a one-shot effect.
        public void Play(string name, float volume = 1f, float pitch = 1f) {
            var clip = Clip(name);
            if (clip == null) return;
            var src = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
            src.Stop();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
        }

        // Plays with a little random pitch so repeated sounds don't feel robotic.
        public void PlayVaried(string name, float volume = 1f, float spread = 0.08f) =>
            Play(name, volume, 1f + Random.Range(-spread, spread));

        public void Click() => Play("ui_click", 0.6f);
        public void Hover() => Play("ui_hover", 0.25f, Random.Range(0.95f, 1.05f));

        // Rate-limited splash for liquid hitting the counter or a rim.
        public void Splash(float volume) {
            if (Time.unscaledTime - lastSplash < 0.14f) return;
            lastSplash = Time.unscaledTime;
            Play("splash", volume, Random.Range(0.85f, 1.15f));
        }

        public void Drip() {
            if (Time.unscaledTime - lastDrip < 0.09f) return;
            lastDrip = Time.unscaledTime;
            Play("drip", 0.35f, Random.Range(0.8f, 1.3f));
        }

        public void Glug(float bottleFill) {
            // Emptier bottles have a bigger air pocket, so the glug is deeper.
            string clip = "glug_" + Random.Range(1, 4);
            Play(clip, 0.55f, Mathf.Lerp(0.8f, 1.25f, bottleFill));
        }

        /*
        * Called every frame during play.
        * How much liquid is hitting the glass right now.
        * Liquid height as a fraction of the glass height.
        */
        public void UpdatePour(float landingMlPerSec, float glassFill01, bool thick) {
            if (thick != pourThick) {
                pourThick = thick;
                pour.clip = Clip(thick ? "pour_thick_loop" : "pour_loop");
            }

            // Louder when more liquid lands; pitch rises as the glass fills. Both ease toward the target below.
            float targetVol = Mathf.Pow(Mathf.Clamp01(landingMlPerSec / 30f), 0.5f) * 0.85f;
            float targetPitch = thick ? 0.8f + 0.45f * glassFill01 : 0.85f + 0.8f * glassFill01;
            float k = 1f - Mathf.Exp(-Time.deltaTime / 0.06f);
            pourVolume += (targetVol - pourVolume) * k;
            pourPitch += (targetPitch - pourPitch) * k;
            pour.volume = pourVolume;
            pour.pitch = pourPitch;
            if (pourVolume > 0.01f && !pour.isPlaying) pour.Play();
            else if (pourVolume <= 0.01f && pour.isPlaying) pour.Pause();
        }

        public void StopPour() {
            pourVolume = 0f;
            pour.volume = 0f;
            pour.Stop();
        }
    }
}
