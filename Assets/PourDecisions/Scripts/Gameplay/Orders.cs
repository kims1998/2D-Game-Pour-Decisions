using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions {
    // Everything about one drink order.
    public class Order {
        public GlassDef glass;
        public LiquidDef liquid;
        public SpoutDef spout;
        public float shots;
        public float targetMl;
        // Starting bottle fill (0..1). Varies per order so the start angle changes.
        public float bottleFill;
        // Random flow factor from the spout's variance.
        public float flowMultiplier;
        public int customer;
        public string request;
    }

    // Builds random but always-possible orders.
    public static class OrderGenerator {
        static readonly string[] Asks = {
            "{0} of {1}, please!",
            "Can I get {0} of {1}?",
            "{0} of {1}. Neat.",
            "I'll take {0} of {1}.",
        };

        // Classic level, or null for Training (everything allowed).
        // Time allowed per order, or 0 for no per-order timer.
        public static Order Make(LevelDef level, float orderSeconds, Order previous) {
            string[] glassIds = level != null ? level.glasses : GameData.AllGlasses;
            var liquids = new List<LiquidDef>();
            if (level != null) foreach (var id in level.liquids) liquids.Add(GameData.Liquid(id));
            else liquids.AddRange(GameData.Liquids);
            string[] spoutIds = level != null ? level.spouts : new[] { "slow", "speed", "fast", "open" };
            float[] targets = level != null ? level.targetShots : GameData.AllTargets;

            // The longest a pour should take at full flow, so every order is doable in time.
            float maxPourSeconds = orderSeconds > 0f ? orderSeconds * 0.4f : 6f;

            Order o = null;
            
            // Pick random glass, liquid, spout and size until the combination is actually doable.
            for (int attempt = 0; attempt < 80 && o == null; attempt++) {
                var glass = Art.Glass(glassIds[Random.Range(0, glassIds.Length)]);
                if (previous != null && glassIds.Length > 1 && glass.id == previous.glass.id && attempt < 40) continue;
                var liquid = liquids[Random.Range(0, liquids.Count)];
                var spout = GameData.Spout(spoutIds[Random.Range(0, spoutIds.Length)]);
                float shots = targets[Random.Range(0, targets.Length)];
                float ml = shots * K.ShotMl;
                // Skip orders that would nearly fill the glass.
                if (ml > glass.capacityMl * 0.8f) continue;
                // Skip orders that would take too long to pour even at full flow.
                float seconds = ml / (spout.maxFlowMlPerSec * liquid.flowMultiplier);
                if (seconds > maxPourSeconds) continue;
                o = new Order { glass = glass, liquid = liquid, spout = spout, shots = shots, targetMl = ml };
            }

            if (o == null) {
                // Fallback that always works: a single shot of the first liquid with the fastest allowed spout
                // in the biggest allowed glass.
                GlassDef biggest = Art.Glass(glassIds[0]);
                foreach (var id in glassIds) { var g = Art.Glass(id); if (g.capacityMl > biggest.capacityMl) biggest = g; }
                SpoutDef fastest = GameData.Spout(spoutIds[0]);
                foreach (var id in spoutIds) { var s = GameData.Spout(id); if (s.maxFlowMlPerSec > fastest.maxFlowMlPerSec) fastest = s; }
                o = new Order { glass = biggest, liquid = liquids[0], spout = fastest, shots = 1f, targetMl = K.ShotMl };
            }

            // Make sure the bottle holds plenty more than the order.
            var bottle = Art.Bottle(o.liquid.bottleId);
            float minFill = Mathf.Min(0.9f, (o.targetMl * 2.5f + 40f) / bottle.capacityMl);
            o.bottleFill = Mathf.Max(minFill, Random.Range(0.2f, 0.9f));
            o.flowMultiplier = 1f + Random.Range(-o.spout.variance, o.spout.variance);

            do { o.customer = Random.Range(0, Customer.Count); }
            while (previous != null && o.customer == previous.customer);

            string amount = GameData.AmountText(o.shots);
            string ask = Asks[Random.Range(0, Asks.Length)];
            // "A double of Whiskey" reads oddly; say "A double Whiskey" instead.
            if (Mathf.Approximately(o.shots, 2f)) o.request = $"A double {o.liquid.name}, please!";
            else o.request = string.Format(ask, amount, o.liquid.name);

            return o;
        }

        static readonly Dictionary<Grade, string[]> Reactions = new Dictionary<Grade, string[]> {
            { Grade.Perfect, new[] { "Perfect pour!", "Now THAT'S a pour.", "Exactly right. Cheers!" } },
            { Grade.Great, new[] { "Nice, right on.", "Looks good to me!", "Smooth." } },
            { Grade.Good, new[] { "Close enough.", "Eh, it'll do.", "Not bad." } },
            { Grade.Short, new[] { "Hey, that's a short pour!", "Is that it?", "Where's the rest?" } },
            { Grade.Over, new[] { "Whoa, heavy hand!", "Your boss won't like that.", "That's way too much!" } },
            { Grade.Spilled, new[] { "It's all over the bar!", "You spilled it!", "Grab a towel..." } },
            { Grade.Empty, new[] { "Uh... it's empty.", "Forget it then." } },
        };

        public static string Reaction(Grade g) {
            var lines = Reactions[g];
            return lines[Random.Range(0, lines.Length)];
        }
    }
}