// Signature-only stubs of the UnityEngine API used by the game, for compile checking outside Unity.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine {
    public class Object {
        public string name { get; set; }
        public static void Destroy(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }

    public class Component : Object {
        public GameObject gameObject => null;
        public Transform transform => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public string tag { get; set; }
    }

    public class Behaviour : Component { public bool enabled { get; set; } public bool isActiveAndEnabled => true; }

    public class YieldInstruction { }
    public class Coroutine : YieldInstruction { }
    public sealed class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }

    public class MonoBehaviour : Behaviour {
        public Coroutine StartCoroutine(IEnumerator e) => null;
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
    }

    public sealed class GameObject : Object {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform => null;
        public T AddComponent<T>() where T : Component => default;
        public Component AddComponent(Type t) => null;
        public T GetComponent<T>() => default;
        public void SetActive(bool v) { }
        public bool activeSelf => true;
        public bool activeInHierarchy => true;
        public string tag { get; set; }
    }

    public class Transform : Component, IEnumerable {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 up { get; set; }
        public Transform parent { get; set; }
        public Transform root => null;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetParent(Transform p) { }
        public Transform Find(string n) => null;
        public Vector3 TransformPoint(Vector3 p) => p;
        public void Rotate(float x, float y, float z) { }
        public void SetAsLastSibling() { }
        public void SetAsFirstSibling() { }
        public int childCount => 0;
        public IEnumerator GetEnumerator() { yield break; }
    }

    public sealed class RectTransform : Transform {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect => default;
    }

    public static class RectTransformUtility {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint) { localPoint = default; return true; }
    }

    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }

    public sealed class Camera : Behaviour {
        public static Camera main => null;
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public float aspect { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 p) => p;
    }

    public sealed class AudioListener : Behaviour { public static float volume { get; set; } public static bool pause { get; set; } }

    public class Renderer : Component {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public int sortingOrder { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
        public bool enabled { get; set; }
    }

    public sealed class SpriteRenderer : Renderer {
        public Sprite sprite { get; set; }
        public Color color { get; set; }
        public bool flipX { get; set; }
    }

    public sealed class MeshRenderer : Renderer { }
    public sealed class MeshFilter : Component { public Mesh sharedMesh { get; set; } public Mesh mesh { get; set; } }

    public sealed class Mesh : Object {
        public void Clear() { }
        public void MarkDynamic() { }
        public void SetVertices(List<Vector3> v) { }
        public void SetColors(List<Color> c) { }
        public void SetTriangles(List<int> t, int submesh) { }
        public void SetTriangles(int[] t, int submesh) { }
    }

    public enum LineAlignment { View = 0, TransformZ = 1 }
    public enum LineTextureMode { Stretch = 0, Tile = 1 }

    public sealed class LineRenderer : Renderer {
        public int positionCount { get; set; }
        public void SetPositions(Vector3[] p) { }
        public void SetPosition(int i, Vector3 p) { }
        public float widthMultiplier { get; set; }
        public float startWidth { get; set; }
        public float endWidth { get; set; }
        public Color startColor { get; set; }
        public Color endColor { get; set; }
        public bool useWorldSpace { get; set; }
        public int numCapVertices { get; set; }
        public int numCornerVertices { get; set; }
        public LineAlignment alignment { get; set; }
        public LineTextureMode textureMode { get; set; }
        public AnimationCurve widthCurve { get; set; }
    }

    public struct Keyframe { public Keyframe(float time, float value) { } }
    public class AnimationCurve { public AnimationCurve(params Keyframe[] keys) { } public float Evaluate(float t) => 0; }

    public class Shader : Object { public static Shader Find(string n) => null; }
    public class Material : Object { public Material(Shader s) { } public Color color { get; set; } }

    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public class Texture : Object { public int width => 0; public int height => 0; public TextureWrapMode wrapMode { get; set; } public FilterMode filterMode { get; set; } }
    public sealed class Texture2D : Texture { public static Texture2D whiteTexture => null; }

    public enum SpriteMeshType { FullRect, Tight }
    public sealed class Sprite : Object {
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => null;
        public Rect rect => default;
    }

    public class TextAsset : Object { public string text => ""; }
    public sealed class Font : Object { }
    public static class Resources {
        public static T Load<T>(string path) where T : Object => default;
        public static T GetBuiltinResource<T>(string path) where T : Object => default;
    }
    public static class JsonUtility { public static T FromJson<T>(string json) => default; }

    public static class Debug {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
    }

    public static class Time {
        public static float deltaTime => 0;
        public static float unscaledDeltaTime => 0;
        public static float time => 0;
        public static float unscaledTime => 0;
        public static float timeScale { get; set; }
    }

    public static class Random {
        static readonly System.Random r = new System.Random(1);
        public static float Range(float a, float b) => a + (float)r.NextDouble() * (b - a);
        public static int Range(int a, int b) => r.Next(a, b);
        public static float value => (float)r.NextDouble();
        public static Vector2 insideUnitCircle => new Vector2(value - 0.5f, value - 0.5f);
    }

    public static class Application { public static int targetFrameRate { get; set; } public static void Quit() { } public static bool isPlaying => true; }
    public static class QualitySettings { public static int vSyncCount { get; set; } }
    public static class Screen { public static int width => 1920; public static int height => 1080; }

    public static class PlayerPrefs {
        public static int GetInt(string k, int d) => d;
        public static void SetInt(string k, int v) { }
        public static float GetFloat(string k, float d) => d;
        public static void SetFloat(string k, float v) { }
        public static void Save() { }
    }

    public sealed class AudioClip : Object { public float length => 0; }
    public sealed class AudioSource : Behaviour {
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public bool isPlaying => false;
        public void Play() { }
        public void Stop() { }
        public void Pause() { }
        public void PlayScheduled(double t) { }
        public void PlayOneShot(AudioClip c, float v) { }
    }
    public static class AudioSettings { public static double dspTime => 0; }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public sealed class Canvas : Behaviour { public RenderMode renderMode { get; set; } public int sortingOrder { get; set; } }
    public sealed class CanvasGroup : Behaviour { public float alpha { get; set; } public bool blocksRaycasts { get; set; } public bool interactable { get; set; } }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    public enum KeyCode { Space, RightArrow, D, Return, KeypadEnter, E, Escape, P, Tab }
    public static class Input {
        public static Vector3 mousePosition => default;
        public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButton(int b) => false;
        public static bool GetKey(KeyCode k) => false;
        public static bool GetKeyDown(KeyCode k) => false;
    }
}

namespace UnityEngine.Rendering {
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
}

namespace UnityEngine.Events {
    public delegate void UnityAction();
    public class UnityEvent { public void AddListener(UnityAction a) { } public void RemoveAllListeners() { } public void Invoke() { } }
}

namespace UnityEngine.EventSystems {
    public class UIBehaviour : MonoBehaviour { }
    public class EventSystem : UIBehaviour {
        public static EventSystem current => null;
        public bool IsPointerOverGameObject() => false;
        public void SetSelectedGameObject(GameObject g) { }
    }
    public abstract class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class PointerEventData { }
    public interface IEventSystemHandler { }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData e); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData e); }
}

namespace UnityEngine.UI {
    using UnityEngine.EventSystems;
    public abstract class Graphic : UIBehaviour {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform => null;
    }
    public abstract class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
    }
    public class Text : MaskableGraphic {
        public Font font { get; set; }
        public string text { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool supportRichText { get; set; }
    }
    public struct ColorBlock {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
    }
    public struct Navigation { public enum Mode { None = 0, Horizontal = 1, Vertical = 2, Automatic = 3, Explicit = 4 } public Mode mode { get; set; } }
    public class Selectable : UIBehaviour {
        public Navigation navigation { get; set; }
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
        public bool interactable { get; set; }
    }
    public class Button : Selectable {
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public ButtonClickedEvent onClick { get; set; }
    }
    public class BaseMeshEffect : UIBehaviour { }
    public class Shadow : BaseMeshEffect { public Color effectColor { get; set; } public Vector2 effectDistance { get; set; } }
    public class Outline : Shadow { }
    public class CanvasScaler : UIBehaviour {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }
    public class GraphicRaycaster : UIBehaviour { }
}
