using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    public static class BabyBirthdaySceneAuthoring
    {
        private const string OldPath = "Assets/Scenes/Level02_Placeholder.unity";
        private const string LevelPath = "Assets/Scenes/Level02_FirstBirthday.unity";
        private const string NextPath = "Assets/Scenes/Level03_Placeholder.unity";
        private static Sprite circle, square;
        private static Material material;
        private static Font font;
        private static readonly Color Ink = new Color32(91, 54, 73, 255);
        private static readonly Color Cream = new Color32(255, 244, 230, 255);
        private static readonly Color Skin = new Color32(255, 219, 196, 255);

        public static void Apply()
        {
            if (File.Exists(LevelPath))
                throw new System.InvalidOperationException("The birthday level already exists; preserve its scene edits.");
            circle = LoadSprite("Assets/PinkDream/Geometry/Circle.asset");
            square = LoadSprite("Assets/PinkDream/Geometry/Square.asset");
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PinkDream/Geometry/RagdollGeometry.mat");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/PinkDream/Geometry/SceneUIFont.ttf");
            if (File.Exists(OldPath))
            {
                string error = AssetDatabase.MoveAsset(OldPath, LevelPath);
                if (!string.IsNullOrEmpty(error))
                    throw new System.InvalidOperationException(error);
            }

            Scene source = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Camera sourceCamera = Camera.main;
            Color background = sourceCamera.backgroundColor;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Camera camera = Object.Instantiate(sourceCamera.gameObject).GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.name = "Main Camera";
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographicSize = 4.4f;
            EditorSceneManager.CloseScene(source, true);

            Transform mat = Group("周岁抓周垫", null, new Vector3(0, -1.2f, 0));
            Shape(mat, "垫子外缘", circle, Vector2.zero, new Vector2(9.8f, 4.6f), new Color32(221, 181, 193, 255), -100);
            Shape(mat, "柔软抓周垫", circle, Vector2.zero, new Vector2(9.66f, 4.46f), Cream, -99);

            var babyObject = new GameObject("女孩 · 周岁婴儿布娃娃");
            babyObject.transform.position = new Vector3(0, -0.25f, 0);
            FrozenRagdoll baby = babyObject.AddComponent<FrozenRagdoll>();
            var babyData = new SerializedObject(baby);
            Ref(babyData, "interactionCamera", camera);
            Ref(babyData, "geometryShader", material.shader);
            babyData.FindProperty("showPrototypeOverlay").boolValue = false;
            babyData.FindProperty("bodyScale").floatValue = 0.58f;
            babyData.FindProperty("headScale").floatValue = 1.95f;
            babyData.FindProperty("seatedRestPose").boolValue = true;
            babyData.ApplyModifiedPropertiesWithoutUndo();
            baby.BakeSceneGeometry();
            babyData = new SerializedObject(baby);
            SerializedProperty colors = babyData.FindProperty("bodyColors");
            for (int i = 0; i < colors.arraySize; i++)
            {
                var body = (SpriteRenderer)colors.GetArrayElementAtIndex(i).objectReferenceValue;
                Color color = i == 0 ? new Color32(228, 175, 196, 255) : Skin;
                foreach (SpriteRenderer renderer in body.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = color;
            }
            foreach (SpriteRenderer renderer in baby.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.name == "Foot")
                    renderer.color = Skin;
                if (renderer.name == "Hand outline")
                    renderer.sortingOrder = 300;
                if (renderer.name == "Hand")
                    renderer.sortingOrder = 301;
            }
            Transform head = baby.transform.Find("Geometric ragdoll/Head");
            Shape(head, "左脸颊", circle, new Vector2(-0.28f, -0.10f), new Vector2(0.15f, 0.09f), new Color32(245, 175, 184, 255), 114);
            Shape(head, "右脸颊", circle, new Vector2(0.28f, -0.10f), new Vector2(0.15f, 0.09f), new Color32(245, 175, 184, 255), 114);

            Transform leftArm = baby.transform.Find("Geometric ragdoll/Left forearm");
            Transform rightArm = baby.transform.Find("Geometric ragdoll/Right forearm");
            float armLength = baby.Model.Parts[3].Length;
            Transform leftHand = Group("左手抓取锚点", leftArm, new Vector3(0, -armLength * 0.5f, 0));
            Transform rightHand = Group("右手抓取锚点", rightArm, new Vector3(0, -baby.Model.Parts[5].Length * 0.5f, 0));

            var objects = new List<BabyBirthdayLevel.GraspItem>();
            Transform scissors = ItemRoot("剪刀", new Vector2(-3.5f, -0.50f));
            Ring(scissors, new Vector2(-0.24f, -0.23f), 0.44f, new Color32(151, 180, 198, 255));
            Ring(scissors, new Vector2(0.24f, -0.23f), 0.44f, new Color32(151, 180, 198, 255));
            Bar(scissors, "左剪刀刃轮廓", new Vector2(-0.10f, -0.02f), new Vector2(0.33f, 0.63f), 0.14f, Ink, 202);
            Bar(scissors, "右剪刀刃轮廓", new Vector2(0.10f, -0.02f), new Vector2(-0.33f, 0.63f), 0.14f, Ink, 202);
            Bar(scissors, "左剪刀刃", new Vector2(-0.10f, -0.02f), new Vector2(0.33f, 0.63f), 0.085f, new Color32(207, 217, 222, 255), 203);
            Bar(scissors, "右剪刀刃", new Vector2(0.10f, -0.02f), new Vector2(-0.33f, 0.63f), 0.085f, new Color32(207, 217, 222, 255), 203);
            Shape(scissors, "转轴", circle, new Vector2(0, 0.15f), Vector2.one * 0.14f, Ink, 204);
            objects.Add(Record(scissors, "剪刀", new Vector2(-0.24f, -0.23f)));
            WorldText(scissors, "剪刀", new Vector2(0, -0.86f), 24);

            Transform mouse = ItemRoot("鼠标", new Vector2(-2.15f, -1.95f));
            Shape(mouse, "鼠标轮廓", circle, Vector2.zero, new Vector2(0.78f, 1.02f), Ink, 201);
            Shape(mouse, "鼠标外壳", circle, Vector2.zero, new Vector2(0.72f, 0.96f), new Color32(169, 185, 196, 255), 202);
            Shape(mouse, "按键分缝", square, new Vector2(0, 0.23f), new Vector2(0.025f, 0.38f), Ink, 203);
            Shape(mouse, "滚轮", circle, new Vector2(0, 0.17f), new Vector2(0.095f, 0.19f), Cream, 204);
            Line(mouse, "鼠标线", new[] { new Vector3(0, 0.48f, 0), new Vector3(0, 0.67f, 0), new Vector3(0.16f, 0.82f, 0), new Vector3(0.31f, 0.77f, 0) }, 0.025f, Ink, 200);
            objects.Add(Record(mouse, "鼠标", Vector2.zero));
            WorldText(mouse, "鼠标", new Vector2(0, -0.85f), 24);

            Transform brush = ItemRoot("画笔", new Vector2(0, -2.48f));
            Transform brushVisual = Group("倾斜画笔", brush, Vector3.zero);
            brushVisual.localRotation = Quaternion.Euler(0, 0, -52);
            Shape(brushVisual, "笔杆轮廓", square, new Vector2(0, -0.12f), new Vector2(0.19f, 1.15f), Ink, 201);
            Shape(brushVisual, "木质笔杆", square, new Vector2(0, -0.12f), new Vector2(0.13f, 1.10f), new Color32(213, 166, 121, 255), 202);
            Shape(brushVisual, "金属箍", square, new Vector2(0, 0.44f), new Vector2(0.23f, 0.24f), new Color32(177, 192, 199, 255), 203);
            Shape(brushVisual, "笔毛", circle, new Vector2(0, 0.71f), new Vector2(0.29f, 0.44f), new Color32(118, 86, 73, 255), 203);
            Shape(brushVisual, "笔尖颜料", circle, new Vector2(0, 0.86f), new Vector2(0.26f, 0.20f), new Color32(209, 130, 167, 255), 204);
            var brushRecord = Record(brush, "画笔", Vector2.zero);
            brushRecord.gripPoint.position = brushVisual.TransformPoint(new Vector3(0, -0.18f, 0));
            objects.Add(brushRecord);
            WorldText(brush, "画笔", new Vector2(0, -0.87f), 24);

            Transform envelope = ItemRoot("红包", new Vector2(2.15f, -1.95f));
            Shape(envelope, "红包轮廓", square, Vector2.zero, new Vector2(0.90f, 1.30f), Ink, 201);
            Shape(envelope, "红包纸面", square, Vector2.zero, new Vector2(0.84f, 1.24f), new Color32(211, 86, 111, 255), 202);
            Bar(envelope, "封口左边", new Vector2(-0.41f, 0.48f), new Vector2(0, 0.27f), 0.025f, new Color32(255, 205, 143, 255), 203);
            Bar(envelope, "封口右边", new Vector2(0, 0.27f), new Vector2(0.41f, 0.48f), 0.025f, new Color32(255, 205, 143, 255), 203);
            SpriteRenderer emblem = Shape(envelope, "金色封印", square, new Vector2(0, -0.07f), Vector2.one * 0.36f, new Color32(249, 204, 136, 255), 203);
            emblem.transform.localRotation = Quaternion.Euler(0, 0, 45);
            objects.Add(Record(envelope, "红包", new Vector2(0, -0.2f)));
            WorldText(envelope, "红包", new Vector2(0, -0.96f), 24);

            Transform ukulele = ItemRoot("尤克里里", new Vector2(3.55f, -0.45f));
            Shape(ukulele, "琴身下部轮廓", circle, new Vector2(0, -0.22f), new Vector2(0.90f, 0.91f), Ink, 201);
            Shape(ukulele, "琴身上部轮廓", circle, new Vector2(0, 0.22f), new Vector2(0.69f, 0.70f), Ink, 201);
            Shape(ukulele, "琴身下部", circle, new Vector2(0, -0.22f), new Vector2(0.84f, 0.85f), new Color32(209, 156, 108, 255), 202);
            Shape(ukulele, "琴身上部", circle, new Vector2(0, 0.22f), new Vector2(0.63f, 0.64f), new Color32(209, 156, 108, 255), 202);
            Shape(ukulele, "琴颈", square, new Vector2(0, 0.75f), new Vector2(0.17f, 0.95f), new Color32(129, 89, 72, 255), 203);
            Shape(ukulele, "琴头", square, new Vector2(0, 1.23f), new Vector2(0.30f, 0.32f), new Color32(173, 117, 83, 255), 203);
            Shape(ukulele, "音孔", circle, new Vector2(0, 0.07f), Vector2.one * 0.27f, Ink, 204);
            Shape(ukulele, "琴桥", square, new Vector2(0, -0.35f), new Vector2(0.34f, 0.09f), Ink, 204);
            for (int i = 0; i < 4; i++)
                Shape(ukulele, "琴弦 " + (i + 1), square, new Vector2(-0.051f + i * 0.034f, 0.42f), new Vector2(0.009f, 1.48f), Cream, 205);
            objects.Add(Record(ukulele, "尤克里里", new Vector2(0, 0.67f)));
            WorldText(ukulele, "尤克里里", new Vector2(0, -1.03f), 24);

            Canvas ui = ScreenCanvas();
            ScreenText(ui.transform, "关卡名称", "02 / 婴儿周岁抓周", new Vector2(0, -22), 22, true);
            ScreenText(ui.transform, "关卡提示", "抓住你喜欢的东西吧。", new Vector2(0, -76), 36, true);
            Text feedback = ScreenText(ui.transform, "抓取反馈", "拖动宝宝的手，或把喜欢的小物件送到手边。", new Vector2(0, 24), 22, false);
            var controller = new GameObject("周岁抓周 · 任意物品即可通关").AddComponent<BabyBirthdayLevel>();
            var data = new SerializedObject(controller);
            Ref(data, "interactionCamera", camera);
            Ref(data, "baby", baby);
            Ref(data, "leftHandAnchor", leftHand);
            Ref(data, "rightHandAnchor", rightHand);
            Ref(data, "feedbackText", feedback);
            data.FindProperty("completionHoldSeconds").floatValue = 2f;
            SerializedProperty list = data.FindProperty("items");
            list.arraySize = objects.Count;
            for (int i = 0; i < objects.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("itemName").stringValue = objects[i].itemName;
                entry.FindPropertyRelative("root").objectReferenceValue = objects[i].root;
                entry.FindPropertyRelative("collider").objectReferenceValue = objects[i].collider;
                entry.FindPropertyRelative("gripPoint").objectReferenceValue = objects[i].gripPoint;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LevelPath);

            Scene transition = EditorSceneManager.OpenScene("Assets/Scenes/Transition_Blank.unity");
            var transitionData = new SerializedObject(Object.FindObjectOfType<BlankTransition>());
            transitionData.FindProperty("nextScene").stringValue = "Level02_FirstBirthday";
            transitionData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(transition);
            EditorSceneManager.SaveScene(transition);

            if (!File.Exists(NextPath))
            {
                Scene next = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var nextCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                nextCamera.tag = "MainCamera";
                nextCamera.transform.position = new Vector3(0, 0, -10);
                Camera blankCamera = nextCamera.GetComponent<Camera>();
                blankCamera.orthographic = true;
                blankCamera.orthographicSize = 4.4f;
                blankCamera.clearFlags = CameraClearFlags.SolidColor;
                blankCamera.backgroundColor = background;
                new GameObject("第三关 · 内容预留（空场景）");
                EditorSceneManager.SaveScene(next, NextPath);
            }
            var buildScenes = new List<EditorBuildSettingsScene>();
            bool addedLevel = false;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (entry.path == NextPath)
                    continue;
                if (entry.path == OldPath || entry.path == LevelPath)
                {
                    buildScenes.Add(new EditorBuildSettingsScene(LevelPath, true));
                    buildScenes.Add(new EditorBuildSettingsScene(NextPath, true));
                    addedLevel = true;
                }
                else
                    buildScenes.Add(entry);
            }
            if (!addedLevel)
            {
                buildScenes.Add(new EditorBuildSettingsScene(LevelPath, true));
                buildScenes.Add(new EditorBuildSettingsScene(NextPath, true));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Saved independent first-birthday level: one seated baby, five equally valid items, and a shared route to the next placeholder. No Play mode or tests executed.");
        }

        private static Transform ItemRoot(string name, Vector2 position) => Group("抓周物品 · " + name, null, position);
        private static BabyBirthdayLevel.GraspItem Record(Transform root, string name, Vector2 grip)
        {
            var collider = BabyBirthdayItemColliders.Fit(root, name);
            return new BabyBirthdayLevel.GraspItem { itemName = name, root = root, collider = collider, gripPoint = Group("抓握位置", root, grip) };
        }
        private static void Ring(Transform parent, Vector2 position, float size, Color color)
        {
            Shape(parent, "指环轮廓", circle, position, Vector2.one * size, Ink, 201);
            Shape(parent, "指环颜色", circle, position, Vector2.one * (size - 0.055f), color, 202);
            Shape(parent, "指环孔", circle, position, Vector2.one * (size - 0.21f), Cream, 203);
        }
        private static void Bar(Transform parent, string name, Vector2 start, Vector2 end, float width, Color color, int order)
        {
            Vector2 up = end - start;
            SpriteRenderer bar = Shape(parent, name, square, (start + end) * 0.5f, new Vector2(width, up.magnitude), color, order);
            bar.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90);
        }
        private static void Line(Transform parent, string name, Vector3[] points, float width, Color color, int order)
        {
            var line = Group(name, parent, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.positionCount = points.Length;
            line.SetPositions(points);
        }
        private static Sprite LoadSprite(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite)
                    return sprite;
            throw new System.InvalidOperationException("Missing sprite: " + path);
        }
        private static Transform Group(string name, Transform parent, Vector3 position)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
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
        private static Canvas ScreenCanvas()
        {
            var go = new GameObject("周岁抓周 · 可编辑提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }
        private static Text ScreenText(Transform parent, string name, string text, Vector2 position, int size, bool top)
        {
            Text label = TextObject(parent, name, text, size);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, top ? 1 : 0);
            label.rectTransform.pivot = new Vector2(0.5f, top ? 1 : 0);
            label.rectTransform.sizeDelta = new Vector2(1450, 64);
            label.rectTransform.anchoredPosition = position;
            return label;
        }
        private static void WorldText(Transform parent, string text, Vector2 position, int size)
        {
            var go = new GameObject(text + " · 物品标签", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 230;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.01f;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 48);
            Text label = TextObject(go.transform, "名称", text, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }
        private static Text TextObject(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = font;
            label.fontSize = size;
            label.color = Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }
        private static void Ref(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
    }
}
