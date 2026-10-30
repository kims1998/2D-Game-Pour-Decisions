// Headless checks for the pour physics. See PhysicsTests.csproj for how to run.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PourDecisions;
using UnityEngine;

static class Program {
    /// <summary>Finds the manifest by walking up from the working directory to the repo root.</summary>
    static ArtManifest Load() {
        const string rel = "Assets/PourDecisions/Resources/Data/art_manifest.json";
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, rel))) dir = dir.Parent;
        if (dir == null) throw new FileNotFoundException("Run from inside the repo: " + rel);
        var json = File.ReadAllText(Path.Combine(dir.FullName, rel));
        return JsonSerializer.Deserialize<ArtManifest>(json, new JsonSerializerOptions { IncludeFields = true });
    }

    static Vector2[][] Pieces(BottleDef b) {
        var res = new Vector2[b.interior.Length][];
        for (int i = 0; i < res.Length; i++) {
            var pts = b.interior[i].points;
            res[i] = new Vector2[pts.Length];
            for (int j = 0; j < pts.Length; j++) res[i][j] = new Vector2(pts[j].x, pts[j].y);
        }
        return res;
    }

    public static int Main(string[] args) {
        var m = Load();
        var scratch = new List<Vector2>();
        int fails = 0;

        // ---- cup math sanity
        foreach (var g in m.glasses) {
            float v = CupMath.Volume(g.bottomRadiusCm, g.topRadiusCm, g.heightCm, g.heightCm);
            float h = CupMath.HeightForVolume(g.bottomRadiusCm, g.topRadiusCm, g.heightCm, 44.36f);
            float back = CupMath.Volume(g.bottomRadiusCm, g.topRadiusCm, g.heightCm, h);
            Console.WriteLine($"glass {g.id,-10} cap {v,6:F1} ml  1 shot = {h,5:F2} cm high ({h / m.cmPerUnit * 100,5:F1} px@100)  roundtrip {back:F3}");
            if (Math.Abs(back - 44.36f) > 0.01f) { fails++; Console.WriteLine("  ROUNDTRIP FAIL"); }
            if (Math.Abs(v - g.capacityMl) > 0.5f) { fails++; Console.WriteLine("  CAPACITY MISMATCH"); }
        }

        // ---- bottle start angles and pour times
        var spoutLen = new Dictionary<string, float>();
        foreach (var s in m.spouts) spoutLen[s.id] = s.length;
        Console.WriteLine();
        foreach (var b in m.bottles) {
            var pieces = Pieces(b);
            float total = 0; foreach (var p in pieces) total += BottleMath.Area(p);
            var liquid = Array.Find(GameData.Liquids, l => l.bottleId == b.id);
            Console.Write($"bottle {b.id,-8} ");
            foreach (var spoutId in new[] { "speed", "open" }) {
                var spout = GameData.Spout(spoutId);
                float outletY = b.neckTopY + (spout.art != null ? spoutLen[spout.art] : 0f);
                foreach (float fill in new[] { 0.15f, 0.5f, 0.85f }) {
                    float area = fill * Bottle_FullAreaFraction * total;
                    float start = -1;
                    for (float tilt = 0; tilt <= 180; tilt += 0.5f) {
                        var up = BottleMath.LocalUp(tilt);
                        float c = BottleMath.SolveSurface(pieces, up, area, scratch);
                        float head = c - Vector2.Dot(new Vector2(0, outletY), up) - liquid.minHead;
                        if (head > 0) { start = tilt; break; }
                    }
                    Console.Write($"{spoutId}/{fill:F2}:{start,5:F1}  ");
                    if (start < 0 || start < 40f) { fails++; Console.Write("<!> "); }
                }
            }
            Console.WriteLine();
        }

        // ---- simulate pouring one shot while holding 15 degrees past the start angle
        Console.WriteLine();
        foreach (var liquid in GameData.Liquids) {
            var b = Array.Find(m.bottles, x => x.id == liquid.bottleId);
            var pieces = Pieces(b);
            float total = 0; foreach (var p in pieces) total += BottleMath.Area(p);
            Console.Write($"{liquid.id,-8}");
            foreach (var spout in GameData.Spouts) {
                string art = spout.art ?? "";
                float outletY = b.neckTopY + (art != "" ? spoutLen[art] : 0f);
                float ml = b.capacityMl * 0.6f;
                var flow = new FlowModel();
                float dt = 1 / 60f, t = 0, poured = 0;
                float tiltStart = -1;
                for (float tilt = 0; tilt <= 180; tilt += 0.5f) {
                    var up = BottleMath.LocalUp(tilt);
                    float c = BottleMath.SolveSurface(pieces, up, ml / b.capacityMl * Bottle_FullAreaFraction * total, scratch);
                    if (c - Vector2.Dot(new Vector2(0, outletY), up) - liquid.minHead > 0) { tiltStart = tilt; break; }
                }
                float hold = tiltStart + 15f;
                float stopAt = -1, tail = 0;
                while (t < 20) {
                    bool holding = stopAt < 0;
                    float tilt = holding ? hold : 0f;
                    var up = BottleMath.LocalUp(tilt);
                    float c = BottleMath.SolveSurface(pieces, up, ml / b.capacityMl * Bottle_FullAreaFraction * total, scratch);
                    float head = c - Vector2.Dot(new Vector2(0, outletY), up);
                    float q = flow.Step(head, ml / b.capacityMl, liquid, spout, 1f, dt);
                    float d = q * dt; ml -= d; poured += d;
                    if (!holding) tail += d;
                    t += dt;
                    if (holding && poured >= 44.36f) stopAt = t;
                    if (!holding && q <= 0) break;
                }
                Console.Write($" {spout.id}: start {tiltStart,5:F1} shot {stopAt,4:F2}s tail {tail,4:F1}ml |");
            }
            Console.WriteLine();
        }

        // ---- stream landing: every glass x bottle x fill x tilt must put the liquid in the glass
        Console.WriteLine();
        float worstSpill = 0; string worstCase = "";
        foreach (var g in m.glasses) {
            float cpu = m.cmPerUnit;
            float rb = g.bottomRadiusCm / cpu, rt = g.topRadiusCm / cpu, H = g.heightCm / cpu, baseY = g.baseCm / cpu, wall = g.wallCm / cpu;
            float gx = K.GlassX, bottomY = K.CounterY + baseY, mouthY = bottomY + H;
            foreach (var b in m.bottles) {
                var liquid = Array.Find(GameData.Liquids, l => l.bottleId == b.id);
                var pieces = Pieces(b);
                float total = 0; foreach (var p in pieces) total += BottleMath.Area(p);
                foreach (var spout in GameData.Spouts) {
                    string art = spout.art;
                    float outletY = art != null ? b.neckTopY - 0.06f + spoutLen[art] : b.neckTopY;
                    foreach (float fill in new[] { 0.25f, 0.9f })
                    foreach (float holdExtra in new[] { 5f, 30f, 200f }) {
                        float ml = b.capacityMl * fill, poured = 0, landed = 0, spilled = 0, glassMl = 0;
                        var flow = new FlowModel();
                        var parcels = new List<(Vector2 pos, Vector2 vel, float ml, bool inside)>();
                        float dt = 1 / 60f, tilt = 0;
                        Vector2 rest = new Vector2(gx - 4.3f, K.CounterY - b.bodyBottomY * K.BottleScale);
                        float startTilt = -1;
                        for (int frame = 0; frame < 60 * 12; frame++) {
                            bool pouring = poured < 1.0f * K.ShotMl;
                            float goal = pouring ? 165f : 0f;
                            if (pouring && startTilt > 0) goal = Math.Min(165f, startTilt + holdExtra);
                            tilt = Mathf.MoveTowards(tilt, goal, (goal > tilt ? 120f : 320f) * dt);
                            var up = BottleMath.LocalUp(tilt);
                            float c = BottleMath.SolveSurface(pieces, up, ml / b.capacityMl * Bottle_FullAreaFraction * total, scratch);
                            float head = c - Vector2.Dot(new Vector2(0, outletY), up);
                            if (startTilt < 0 && head - liquid.minHead > 0) startTilt = tilt;
                            float q = flow.Step(head, ml / b.capacityMl, liquid, spout, 1f, dt);
                            float d = Math.Min(q * dt, ml);
                            Vector2 target = PourPose.OutletTarget(gx, rt, mouthY);
                            float L = outletY * K.BottleScale;
                            Vector2 pivot = PourPose.Pivot(rest, tilt, L, target);
                            float rad = tilt * Mathf.Deg2Rad;
                            Vector2 axis = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                            Vector2 outlet = pivot + axis * L;
                            if (d > 0) { ml -= d; poured += d; parcels.Add((outlet, axis * PourPose.StreamSpeed(q), d, false)); }
                            for (int i = parcels.Count - 1; i >= 0; i--) {
                                var pc = parcels[i];
                                Vector2 from = pc.pos; pc.vel.y -= K.Gravity * dt; Vector2 to = from + pc.vel * dt;
                                bool done = false;
                                if (!pc.inside && from.y >= mouthY && to.y < mouthY) {
                                    float t = (from.y - mouthY) / (from.y - to.y); float x = from.x + (to.x - from.x) * t; float dx = Math.Abs(x - gx);
                                    if (dx < rt) pc.inside = true; else if (dx < rt + wall + 0.02f) { spilled += pc.ml; done = true; }
                                }
                                if (!done && pc.inside) {
                                    float lvl = CupMath.HeightForVolume(g.bottomRadiusCm, g.topRadiusCm, g.heightCm, glassMl) / cpu;
                                    float r = Mathf.Lerp(rb, rt, (to.y - bottomY) / H) - 0.015f;
                                    if (Math.Abs(to.x - gx) > r) { to.x = gx + Math.Sign(to.x - gx) * r; pc.vel.x *= -0.25f; }
                                    if (to.y <= bottomY + lvl) { float acc = Math.Min(pc.ml, g.capacityMl - glassMl); glassMl += acc; spilled += pc.ml - acc; landed += pc.ml; done = true; }
                                }
                                if (!done && !pc.inside && to.y <= K.CounterY) { spilled += pc.ml; done = true; }
                                if (done) parcels.RemoveAt(i); else { pc.pos = to; parcels[i] = pc; }
                            }
                            if (!pouring && q <= 0 && parcels.Count == 0 && tilt < 1) break;
                        }
                        float frac = poured > 0 ? spilled / poured : 0;
                        if (frac > worstSpill) { worstSpill = frac; worstCase = $"{g.id}/{b.id}/{spout.id}/fill{fill}/+{holdExtra}"; }
                        if (frac > 0.02f && glassMl < g.capacityMl - 1) { fails++; Console.WriteLine($"SPILL {frac:P1} {g.id} {b.id} {spout.id} fill {fill} hold+{holdExtra} poured {poured:F1}"); }
                    }
                }
            }
        }
        Console.WriteLine($"stream test: worst non-overflow spill fraction {worstSpill:P2} ({worstCase})");

        // ---- every level must be able to produce a doable order
        for (int li = 0; li < GameData.Levels.Length; li++) {
            var lv = GameData.Levels[li];
            int ok = 0, total2 = 0;
            foreach (var gid in lv.glasses) foreach (var lid in lv.liquids) foreach (var sid in lv.spouts) foreach (var sh in lv.targetShots) {
                total2++;
                var gg = Array.Find(m.glasses, x => x.id == gid); var ll = GameData.Liquid(lid); var ss = GameData.Spout(sid);
                if (gg == null || ll == null || ss == null) { fails++; Console.WriteLine($"BAD ID {gid}/{lid}/{sid}"); continue; }
                float tml = sh * K.ShotMl;
                if (tml <= gg.capacityMl * 0.8f && tml / (ss.maxFlowMlPerSec * ll.flowMultiplier) <= lv.secondsPerOrder * 0.4f) ok++;
            }
            Console.WriteLine($"Level {li + 1} ({lv.name}): {ok}/{total2} combos doable");
            if (ok == 0) fails++;
        }

        // ---- grading table
        Console.WriteLine();
        foreach (float poured in new[] { 30f, 40f, 42.5f, 44f, 46f, 49f, 55f }) {
            var r = Scoring.Evaluate(poured, 44.36f, 0, false, 0.5f, 0, false);
            Console.WriteLine($"poured {poured,5:F1} -> {r.grade,-8} {r.points,4} pts acc {r.accuracy:F0}%");
        }
        Console.WriteLine(fails == 0 ? "\nALL CHECKS PASSED" : $"\n{fails} CHECK(S) FAILED");
        return fails == 0 ? 0 : 1;
    }

    const float Bottle_FullAreaFraction = 0.88f;
}
