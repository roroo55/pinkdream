using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    // Display text only: scene object names, gameplay references and assets stay intact.
    public static class EnglishTypographyAuthoring
    {
        public const string FontPath = "Assets/art/Date Night.ttf";
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "把放大镜拖到 B 超屏幕，找到 START 后停留至少 1 秒。", "Find the baby with the magnifier Hold still for one second to START" },
            { "拖动放大镜，在 B 超屏幕上找到 START。", "Use the magnifier to find START on the ultrasound" },
            { "把旁边的放大镜拖到 B 超屏幕上，父母也可以拖动。", "Move the magnifier over the ultrasound You can drag the parents too" },
            { "找到 START 了，停住放大镜至少 1 秒……", "START found Hold the magnifier still for one second" },
            { "拖动手到 START，开始游戏", "Drag a hand to START to begin" },
            { "手牵手，一起握刀切开蛋糕", "Hold hands and cut the cake together" },
            { "抓住你喜欢的东西吧。", "Reach for something you love" },
            { "拖动宝宝的手，或把喜欢的小物件送到手边。", "Drag a hand or bring an item within reach" },
            { "已抓到物品，即将进入下一关。", "Item caught Moving to the next stage" },
            { "整理书包：把粉色的东西全部装进去", "Pack all the pink items in the schoolbag" },
            { "拖动女孩的手拿起物品，放进书包里，再松手。", "Pick up an item put it in the bag then release" },
            { "整理书包  0 / 5", "Packed zero of five" },
            { "把物品放在这里\n松手收好", "DROP HERE\nRELEASE" },
            { "已全部收好，即将进入下一关。", "All packed Moving to the next stage" },
            { "拿起穿耳器，完成两侧耳洞", "Pick up the tool and pierce both ears" },
            { "拖动女孩的手拿起穿耳器，完成两侧耳洞（0 / 2）。", "Use her hand to pick up the piercing tool Ears zero of two" },
            { "让穿耳器的银色前端碰到耳垂，再松手。两侧都要完成（0 / 2）。", "Touch an earlobe with the silver tip then release Ears zero of two" },
            { "已完成一侧耳洞（1 / 2），继续完成另一侧。", "One ear done Pierce the other ear Ears one of two" },
            { "两侧耳洞已完成（2 / 2），即将进入下一关。", "Both ears done Moving to the next stage" },
            { "向下拉\n装扮完毕", "PULL DOWN\nTO FINISH" },
            { "拿起喜欢的物品，把它变成蛋糕上的装饰", "Turn the things you love into cake decorations" },
            { "已组合 0 种装饰 · 可以继续，也可以随时完成", "Decorations zero keep creating or finish whenever you like" },
            { "拖动女孩的手碰到物品，拿起后把手带到蛋糕旁。", "Pick up an item with her hand then move it to the cake" },
            { "还可以继续组合。装扮好了，就拉下右边的拉绳。", "Add more or pull the cord on the right when you are ready" },
            { "点击空白处，返回 Start 重新开始游戏。", "Click an empty area to return to START" },
            { "按住头、身体或四肢拖拽，摆出你想要的姿势", "Drag the head body or limbs to strike a pose" },
            { "鼠标停住就停住 · 松手保持姿势", "Move the mouse to pose Release to hold" },
            { "剪刀", "Scissors" },
            { "鼠标", "Computer mouse" },
            { "画笔", "Paintbrush" },
            { "红包", "Red envelope" },
            { "尤克里里", "Ukulele" },
            { "粉色布娃娃", "Pink doll" },
            { "粉色铅笔", "Pink pencil" },
            { "粉色橡皮", "Pink eraser" },
            { "粉色笔记本", "Pink notebook" },
            { "粉色故事书", "Pink storybook" },
            { "种子", "Seed" },
            { "星星", "Star" },
            { "蝴蝶结", "Ribbon" },
            { "相机", "Camera" },
        };

        public static string Translate(string text)
            => text != null && English.TryGetValue(text, out string translated) ? translated : text;

        [MenuItem("Pink Dream/Apply Date Night and English text")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) throw new InvalidOperationException("Date Night font is missing.");
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Text label in root.GetComponentsInChildren<Text>(true))
                    {
                        label.text = Translate(label.text);
                        if (HasNonEnglishText(label.text)) throw new InvalidOperationException("Untranslated text: " + label.name + " / " + label.text);
                        label.font = font;
                        label.resizeTextForBestFit = true;
                        label.resizeTextMinSize = Mathf.Min(16, label.fontSize);
                        label.resizeTextMaxSize = label.fontSize;
                        EditorUtility.SetDirty(label); changed = true;
                    }
                    foreach (FrozenRagdoll ragdoll in root.GetComponentsInChildren<FrozenRagdoll>(true))
                    {
                        var data = new SerializedObject(ragdoll);
                        data.FindProperty("uiFont").objectReferenceValue = font;
                        data.ApplyModifiedPropertiesWithoutUndo(); changed = true;
                    }
                    foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!(behaviour is BabyBirthdayLevel || behaviour is ChildhoodPackingLevel || behaviour is ComingOfAgeLevel)) continue;
                        var data = new SerializedObject(behaviour);
                        var items = data.FindProperty(behaviour is ComingOfAgeLevel ? "choices" : "items");
                        for (int i=0; i<items.arraySize; i++)
                        {
                            var name=items.GetArrayElementAtIndex(i).FindPropertyRelative("itemName");
                            name.stringValue=Translate(name.stringValue);
                            if (HasNonEnglishText(name.stringValue)) throw new InvalidOperationException("Untranslated item: " + name.stringValue);
                        }
                        data.ApplyModifiedPropertiesWithoutUndo(); changed=true;
                    }
                }
                if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ENGLISH TYPOGRAPHY SAVED: Date Night applied to every scene label and prototype overlay; prompts and item display names translated.");
        }
        private static bool HasNonEnglishText(string text)
        {
            if (text == null) return false;
            foreach (char c in text) if (c > 127) return true;
            return false;
        }
    }
}
