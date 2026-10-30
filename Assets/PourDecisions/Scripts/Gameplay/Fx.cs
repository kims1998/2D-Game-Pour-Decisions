using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions {
    /*
    * Lightweight pooled particles: splash droplets, counter puddles and sparkles.
    * Hand-rolled instead of ParticleSystem so it behaves identically in every render pipeline.
    */
    public class Fx : MonoBehaviour {
        public static Fx I { get; private set; }

        class Particle {
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float life, maxLife, gravity, size0, size1, spin;
            public Color color;
            public bool alive, puddleOnLand;
        }

        readonly List<Particle> particles = new List<Particle>();
        readonly List<SpriteRenderer> puddles = new List<SpriteRenderer>();
        readonly List<float> puddleAge = new List<float>();
        Sprite droplet, sparkle, puddle;
        public Color Ambient = Color.white;

        void Awake() {
            I = this;
            droplet = Art.Sprite("fx_droplet");
            sparkle = Art.Sprite("fx_sparkle");
            puddle = Art.Sprite("fx_puddle");
        }

        // Object pool: reuse a finished particle if there is one, otherwise make a new one (up to 220).
        // This avoids creating and destroying objects every splash.
        Particle Get() {
            foreach (var p in particles) if (!p.alive) return p;
            if (particles.Count >= 220) return particles[Random.Range(0, particles.Count)];
            var np = new Particle { sr = Art.NewSprite("fx", transform, droplet, Layers.Fx) };
            particles.Add(np);

            return np;
        }

        // Splash droplets thrown from a point.
        public void Droplets(Vector2 pos, Color color, int count, float speed, bool puddleOnLand) {
            for (int i = 0; i < count; i++) {
                var p = Get();
                p.alive = true;
                p.pos = pos;
                float a = Random.Range(25f, 155f) * Mathf.Deg2Rad;
                p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.5f, 1.2f);
                p.life = 0f;
                p.maxLife = Random.Range(0.25f, 0.5f);
                p.gravity = K.Gravity * 0.12f;
                p.size0 = Random.Range(0.05f, 0.09f);
                p.size1 = p.size0 * 0.6f;
                p.color = new Color(color.r, color.g, color.b, Mathf.Max(0.7f, color.a));
                p.puddleOnLand = puddleOnLand;
                p.spin = 0f;
                p.sr.sprite = droplet;
                p.sr.gameObject.SetActive(true);
            }
        }

        // A drip that runs off the rim and falls (used for overflow).
        public void Drip(Vector2 pos, Color color, float sideways) {
            var p = Get();
            p.alive = true;
            p.pos = pos;
            p.vel = new Vector2(sideways * Random.Range(0.2f, 0.6f), Random.Range(-0.3f, 0f));
            p.life = 0f;
            p.maxLife = 3f;
            p.gravity = K.Gravity * 0.05f;
            p.size0 = p.size1 = Random.Range(0.06f, 0.1f);
            p.color = new Color(color.r, color.g, color.b, Mathf.Max(0.75f, color.a));
            p.puddleOnLand = true;
            p.spin = 0f;
            p.sr.sprite = droplet;
            p.sr.gameObject.SetActive(true);
        }

        public void Sparkles(Vector2 pos, int count, Color color) {
            for (int i = 0; i < count; i++) {
                var p = Get();
                p.alive = true;
                p.pos = pos + Random.insideUnitCircle * 0.6f;
                p.vel = Random.insideUnitCircle * 1.2f + Vector2.up * 0.8f;
                p.life = 0f;
                p.maxLife = Random.Range(0.5f, 0.9f);
                p.gravity = 0f;
                p.size0 = Random.Range(0.1f, 0.2f);
                p.size1 = 0f;
                p.color = color;
                p.puddleOnLand = false;
                p.spin = Random.Range(-360f, 360f);
                p.sr.sprite = sparkle;
                p.sr.gameObject.SetActive(true);
            }
        }

        // Adds to (or starts) a puddle on the counter near x.
        public void Puddle(float x, Color color, float amountMl) {
            SpriteRenderer target = null;
            for (int i = 0; i < puddles.Count; i++)
                if (Mathf.Abs(puddles[i].transform.position.x - x) < 0.35f) { target = puddles[i]; puddleAge[i] = 0f; break; }

            if (target == null) {
                if (puddles.Count >= 10) {
                    Destroy(puddles[0].gameObject);
                    puddles.RemoveAt(0);
                    puddleAge.RemoveAt(0);
                }

                target = Art.NewSprite("puddle", transform, puddle, Layers.Puddle);
                target.transform.position = new Vector3(x, K.CounterY - 0.06f, 0f);
                target.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
                puddles.Add(target);
                puddleAge.Add(0f);
            }

            target.color = new Color(color.r, color.g, color.b, Mathf.Max(0.55f, color.a * 0.8f)) * Ambient;
            float s = Mathf.Min(1.6f, target.transform.localScale.x + amountMl * 0.02f);
            target.transform.localScale = new Vector3(s, s * 0.9f, 1f);
        }

        public void ClearPuddles() {
            foreach (var p in puddles) if (p != null) Destroy(p.gameObject);
            puddles.Clear();
            puddleAge.Clear();
        }

        public void ClearAll() {
            ClearPuddles();
            foreach (var p in particles) { p.alive = false; p.sr.gameObject.SetActive(false); }
        }

        void Update() {
            float dt = Time.deltaTime;
            foreach (var p in particles) {
                if (!p.alive) continue;
                p.life += dt;
                p.vel.y -= p.gravity * dt;
                p.pos += p.vel * dt;

                if (p.puddleOnLand && p.pos.y <= K.CounterY - 0.04f) {
                    Puddle(p.pos.x, p.color, 0.6f);
                    p.alive = false;
                }

                if (p.life >= p.maxLife) p.alive = false;
                if (!p.alive) { p.sr.gameObject.SetActive(false); continue; }
                float t = p.life / p.maxLife;
                float size = Mathf.Lerp(p.size0, p.size1, t);
                p.sr.transform.position = p.pos;
                p.sr.transform.localScale = new Vector3(size, size, 1f) * 3.2f;
                if (p.spin != 0f) p.sr.transform.Rotate(0f, 0f, p.spin * dt);
                var c = p.color * Ambient;
                c.a = p.color.a * (1f - t * t);
                p.sr.color = c;
            }

            // puddles slowly dry up
            for (int i = puddles.Count - 1; i >= 0; i--) {
                puddleAge[i] += dt;

                if (puddleAge[i] > 6f) {
                    var c = puddles[i].color;
                    c.a -= dt * 0.2f;
                    puddles[i].color = c;

                    if (c.a <= 0f) {
                        Destroy(puddles[i].gameObject);
                        puddles.RemoveAt(i);
                        puddleAge.RemoveAt(i);
                    }
                }
            }
        }
    }
}