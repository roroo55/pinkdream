using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinkDream.Editor
{
    /// <summary>
    /// Play from the old prototype enters Start; opening a level directly still
    /// plays that level. Build order alone does not control the Editor Play button.
    /// </summary>
    [InitializeOnLoad]
    public static class GameEntryScene
    {
        private const string Prototype = "Assets/Scenes/SampleScene.unity";
        private const string Start = "Assets/Scenes/Start.unity";
        private const string OpenRequest = "Logs/OpenStartScene.request";

        static GameEntryScene()
        {
            EditorApplication.delayCall += Configure;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += ConsumeOpenRequest;
        }

        private static void OnSceneChanged(Scene previous, Scene current) => Configure();

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
                Configure();
        }

        private static void Configure()
        {
            if (Application.isBatchMode || EditorApplication.isPlaying)
                return;
            EditorSceneManager.playModeStartScene = SceneManager.GetActiveScene().path == Prototype
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(Start) : null;
        }

        [MenuItem("Pink Dream/打开 Start 开始界面")]
        public static void OpenStart()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(Start, OpenSceneMode.Single);
            Configure();
        }

        // One-shot handoff used to show the finished entry scene in the user's
        // existing Editor, without closing Unity or discarding unsaved scenes.
        private static void ConsumeOpenRequest()
        {
            if (Application.isBatchMode || !File.Exists(OpenRequest) ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    File.Delete(OpenRequest);
                    File.WriteAllText("Logs/OpenStartScene-result.txt", "Unsaved scene preserved; use Pink Dream/Open Start after saving.");
                    return;
                }
            File.Delete(OpenRequest);
            EditorSceneManager.OpenScene(Start, OpenSceneMode.Single);
            Configure();
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).Focus();
            File.WriteAllText("Logs/OpenStartScene-result.txt", "Opened Assets/Scenes/Start.unity. Direct level editing remains available.");
        }
    }
}
