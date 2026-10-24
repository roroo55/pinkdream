using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    /// <summary>Updates only the first level's ending and saves two blank scenes.</summary>
    public static class BirthEndingSceneAuthoring
    {
        private const string BirthPath = "Assets/Scenes/Level01_BeforeBirth.unity";
        private const string TransitionPath = "Assets/Scenes/Transition_Blank.unity";
        private const string NextPath = "Assets/Scenes/Level02_FirstBirthday.unity";
        private static Color gameBackground = new Color(0.98f, 0.928f, 0.943f, 1);

        public static void Apply()
        {
            var birthScene = EditorSceneManager.OpenScene(BirthPath);
            BeforeBirthLevel level = Object.FindObjectOfType<BeforeBirthLevel>();
            if (Object.FindObjectOfType<BirthCompletionSequence>() != null)
                throw new System.InvalidOperationException("The ending is already authored. Preserve its scene edits.");
            var levelData = new SerializedObject(level);
            Camera camera = (Camera)levelData.FindProperty("interactionCamera").objectReferenceValue;
            gameBackground = camera.backgroundColor;
            var particles = ReadArray<ParticleSystem>(levelData, "pinkConfetti");
            var balloons = ReadArray<Transform>(levelData, "pinkBalloons");
            Transform effects = particles[0].transform.parent;

            int originalParticles = particles.Count;
            while (particles.Count < 12)
            {
                ParticleSystem copy = Object.Instantiate(particles[particles.Count % originalParticles], effects);
                particles.Add(copy);
            }
            for (int i = 0; i < particles.Count; i++)
                ConfigureConfetti(particles[i], camera, i);

            int originalBalloons = balloons.Count;
            while (balloons.Count < 18)
            {
                Transform copy = Object.Instantiate(balloons[balloons.Count % originalBalloons], effects);
                balloons.Add(copy);
            }
            float height = camera.orthographicSize * 2;
            float width = height * (16f / 9f);
            for (int i = 0; i < balloons.Count; i++)
            {
                balloons[i].name = "底部放飞气球 " + (i + 1);
                float x = 0.12f + i % 9 * 0.095f;
                float y = -0.045f - i / 9 * 0.045f;
                balloons[i].position = new Vector3((x - 0.5f) * width, (y - 0.5f) * height, 0);
                balloons[i].gameObject.SetActive(false);
            }
            WriteArray(levelData, "pinkConfetti", particles);
            WriteArray(levelData, "pinkBalloons", balloons);

            var ending = new GameObject("出生前 · 完成演出与时间设置").AddComponent<BirthCompletionSequence>();
            var ui = new GameObject("完成演出 · 可编辑转场", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            var cover = new GameObject("转场 · 全屏背景色遮罩", typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasRenderer), typeof(Image));
            cover.transform.SetParent(ui.transform, false);
            RectTransform coverRect = cover.GetComponent<RectTransform>();
            coverRect.anchorMin = Vector2.zero;
            coverRect.anchorMax = Vector2.one;
            coverRect.offsetMin = coverRect.offsetMax = Vector2.zero;
            cover.GetComponent<Image>().color = gameBackground;
            cover.GetComponent<Image>().raycastTarget = false;
            CanvasGroup coverGroup = cover.GetComponent<CanvasGroup>();
            coverGroup.alpha = 1;
            cover.SetActive(false);

            var endingData = new SerializedObject(ending);
            endingData.FindProperty("sceneCamera").objectReferenceValue = camera;
            WriteArray(endingData, "confetti", particles);
            WriteArray(endingData, "balloons", balloons);
            endingData.FindProperty("whiteCover").objectReferenceValue = coverGroup;
            endingData.ApplyModifiedPropertiesWithoutUndo();
            levelData.FindProperty("completionSequence").objectReferenceValue = ending;
            levelData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(birthScene);
            EditorSceneManager.SaveScene(birthScene);

            CreateBlankScene(TransitionPath, true);
            CreateBlankScene(NextPath, false);
            var buildScenes = new List<EditorBuildSettingsScene>();
            bool inserted = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == TransitionPath || scene.path == NextPath)
                    continue;
                buildScenes.Add(scene);
                if (scene.path == BirthPath)
                {
                    buildScenes.Add(new EditorBuildSettingsScene(TransitionPath, true));
                    buildScenes.Add(new EditorBuildSettingsScene(NextPath, true));
                    inserted = true;
                }
            }
            if (!inserted)
            {
                buildScenes.Add(new EditorBuildSettingsScene(TransitionPath, true));
                buildScenes.Add(new EditorBuildSettingsScene(NextPath, true));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Birth ending authored: fullscreen celebration, editable 1s hold, fade and blank transition. No Play mode or tests executed.");
        }

        public static void SaveAuthoredEnding()
        {
            var scene = EditorSceneManager.OpenScene(BirthPath);
            var ending = Object.FindObjectOfType<BirthCompletionSequence>();
            var data = new SerializedObject(ending);
            // Hidden at first, but immediately visible if enabled while editing.
            foreach (string field in new[] { "whiteCover" })
            {
                var group = (CanvasGroup)data.FindProperty(field).objectReferenceValue;
                group.alpha = 1;
                group.gameObject.SetActive(false);
                EditorUtility.SetDirty(group);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the authored birth ending and editable UI visibility. No Play mode or tests executed.");
        }

        public static void UpdateEmissionAndPalette()
        {
            var scene = EditorSceneManager.OpenScene(BirthPath);
            var level = Object.FindObjectOfType<BeforeBirthLevel>();
            var levelData = new SerializedObject(level);
            Camera camera = (Camera)levelData.FindProperty("interactionCamera").objectReferenceValue;
            gameBackground = camera.backgroundColor;
            var particles = ReadArray<ParticleSystem>(levelData, "pinkConfetti");
            var balloons = ReadArray<Transform>(levelData, "pinkBalloons");
            for (int i = 0; i < particles.Count; i++)
                ConfigureConfetti(particles[i], camera, i);
            float height = camera.orthographicSize * 2;
            float width = height * 16f / 9f;
            for (int i = 0; i < balloons.Count; i++)
            {
                balloons[i].name = "底部放飞气球 " + (i + 1);
                balloons[i].position = new Vector3((0.12f + i % 9 * 0.095f - 0.5f) * width,
                    (-0.045f - i / 9 * 0.045f - 0.5f) * height, 0);
                balloons[i].gameObject.SetActive(false);
            }
            var ending = Object.FindObjectOfType<BirthCompletionSequence>();
            var endingData = new SerializedObject(ending);
            var cover = (CanvasGroup)endingData.FindProperty("whiteCover").objectReferenceValue;
            cover.name = "转场 · 全屏背景色遮罩";
            cover.GetComponent<Image>().color = gameBackground;
            EditorUtility.SetDirty(cover.GetComponent<Image>());
            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            foreach (string path in new[] { TransitionPath, NextPath })
            {
                var blank = EditorSceneManager.OpenScene(path);
                Camera.main.backgroundColor = gameBackground;
                EditorUtility.SetDirty(Camera.main);
                EditorSceneManager.MarkSceneDirty(blank);
                EditorSceneManager.SaveScene(blank);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved matching background colors, inward side confetti jets and bottom balloon release. No Play mode or tests executed.");
        }

        private static void ConfigureConfetti(ParticleSystem particles, Camera camera, int index)
        {
            bool left = index % 2 == 0;
            int lane = index / 2;
            particles.name = (left ? "左侧向内礼花 " : "右侧向内礼花 ") + (lane + 1);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float height = camera.orthographicSize * 2;
            float width = height * 16f / 9f;
            particles.transform.position = new Vector3((left ? -0.475f : 0.475f) * width,
                (0.10f + lane * 0.018f - 0.5f) * height, 0);
            float angle = (28f + lane % 3 * 9f) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3((left ? 1 : -1) * Mathf.Cos(angle), Mathf.Sin(angle), 0);
            particles.transform.rotation = Quaternion.LookRotation(direction, Vector3.forward);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.7f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6.375f, 8.625f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.12f);
            main.gravityModifier = 0.55f;
            main.maxParticles = 220;
            var emission = particles.emission;
            emission.rateOverTime = 18;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)12) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11;
            shape.radius = 0.10f;
            shape.scale = Vector3.one;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = false;
            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            particles.GetComponent<ParticleSystemRenderer>().sortingOrder = 500;
        }

        private static void CreateBlankScene(string path, bool transition)
        {
            if (File.Exists(path))
                return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = gameBackground;
            if (transition)
                new GameObject("空白转场 · 停留时间与下一关设置").AddComponent<BlankTransition>();
            else
                new GameObject("第二关 · 内容预留（空场景）");
            EditorSceneManager.SaveScene(scene, path);
        }

        private static List<T> ReadArray<T>(SerializedObject data, string name) where T : Object
        {
            SerializedProperty property = data.FindProperty(name);
            var result = new List<T>();
            for (int i = 0; i < property.arraySize; i++)
                result.Add((T)property.GetArrayElementAtIndex(i).objectReferenceValue);
            return result;
        }

        private static void WriteArray<T>(SerializedObject data, string name, List<T> values) where T : Object
        {
            SerializedProperty property = data.FindProperty(name);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
