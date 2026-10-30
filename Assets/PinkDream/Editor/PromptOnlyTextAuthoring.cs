using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    public static class PromptOnlyTextAuthoring
    {
        private static readonly HashSet<string> PromptNames = new HashSet<string>
        {
            "操作说明", "开始提示", "拖拽提示", "顶部文字提示", "关卡提示",
            "操作提示", "抓取反馈", "整理进度", "自由组合进度"
        };

        public static void Apply()
        {
            foreach (string path in SceneFlowAuthoring.PlayableScenes)
            {
                var scene = EditorSceneManager.OpenScene(path);
                int removed = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform view in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (view == null)
                            continue;
                        if (view.name.StartsWith("对话 ·"))
                        {
                            Object.DestroyImmediate(view.gameObject);
                            removed++;
                        }
                        else if (view.name == "完成演出 · 可编辑对话与转场")
                            view.name = "完成演出 · 可编辑转场";
                    }
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Text text in root.GetComponentsInChildren<Text>(true))
                    {
                        if (text == null || IsPrompt(text))
                            continue;
                        Canvas canvas = text.GetComponentInParent<Canvas>();
                        // A label's world canvas contains only that label, never item geometry.
                        GameObject target = canvas != null && canvas.renderMode == RenderMode.WorldSpace &&
                            canvas.GetComponentsInChildren<Text>(true).Length == 1 ? canvas.gameObject : text.gameObject;
                        Object.DestroyImmediate(target);
                        removed++;
                    }
                var start = Object.FindObjectOfType<StartSceneController>();
                if (start != null)
                {
                    var data = new SerializedObject(start);
                    var instruction = (Text)data.FindProperty("instructionText").objectReferenceValue;
                    instruction.text = EnglishTypographyAuthoring.Translate("把放大镜拖到 B 超屏幕，找到 START 后停留至少 1 秒。");
                    EditorUtility.SetDirty(instruction);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Saved prompt-only text in " + scene.name + "; removed " + removed + " dialogue/title/label objects. No Play mode or tests executed.");
            }
            AssetDatabase.SaveAssets();
        }

        private static bool IsPrompt(Text text) => PromptNames.Contains(text.name) || text.text == "START" ||
            EnglishTypographyAuthoring.Translate(text.text) == "DROP HERE\nRELEASE" || EnglishTypographyAuthoring.Translate(text.text) == "PULL DOWN\nTO FINISH";
    }
}
