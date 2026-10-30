using System.Collections;
using UnityEngine;

namespace PourDecisions {
    /// <summary>The customer across the counter. Walks in with an order and reacts to the pour.</summary>
    public class Customer : MonoBehaviour {
        public const int Count = 6;
        const float StandX = 7.0f, OffX = 11.5f;
        const float Height = 3.6f;

        SpriteRenderer body;
        int index;
        string mood = "neutral";
        float bobPhase;

        /// <summary>Roughly where the customer's head is (for the speech bubble).</summary>
        public Vector2 HeadWorld => (Vector2)transform.position + new Vector2(0f, Height * 0.72f);

        public void Init() {
            body = Art.NewSprite("Body", transform, null, Layers.Customer);
            transform.position = new Vector3(OffX, BaseY, 0f);
        }

        static float BaseY => K.CounterBackY - 0.3f;

        public void SetAmbient(Color c) => body.color = c;

        public void Setup(int customerIndex) {
            index = Mathf.Clamp(customerIndex, 0, Count - 1);
            SetMood("neutral");
        }

        public void SetMood(string m) {
            mood = m;
            body.sprite = Art.Sprite($"customer_{index}_{mood}", 100f, new Vector2(0.5f, 0f));
        }

        public static string MoodFor(Grade g) =>
            g == Grade.Perfect || g == Grade.Great ? "happy" : g == Grade.Good ? "neutral" : "upset";

        // Enter and Leave are coroutines: they slide the customer over half a second, one frame at a time.
        public IEnumerator Enter() {
            transform.position = new Vector3(OffX, BaseY, 0f);
            yield return Tween.Move(transform, new Vector3(StandX, BaseY, 0f), 0.6f, Tween.EaseOutCubic);
        }

        public IEnumerator Leave() {
            yield return Tween.Move(transform, new Vector3(OffX, BaseY, 0f), 0.5f, Tween.EaseInCubic);
        }

        public void HideImmediate() => transform.position = new Vector3(OffX, BaseY, 0f);

        void Update() {
            // idle breathing, and a little hop when happy
            // Idle motion: a sine wave moves the sprite up and down a little (faster and bigger when happy).
            bobPhase += Time.deltaTime * (mood == "happy" ? 9f : 2.2f);
            float amp = mood == "happy" ? 0.06f : 0.025f;
            body.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobPhase)) * amp, 0f);
        }
    }
}
