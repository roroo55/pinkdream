using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    public static class SceneFlowAuthoring
    {
        public static readonly string[] PlayableScenes =
        {
            "Assets/Scenes/Start.unity",
            "Assets/Scenes/Level01_BeforeBirth.unity",
            "Assets/Scenes/Level02_FirstBirthday.unity",
            "Assets/Scenes/Level03_ChildhoodPacking.unity",
            "Assets/Scenes/Level04_AdolescencePiercing.unity",
            "Assets/Scenes/Level05_ComingOfAge.unity"
        };

        public static void Apply()
        {
            foreach (string path in PlayableScenes)
            {
                var scene = EditorSceneManager.OpenScene(path);
                Camera camera = Camera.main;
                var transition = Object.FindObjectOfType<SceneFadeTransition>();
                if (transition == null)
                {
                    var go = new GameObject("统一转场 · 淡出与淡入时间设置", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    Canvas canvas = go.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 32760;
                    var scaler = go.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1600, 900);
                    scaler.matchWidthOrHeight = 0.5f;
                    var cover = new GameObject("与游戏背景同色 · 全屏遮罩", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                    cover.transform.SetParent(go.transform, false);
                    RectTransform rect = cover.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    Image image = cover.GetComponent<Image>();
                    image.color = camera.backgroundColor;
                    image.raycastTarget = true;
                    CanvasGroup group = cover.GetComponent<CanvasGroup>();
                    group.alpha = 0;
                    group.blocksRaycasts = false;
                    group.interactable = false;
                    transition = go.AddComponent<SceneFadeTransition>();
                    var data = new SerializedObject(transition);
                    data.FindProperty("sceneCamera").objectReferenceValue = camera;
                    data.FindProperty("cover").objectReferenceValue = group;
                    data.FindProperty("coverImage").objectReferenceValue = image;
                    data.FindProperty("fadeOutSeconds").floatValue = 0.7f;
                    data.FindProperty("fadeInSeconds").floatValue = 0.7f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var birth = Object.FindObjectOfType<BirthCompletionSequence>();
                if (birth != null)
                {
                    var birthData = new SerializedObject(birth);
                    birthData.FindProperty("nextScene").stringValue = "Level02_FirstBirthday";
                    birthData.ApplyModifiedPropertiesWithoutUndo();
                }
                var celebration = Object.FindObjectOfType<ComingOfAgeCelebration>();
                if (celebration != null)
                {
                    var celebrationData = new SerializedObject(celebration);
                    var levelData = new SerializedObject(Object.FindObjectOfType<ComingOfAgeLevel>());
                    celebrationData.FindProperty("restartHint").objectReferenceValue = levelData.FindProperty("feedbackText").objectReferenceValue;
                    celebrationData.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved background-colour fade overlays in all six playable scenes and the birthday restart prompt binding. Existing level content, two-ear progress and decoration combinations preserved. No Play mode or tests executed.");
        }
    }
}
