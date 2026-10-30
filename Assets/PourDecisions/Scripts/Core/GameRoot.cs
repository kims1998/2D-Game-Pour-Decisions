using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PourDecisions {
    /*
    * Starts the game in any scene: no prefabs or scene setup needed. Press Play and the whole game
    * (camera, world, audio, UI) is built from code.
    */
    public static class Bootstrap {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() {
            // Keeps things correct when "Enter Play Mode Options" skips the domain reload.
            GameRoot.ResetStatics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch() {
            if (GameRoot.I != null) return;
            HideScenePreview();
            new GameObject("Pour Decisions").AddComponent<GameRoot>();
        }

        // The game builds its own background, counter, customer, glass and bottle at runtime. A scene that
        // also has copies of them placed in the editor (a layout preview) would show everything twice, so while the
        // game runs we switch off every top-level object except cameras. Nothing is deleted from the scene.
        static void HideScenePreview() {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) {
                if (root.GetComponentInChildren<Camera>(true) != null) continue;
                root.SetActive(false);
            }
        }
    }


    // Owns every system and switches between screens (title, level select, shift, results, pause).    
    public class GameRoot : MonoBehaviour {
        public static GameRoot I { get; private set; }
        public static bool Paused { get; private set; }

        enum ScreenState { Title, Levels, Playing, Results }

        Stage stage;
        PourStream stream;
        Customer customer;
        ShiftController shift;
        Hud hud;
        TitleScreen title;
        LevelSelectScreen levels;
        PauseMenu pause;
        ResultsScreen results;
        ScreenState screen;
        GameMode lastMode;
        int lastLevel;

        public static void ResetStatics() {
            I = null;
            Paused = false;
        }

        void Awake() {
            // GameRoot is a singleton: one instance, reachable anywhere as GameRoot.I.
            I = this;
            DontDestroyOnLoad(gameObject);
            Time.timeScale = 1f;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;

            // Build the game in order: camera, audio, set, effects, stream, customer, UI, then the shift controller.
            var cam = SetupCamera();
            gameObject.AddComponent<AudioManager>();

            stage = new GameObject("Stage").AddComponent<Stage>();
            stage.transform.SetParent(transform, false);
            stage.Build(cam);

            var fx = new GameObject("Fx").AddComponent<Fx>();
            fx.transform.SetParent(transform, false);

            stream = new GameObject("PourStream").AddComponent<PourStream>();
            stream.transform.SetParent(transform, false);

            customer = new GameObject("Customer").AddComponent<Customer>();
            customer.transform.SetParent(transform, false);
            customer.Init();

            var canvasRoot = SetupCanvas();
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(canvasRoot, cam);

            title = new TitleScreen();
            title.Build(canvasRoot);
            levels = new LevelSelectScreen();
            levels.Build(canvasRoot);
            results = new ResultsScreen();
            results.Build(canvasRoot);
            pause = new PauseMenu();
            pause.Build(canvasRoot);

            shift = gameObject.AddComponent<ShiftController>();
            shift.Init(stage, stream, customer, hud);
        }

        void Start() => ShowTitle();

        static Camera SetupCamera() {
            var cam = Camera.main;

            if (cam == null) {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }

            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            return cam;
        }

        // Creates the full-screen UI canvas.
        public static RectTransform CreateCanvas(Transform parent) {
            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            return (RectTransform)go.transform;
        }

        RectTransform SetupCanvas() {
            var canvas = CreateCanvas(transform);
            // UI buttons only work if the scene has an EventSystem. Add one if nothing else did.
            if (EventSystem.current == null) {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                #if ENABLE_INPUT_SYSTEM
                var module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
                #else
                es.AddComponent<StandaloneInputModule>();
                #endif
            }

            return canvas;
        }

        // Only one full-screen menu shows at a time. Pass null to hide them all while playing.
        void ShowOnly(RectTransform active) {
            title.Root.gameObject.SetActive(active == title.Root);
            levels.Root.gameObject.SetActive(active == levels.Root);
            results.Root.gameObject.SetActive(active == results.Root);
            pause.Root.gameObject.SetActive(false);
        }

        void SetPaused(bool p) {
            Paused = p;
            // timeScale 0 freezes everything that uses Time.deltaTime, which is how pause works.
            Time.timeScale = p ? 0f : 1f;
            pause.Root.gameObject.SetActive(p);
            if (p) AudioManager.I.StopPour();
        }

        public void ShowTitle() {
            SetPaused(false);
            shift.Stop();
            hud.SetVisible(false);
            screen = ScreenState.Title;
            ShowOnly(title.Root);
            title.Refresh();
        }

        public void ShowLevelSelect() {
            screen = ScreenState.Levels;
            ShowOnly(levels.Root);
            levels.Refresh();
        }

        public void StartGame(GameMode mode, int level) {
            SetPaused(false);
            lastMode = mode;
            lastLevel = level;
            screen = ScreenState.Playing;
            ShowOnly(null);
            shift.Begin(mode, level);
        }

        public void Restart() => StartGame(lastMode, lastLevel);

        public void NextLevel() => StartGame(GameMode.Classic, Mathf.Min(lastLevel + 1, GameData.Levels.Length - 1));

        public void ShowResults(ShiftSummary s) {
            screen = ScreenState.Results;
            ShowOnly(results.Root);
            results.Show(s);
        }

        public void TogglePause() {
            if (screen != ScreenState.Playing || !shift.Running) return;
            SetPaused(!Paused);
        }

        public static void Quit() {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        void Update() {
            if (!PourInput.PausePressed) return;

            switch (screen) {
                case ScreenState.Playing: 
                    TogglePause(); 
                    break;
                case ScreenState.Levels:
                case ScreenState.Results: 
                    ShowTitle();
                    break;
            }
        }
    }
}
