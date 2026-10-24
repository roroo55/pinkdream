using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    public static class ChildhoodPackingSceneAuthoring
    {
        private const string OldPath = "Assets/Scenes/Level03_Placeholder.unity";
        private const string LevelPath = "Assets/Scenes/Level03_ChildhoodPacking.unity";
        private const string NextPath = "Assets/Scenes/Level04_AdolescencePiercing.unity";
        private static Sprite circle, square;
        private static Material material;
        private static Font font;
        private static readonly Color Ink = new Color32(91, 54, 73, 255);
        private static readonly Color Pink = new Color32(225, 147, 180, 255);
        private static readonly Color LightPink = new Color32(250, 209, 223, 255);
        private static readonly Color DeepPink = new Color32(184, 95, 137, 255);

        public static void Apply()
        {
            if (File.Exists(LevelPath))
                throw new System.InvalidOperationException("The childhood level already exists; preserve its scene edits.");
            circle = LoadSprite("Assets/PinkDream/Geometry/Circle.asset");
            square = LoadSprite("Assets/PinkDream/Geometry/Square.asset");
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PinkDream/Geometry/RagdollGeometry.mat");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/PinkDream/Geometry/SceneUIFont.ttf");
            string error = AssetDatabase.MoveAsset(OldPath, LevelPath);
            if (!string.IsNullOrEmpty(error))
                throw new System.InvalidOperationException(error);
            Scene scene = EditorSceneManager.OpenScene(LevelPath);
            Camera camera = Camera.main;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != camera.gameObject)
                    Object.DestroyImmediate(root);
            camera.orthographicSize = 4.4f;

            Transform floor = Group("童年 · 整理区域", null, new Vector2(0, -1.6f));
            Shape(floor, "地毯外缘", circle, Vector2.zero, new Vector2(10.7f, 3.4f), new Color32(232, 188, 206, 255), -100);
            Shape(floor, "淡粉地毯", circle, Vector2.zero, new Vector2(10.56f, 3.26f), new Color32(252, 228, 236, 255), -99);

            var girlObject = new GameObject("女孩 · 童年布娃娃");
            girlObject.transform.position = new Vector3(-0.55f, 0.25f, 0);
            var girl = girlObject.AddComponent<FrozenRagdoll>();
            var girlData = new SerializedObject(girl);
            Ref(girlData, "interactionCamera", camera);
            Ref(girlData, "geometryShader", material.shader);
            girlData.FindProperty("showPrototypeOverlay").boolValue = false;
            girlData.FindProperty("bodyScale").floatValue = 0.78f;
            girlData.FindProperty("headScale").floatValue = 1.3f;
            girlData.FindProperty("seatedRestPose").boolValue = false;
            girlData.ApplyModifiedPropertiesWithoutUndo();
            girl.BakeSceneGeometry();
            girlData = new SerializedObject(girl);
            var bodyColors = girlData.FindProperty("bodyColors");
            for (int i = 0; i < bodyColors.arraySize; i++)
            {
                if (i == 1 || i == 3 || i == 5)
                    continue;
                var body = (SpriteRenderer)bodyColors.GetArrayElementAtIndex(i).objectReferenceValue;
                foreach (SpriteRenderer renderer in body.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = i >= 6 ? LightPink : Pink;
            }
            foreach (SpriteRenderer renderer in girl.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.name == "Foot")
                    renderer.color = DeepPink;
                if (renderer.name == "Hand outline")
                    renderer.sortingOrder = 330;
                if (renderer.name == "Hand")
                    renderer.sortingOrder = 331;
            }
            Transform leftArm = girl.transform.Find("Geometric ragdoll/Left forearm");
            Transform rightArm = girl.transform.Find("Geometric ragdoll/Right forearm");
            Transform leftHand = Group("左手抓取锚点", leftArm, new Vector2(0, -girl.Model.Parts[3].Length * 0.5f));
            Transform rightHand = Group("右手抓取锚点", rightArm, new Vector2(0, -girl.Model.Parts[5].Length * 0.5f));

            Transform bag = Group("粉色书包", null, new Vector2(3.65f, -0.65f));
            Shape(bag, "书包背部轮廓", square, new Vector2(0, -0.12f), new Vector2(2.68f, 2.86f), Ink, 100);
            Shape(bag, "书包背部", square, new Vector2(0, -0.12f), new Vector2(2.58f, 2.76f), Pink, 101);
            Shape(bag, "提手外侧", circle, new Vector2(0, 1.39f), new Vector2(0.92f, 0.72f), Ink, 102);
            Shape(bag, "粉色提手", circle, new Vector2(0, 1.39f), new Vector2(0.78f, 0.58f), DeepPink, 103);
            Shape(bag, "提手中空", circle, new Vector2(0, 1.43f), new Vector2(0.47f, 0.32f), camera.backgroundColor, 104);
            Shape(bag, "敞开的书包内部", square, new Vector2(0, 0.18f), new Vector2(2.36f, 2.1f), new Color32(156, 79, 117, 255), 110);
            // Packed items stay at the drop pose, behind this existing front panel.
            Shape(bag, "书包前袋轮廓", square, new Vector2(0, -0.14f), new Vector2(2.56f, 2.66f), Ink, 140);
            Shape(bag, "书包前袋", square, new Vector2(0, -0.14f), new Vector2(2.46f, 2.56f), Pink, 141);
            Shape(bag, "放置区底色", square, new Vector2(0, 0.17f), new Vector2(2.12f, 1.76f), new Color32(238, 174, 201, 255), 142);
            Shape(bag, "前袋缝线", square, new Vector2(0, -0.86f), new Vector2(1.98f, 0.035f), LightPink, 143);
            Shape(bag, "粉色口袋", square, new Vector2(0, -1.10f), new Vector2(1.75f, 0.37f), DeepPink, 143);
            Shape(bag, "书包口", circle, new Vector2(0, 1.14f), new Vector2(2.38f, 0.36f), Ink, 145);
            Shape(bag, "书包口内侧", circle, new Vector2(0, 1.15f), new Vector2(2.22f, 0.23f), DeepPink, 146);
            Transform inside = Group("书包内部 · 完整放入后松手", bag, new Vector2(0, 0.17f));
            var bagInterior = inside.gameObject.AddComponent<BoxCollider2D>();
            bagInterior.isTrigger = true;
            bagInterior.size = new Vector2(2.12f, 1.76f);
            Transform packedRoot = Group("已放入的物品", bag, Vector2.zero);
            WorldText(bag, "把物品放在这里\n松手收好", new Vector2(0, 0.17f), 22, 180);
            WorldText(bag, "粉色书包", new Vector2(0, -1.86f), 24, 180);

            var objects = new List<ChildhoodPackingLevel.PackingItem>();
            var paths = new List<Vector2[]>();
            Transform doll = Group("粉色布娃娃", null, new Vector2(-3.75f, -0.40f));
            Solid(doll, "玩偶左手", circle, new Vector2(-0.34f, -0.08f), new Vector2(0.22f, 0.51f), LightPink, 211, paths, doll, -25);
            Solid(doll, "玩偶右手", circle, new Vector2(0.34f, -0.08f), new Vector2(0.22f, 0.51f), LightPink, 211, paths, doll, 25);
            Solid(doll, "玩偶左腿", circle, new Vector2(-0.17f, -0.47f), new Vector2(0.23f, 0.43f), Pink, 211, paths, doll);
            Solid(doll, "玩偶右腿", circle, new Vector2(0.17f, -0.47f), new Vector2(0.23f, 0.43f), Pink, 211, paths, doll);
            Solid(doll, "玩偶裙子", square, new Vector2(0, -0.12f), new Vector2(0.56f, 0.58f), DeepPink, 212, paths, doll);
            Solid(doll, "玩偶脸", circle, new Vector2(0, 0.38f), new Vector2(0.65f, 0.65f), LightPink, 213, paths, doll);
            Shape(doll, "玩偶左眼", circle, new Vector2(-0.12f, 0.42f), Vector2.one * 0.05f, Ink, 214);
            Shape(doll, "玩偶右眼", circle, new Vector2(0.12f, 0.42f), Vector2.one * 0.05f, Ink, 214);
            Shape(doll, "玩偶微笑", circle, new Vector2(0, 0.25f), new Vector2(0.08f, 0.035f), DeepPink, 214);
            objects.Add(Record(doll, "粉色布娃娃", paths));
            WorldText(doll, "布娃娃", new Vector2(0, -0.97f), 23, 230);

            paths = new List<Vector2[]>();
            Transform pencil = Group("粉色铅笔", null, new Vector2(-2.85f, -2.30f));
            Transform pencilVisual = Group("倾斜铅笔", pencil, Vector2.zero);
            pencilVisual.localRotation = Quaternion.Euler(0, 0, -55);
            Solid(pencilVisual, "铅笔杆", square, new Vector2(0, -0.04f), new Vector2(0.19f, 1.08f), Pink, 211, paths, pencil);
            Solid(pencilVisual, "铅笔橡皮", square, new Vector2(0, -0.63f), new Vector2(0.21f, 0.18f), DeepPink, 212, paths, pencil);
            // A small diamond supplies a geometric pencil tip without a new art asset.
            Solid(pencilVisual, "粉色笔尖", square, new Vector2(0, 0.58f), new Vector2(0.18f, 0.18f), DeepPink, 212, paths, pencil, 45);
            objects.Add(Record(pencil, "粉色铅笔", paths));
            WorldText(pencil, "铅笔", new Vector2(0, -0.61f), 23, 230);

            paths = new List<Vector2[]>();
            Transform eraser = Group("粉色橡皮", null, new Vector2(-0.85f, -2.72f));
            Solid(eraser, "橡皮本体", square, Vector2.zero, new Vector2(0.75f, 0.43f), LightPink, 211, paths, eraser, -10);
            Shape(eraser, "橡皮纸套", square, Vector2.zero, new Vector2(0.29f, 0.43f), DeepPink, 212).transform.localRotation = Quaternion.Euler(0, 0, -10);
            objects.Add(Record(eraser, "粉色橡皮", paths));
            WorldText(eraser, "橡皮", new Vector2(0, -0.52f), 23, 230);

            paths = new List<Vector2[]>();
            Transform notebook = Group("粉色笔记本", null, new Vector2(1.13f, -2.14f));
            Solid(notebook, "笔记本封面", square, Vector2.zero, new Vector2(0.83f, 1.08f), DeepPink, 211, paths, notebook);
            Shape(notebook, "笔记本内封", square, new Vector2(0.025f, 0), new Vector2(0.68f, 0.96f), LightPink, 212);
            Shape(notebook, "笔记本书脊", square, new Vector2(-0.31f, 0), new Vector2(0.07f, 1.08f), Pink, 213);
            Shape(notebook, "封面横线", square, new Vector2(0.04f, 0.12f), new Vector2(0.40f, 0.025f), DeepPink, 213);
            Shape(notebook, "封面下横线", square, new Vector2(0.04f, -0.05f), new Vector2(0.40f, 0.025f), DeepPink, 213);
            objects.Add(Record(notebook, "粉色笔记本", paths));
            WorldText(notebook, "笔记本", new Vector2(0, -0.82f), 23, 230);

            paths = new List<Vector2[]>();
            Transform book = Group("粉色故事书", null, new Vector2(-3.65f, 1.66f));
            Solid(book, "故事书封面", square, Vector2.zero, new Vector2(0.98f, 1.24f), Pink, 211, paths, book);
            Shape(book, "故事书书脊", square, new Vector2(-0.40f, 0), new Vector2(0.13f, 1.24f), DeepPink, 212);
            Shape(book, "封面圆形图案", circle, new Vector2(0.06f, 0.11f), Vector2.one * 0.53f, LightPink, 212);
            Shape(book, "封面小圆点", circle, new Vector2(0.06f, 0.11f), Vector2.one * 0.22f, Pink, 213);
            Shape(book, "书页下缘", square, new Vector2(0.04f, -0.49f), new Vector2(0.74f, 0.06f), LightPink, 213);
            objects.Add(Record(book, "粉色故事书", paths));
            WorldText(book, "故事书", new Vector2(0, -0.90f), 23, 230);

            var canvasObject = new GameObject("童年 · 可编辑提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            ScreenText(canvasObject.transform, "关卡名称", "03 / 童年 · 女孩喜欢粉色", new Vector2(0, -22), 22, true);
            ScreenText(canvasObject.transform, "关卡提示", "整理书包：把粉色的东西全部装进去", new Vector2(0, -76), 33, true);
            Text progress = ScreenText(canvasObject.transform, "整理进度", "整理书包  0 / 5", new Vector2(0, -130), 23, true);
            Text feedback = ScreenText(canvasObject.transform, "操作提示", "拖动女孩的手拿起物品，放进书包里，再松手。", new Vector2(0, 22), 23, false);
            var controller = new GameObject("童年 · 整理书包交互与时间设置").AddComponent<ChildhoodPackingLevel>();
            var data = new SerializedObject(controller);
            Ref(data, "interactionCamera", camera);
            Ref(data, "girl", girl);
            Ref(data, "leftHandAnchor", leftHand);
            Ref(data, "rightHandAnchor", rightHand);
            Ref(data, "bagInterior", bagInterior);
            Ref(data, "packedItemsRoot", packedRoot);
            Ref(data, "progressText", progress);
            Ref(data, "feedbackText", feedback);
            data.FindProperty("completionHoldSeconds").floatValue = 2f;
            data.FindProperty("nextScene").stringValue = "Level04_AdolescencePiercing";
            SerializedProperty list = data.FindProperty("items");
            list.arraySize = objects.Count;
            for (int i = 0; i < objects.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("itemName").stringValue = objects[i].itemName;
                entry.FindPropertyRelative("root").objectReferenceValue = objects[i].root;
                entry.FindPropertyRelative("collider").objectReferenceValue = objects[i].collider;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Scene birthday = EditorSceneManager.OpenScene("Assets/Scenes/Level02_FirstBirthday.unity");
            var birthdayData = new SerializedObject(Object.FindObjectOfType<BabyBirthdayLevel>());
            birthdayData.FindProperty("nextScene").stringValue = "Level03_ChildhoodPacking";
            birthdayData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(birthday);
            EditorSceneManager.SaveScene(birthday);
            if (!File.Exists(NextPath))
            {
                Scene next = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0, 0, -10);
                Camera nextCamera = cameraObject.GetComponent<Camera>();
                nextCamera.orthographic = true;
                nextCamera.orthographicSize = 4.4f;
                nextCamera.clearFlags = CameraClearFlags.SolidColor;
                nextCamera.backgroundColor = new Color(0.98f, 0.928f, 0.943f, 1);
                new GameObject("第四关 · 内容预留（空场景）");
                EditorSceneManager.SaveScene(next, NextPath);
            }
            var buildScenes = new List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (entry.path == NextPath)
                    continue;
                if (entry.path == OldPath || entry.path == LevelPath)
                {
                    buildScenes.Add(new EditorBuildSettingsScene(LevelPath, true));
                    buildScenes.Add(new EditorBuildSettingsScene(NextPath, true));
                }
                else
                    buildScenes.Add(entry);
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Saved independent childhood packing level with five pink items, contact-only hand pickup, full-contour bag placement, and the route from birthday to childhood to the fourth placeholder. No Play mode or tests executed.");
        }

        private static ChildhoodPackingLevel.PackingItem Record(Transform root, string name, List<Vector2[]> paths)
        {
            var collider = root.gameObject.AddComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            collider.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
                collider.SetPath(i, paths[i]);
            return new ChildhoodPackingLevel.PackingItem { itemName = name, root = root, collider = collider };
        }

        private static void Solid(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size,
            Color color, int order, List<Vector2[]> paths, Transform root, float angle = 0)
        {
            SpriteRenderer renderer = Shape(parent, name, sprite, position, size, color, order);
            renderer.transform.localRotation = Quaternion.Euler(0, 0, angle);
            Vector2[] points;
            if (sprite == circle)
            {
                points = new Vector2[32];
                for (int i = 0; i < points.Length; i++)
                {
                    float a = i * 2f * Mathf.PI / points.Length;
                    points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f;
                }
            }
            else
                points = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
            for (int i = 0; i < points.Length; i++)
                points[i] = root.InverseTransformPoint(renderer.transform.TransformPoint(points[i]));
            paths.Add(points);
        }

        private static Transform Group(string name, Transform parent, Vector2 position)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }

        private static SpriteRenderer Shape(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            Transform child = Group(name, parent, position);
            child.localScale = new Vector3(size.x, size.y, 1);
            var renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite LoadSprite(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite)
                    return sprite;
            throw new System.InvalidOperationException("Missing sprite: " + path);
        }

        private static Text ScreenText(Transform parent, string name, string text, Vector2 position, int size, bool top)
        {
            Text label = TextObject(parent, name, text, size);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, top ? 1 : 0);
            label.rectTransform.pivot = new Vector2(0.5f, top ? 1 : 0);
            label.rectTransform.sizeDelta = new Vector2(1450, 56);
            label.rectTransform.anchoredPosition = position;
            return label;
        }

        private static void WorldText(Transform parent, string text, Vector2 position, int size, int order)
        {
            var go = new GameObject(text + " · 标签", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = order;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.01f;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 88);
            Text label = TextObject(go.transform, "文字", text, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }

        private static Text TextObject(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        private static void Ref(SerializedObject data, string name, Object value) =>
            data.FindProperty(name).objectReferenceValue = value;
    }
}
