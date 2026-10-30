using UnityEngine;

namespace PourDecisions {
    // Player options and progress, stored in PlayerPrefs.
    public static class Settings {
        const string HardKey = "pd_hard";
        const string SensKey = "pd_sens";
        const string HintKey = "pd_hint_seen";

        // PlayerPrefs stores small values on disk between sessions. Each property below reads and
        // writes one key, so the rest of the code can just use Settings.HardMode like a normal variable.

        // Keyboard-only tilting. Less precise, so it pays 25% more points.
        public static bool HardMode {
            get => PlayerPrefs.GetInt(HardKey, 0) == 1;
            set { PlayerPrefs.SetInt(HardKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // Mouse drag sensitivity multiplier (0.5 to 2).
        public static float Sensitivity {
            get => PlayerPrefs.GetFloat(SensKey, 1f);
            set { PlayerPrefs.SetFloat(SensKey, Mathf.Clamp(value, 0.5f, 2f)); PlayerPrefs.Save(); }
        }

        // The controls hint shows until the player has finished one pour.
        public static bool HintSeen {
            get => PlayerPrefs.GetInt(HintKey, 0) == 1;
            set { PlayerPrefs.SetInt(HintKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ------------------------------------------------------------ progress

        // Highest Classic level index the player can start (0-based).
        public static int UnlockedLevel() => PlayerPrefs.GetInt("pd_unlock", 0);

        public static void Unlock(int level) {
            if (level > UnlockedLevel()) { PlayerPrefs.SetInt("pd_unlock", level); PlayerPrefs.Save(); }
        }

        public static int Stars(int level) => PlayerPrefs.GetInt($"pd_stars_{level}", 0);

        public static void RecordStars(int level, int stars) {
            if (stars > Stars(level)) { PlayerPrefs.SetInt($"pd_stars_{level}", stars); PlayerPrefs.Save(); }
        }

        public static int Best(GameMode m, int level) => PlayerPrefs.GetInt($"pd_best_{(int)m}_{level}", 0);

        // Saves a score if it beats the old best. Returns true for a new record.
        public static bool RecordBest(GameMode m, int level, int score) {
            if (score <= Best(m, level)) return false;
            PlayerPrefs.SetInt($"pd_best_{(int)m}_{level}", score);
            PlayerPrefs.Save();
            
            return true;
        }
    }
}
