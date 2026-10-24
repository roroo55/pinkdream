using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    public static class AdolescencePiercingSceneAuthoring
    {
        private const string OldPath = "Assets/Scenes/Level04_Placeholder.unity";
        private const string LevelPath = "Assets/Scenes/Level04_AdolescencePiercing.unity";
        private const string NextPath = "Assets/Scenes/Level05_ComingOfAge.unity";
        private static Sprite circle, square;
        private static Material material;
        private static Font font;
        private static readonly Color Black = new Color32(29, 29, 35, 255);
        private static readonly Color Charcoal = new Color32(57, 57, 65, 255);
        private static readonly Color Grey = new Color32(108, 108, 119, 255);
        private static readonly Color Silver = new Color32(200, 203, 211, 255);
        private static readonly Color Skin = new Color32(228, 218, 212, 255);

        public static void Apply()
        {
            if (File.Exists(LevelPath))
                throw new System.InvalidOperationException("The adolescence level already exists; preserve its scene edits.");
            circle = LoadSprite("Assets/PinkDream/Geometry/Circle.asset");
            square = LoadSprite("Assets/PinkDream/Geometry/Square.asset");
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PinkDream/Geometry/RagdollGeometry.mat");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/PinkDream/Geometry/SceneUIFont.ttf");
            string error = AssetDatabase.MoveAsset(OldPath, LevelPath);
            if (!string.IsNullOrEmpty(error))
                throw new System.InvalidOperationException(error);
            Scene scene = EditorSceneManager.OpenScene(LevelPath);
            Camera camera = Camera.main;
            Color background = camera.backgroundColor;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != camera.gameObject)
                    Object.DestroyImmediate(root);
            camera.orthographicSize = 4.4f;

            Transform carpet = Group("青少年 · 黑灰色散落物品区", null, new Vector2(0, -1.6f));
            Shape(carpet, "地毯外缘", circle, Vector2.zero, new Vector2(10.8f, 3.3f), Charcoal, -100);
            Shape(carpet, "灰色地毯", circle, Vector2.zero, new Vector2(10.65f, 3.15f), new Color32(171, 170, 178, 255), -99);

            var girlObject = new GameObject("女孩 · 青少年黑色布娃娃");
            girlObject.transform.position = new Vector3(0, 0.12f, 0);
            var girl = girlObject.AddComponent<FrozenRagdoll>();
            var girlData = new SerializedObject(girl);
            Ref(girlData, "interactionCamera", camera);
            Ref(girlData, "geometryShader", material.shader);
            girlData.FindProperty("showPrototypeOverlay").boolValue = false;
            girlData.FindProperty("bodyScale").floatValue = 0.94f;
            girlData.FindProperty("headScale").floatValue = 1.05f;
            girlData.FindProperty("seatedRestPose").boolValue = false;
            girlData.ApplyModifiedPropertiesWithoutUndo();
            girl.BakeSceneGeometry();
            foreach (SpriteRenderer renderer in girl.GetComponentsInChildren<SpriteRenderer>())
            {
                string name = renderer.name;
                if (name.Contains("outline") || name == "Outline" || name.Contains("eye") || name == "Mouth")
                    renderer.color = Black;
                else if (name == "Joint pin")
                    renderer.color = Silver;
                else if (name == "Head" || name == "Neck" || name == "Hand")
                    renderer.color = Skin;
                else
                    renderer.color = Charcoal;
                if (name == "Hand outline")
                    renderer.sortingOrder = 350;
                if (name == "Hand")
                    renderer.sortingOrder = 351;
                if (name == "Foot")
                    renderer.color = Black;
            }
            girlData = new SerializedObject(girl);
            var bodyColors = girlData.FindProperty("bodyColors");
            for (int i = 0; i < bodyColors.arraySize; i++)
            {
                var body = (SpriteRenderer)bodyColors.GetArrayElementAtIndex(i).objectReferenceValue;
                Color color = i == 1 || i == 3 || i == 5 ? Skin : i >= 6 ? Black : Charcoal;
                foreach (SpriteRenderer renderer in body.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = color;
            }
            Transform torso = girl.transform.Find("Geometric ragdoll/Torso");
            Shape(torso, "黑色颈圈", square, new Vector2(0, 0.75f), new Vector2(0.41f, 0.12f), Black, 120);
            Shape(torso, "颈圈银扣", square, new Vector2(0, 0.75f), new Vector2(0.12f, 0.085f), Silver, 121);
            Shape(torso, "黑色腰带", square, new Vector2(0, -0.46f), new Vector2(0.91f, 0.12f), Black, 120);
            for (int i = 0; i < 5; i++)
                Shape(torso, "腰带铆钉 " + (i + 1), square, new Vector2(-0.32f + i * 0.16f, -0.46f), Vector2.one * 0.048f, Silver, 121).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Chain(torso, "衣服银链", new Vector2(-0.27f, -0.12f), new Vector2(0.25f, -0.27f), 6, 0.11f, 122);
            Transform head = girl.transform.Find("Geometric ragdoll/Head");
            float headRadius = girl.Model.Parts[1].Width * 0.5f;
            Shape(head, "黑色短发", circle, new Vector2(0, 0.31f), new Vector2(1.07f, 0.54f), Black, 112);
            Shape(head, "不对称刘海", square, new Vector2(-0.23f, 0.25f), new Vector2(0.60f, 0.23f), Black, 112).transform.localRotation = Quaternion.Euler(0, 0, -24);
            var earTargets = new List<AdolescencePiercingLevel.EarTarget>();
            for (int side = -1; side <= 1; side += 2)
            {
                Transform ear = Group(side < 0 ? "左耳" : "右耳", head, new Vector2(side * (headRadius + 0.035f), -0.04f));
                Shape(ear, "耳廓轮廓", circle, Vector2.zero, new Vector2(0.22f, 0.33f), Black, 109);
                Shape(ear, "耳廓", circle, Vector2.zero, new Vector2(0.18f, 0.29f), Skin, 110);
                Transform lobe = Group("耳垂 · 实际接触区域", ear, new Vector2(side * 0.014f, -0.105f));
                Shape(lobe, "可见耳垂", circle, Vector2.zero, Vector2.one * 0.15f, Skin, 114);
                var lobeCollider = lobe.gameObject.AddComponent<CircleCollider2D>();
                lobeCollider.isTrigger = true;
                lobeCollider.radius = 0.075f;
                Transform earring = Group("完成后显示 · 银色耳饰", lobe, Vector2.zero);
                Shape(earring, "银色耳钉", circle, Vector2.zero, Vector2.one * 0.105f, Silver, 360);
                Ring(earring, "黑银耳环", new Vector2(0, -0.115f), 0.18f, 0.025f, Silver, 360);
                earring.gameObject.SetActive(false);
                earTargets.Add(new AdolescencePiercingLevel.EarTarget { lobe = lobeCollider, earring = earring.gameObject });
            }
            Transform leftArm = girl.transform.Find("Geometric ragdoll/Left forearm");
            Transform rightArm = girl.transform.Find("Geometric ragdoll/Right forearm");
            Transform leftHand = Group("左手抓取锚点", leftArm, new Vector2(0, -girl.Model.Parts[3].Length * 0.5f));
            Transform rightHand = Group("右手抓取锚点", rightArm, new Vector2(0, -girl.Model.Parts[5].Length * 0.5f));

            Transform tool = Group("黑银穿耳器 · 手部接触后拿起", null, new Vector2(-2.1f, -2.15f));
            var toolPaths = new List<Vector2[]>();
            Solid(tool, "黑色握柄", square, new Vector2(-0.20f, -0.10f), new Vector2(0.22f, 0.64f), Black, 211, toolPaths);
            Solid(tool, "灰色器身", square, new Vector2(0, 0.20f), new Vector2(0.65f, 0.20f), Charcoal, 212, toolPaths);
            Solid(tool, "银色前端", square, new Vector2(0.34f, 0.20f), new Vector2(0.18f, 0.09f), Silver, 213, toolPaths);
            Solid(tool, "接触端点", circle, new Vector2(0.45f, 0.20f), Vector2.one * 0.11f, Silver, 214, toolPaths);
            Shape(tool, "握柄银色细节", square, new Vector2(-0.20f, -0.14f), new Vector2(0.09f, 0.26f), Grey, 213);
            var toolCollider = tool.gameObject.AddComponent<PolygonCollider2D>();
            toolCollider.isTrigger = true;
            toolCollider.pathCount = toolPaths.Count;
            for (int i = 0; i < toolPaths.Count; i++)
                toolCollider.SetPath(i, toolPaths[i]);
            Transform tip = Group("前端实际接触点", tool, new Vector2(0.45f, 0.20f));
            var tipCollider = tip.gameObject.AddComponent<CircleCollider2D>();
            tipCollider.isTrigger = true;
            tipCollider.radius = 0.055f;
            WorldText(tool, "穿耳器", new Vector2(0, -0.74f), 24);

            Transform cuff = Group("黑色铆钉手环", null, new Vector2(-3.85f, -1.05f));
            Shape(cuff, "黑色皮革", square, Vector2.zero, new Vector2(0.94f, 0.48f), Black, 201);
            for (int i = 0; i < 4; i++)
                Shape(cuff, "银色铆钉 " + (i + 1), square, new Vector2(-0.32f + i * 0.21f, 0), Vector2.one * 0.12f, Silver, 202).transform.localRotation = Quaternion.Euler(0, 0, 45);
            WorldText(cuff, "铆钉手环", new Vector2(0, -0.62f), 22);

            Transform headphones = Group("黑灰耳机", null, new Vector2(-3.7f, 1.1f));
            Ring(headphones, "耳机头梁", new Vector2(0, 0.08f), 1.04f, 0.10f, Black, 201);
            Shape(headphones, "耳机左耳罩", square, new Vector2(-0.43f, -0.25f), new Vector2(0.28f, 0.54f), Charcoal, 202);
            Shape(headphones, "耳机右耳罩", square, new Vector2(0.43f, -0.25f), new Vector2(0.28f, 0.54f), Charcoal, 202);
            Shape(headphones, "左耳罩银片", square, new Vector2(-0.43f, -0.25f), new Vector2(0.11f, 0.37f), Silver, 203);
            Shape(headphones, "右耳罩银片", square, new Vector2(0.43f, -0.25f), new Vector2(0.11f, 0.37f), Silver, 203);
            WorldText(headphones, "耳机", new Vector2(0, -0.87f), 22);

            Transform book = Group("黑色涂鸦本", null, new Vector2(3.75f, 1.0f));
            Shape(book, "黑色封面", square, Vector2.zero, new Vector2(0.99f, 1.27f), Black, 201);
            Shape(book, "灰色书脊", square, new Vector2(-0.36f, 0), new Vector2(0.12f, 1.27f), Grey, 202);
            Shape(book, "银色封面标志", square, new Vector2(0.06f, 0.09f), new Vector2(0.36f, 0.36f), Silver, 203).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Shape(book, "黑色标志内侧", square, new Vector2(0.06f, 0.09f), Vector2.one * 0.19f, Black, 204).transform.localRotation = Quaternion.Euler(0, 0, 45);
            WorldText(book, "黑色涂鸦本", new Vector2(0, -0.92f), 22);

            Transform boots = Group("黑色厚底靴", null, new Vector2(3.55f, -1.54f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 offset = new Vector2(side * 0.33f, 0);
                Shape(boots, "靴筒 " + side, square, offset + new Vector2(-0.05f, 0.12f), new Vector2(0.38f, 0.82f), Black, 201);
                Shape(boots, "靴头 " + side, circle, offset + new Vector2(0.08f, -0.25f), new Vector2(0.65f, 0.36f), Charcoal, 202);
                Shape(boots, "厚底 " + side, square, offset + new Vector2(0.06f, -0.39f), new Vector2(0.65f, 0.13f), Black, 203);
                for (int i = 0; i < 3; i++)
                    Shape(boots, "银色鞋带 " + side + " · " + i, square, offset + new Vector2(-0.05f, 0.28f - i * 0.17f), new Vector2(0.24f, 0.028f), Silver, 204);
            }
            WorldText(boots, "厚底靴", new Vector2(0, -0.78f), 22);
            Transform chain = Group("散落的银色链条", null, new Vector2(1.55f, -2.8f));
            Chain(chain, "链节", new Vector2(-0.52f, 0), new Vector2(0.52f, 0.15f), 9, 0.18f, 201);
            WorldText(chain, "银链", new Vector2(0, -0.43f), 22);

            var canvasObject = new GameObject("青少年 · 可编辑提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            ScreenText(canvasObject.transform, "关卡名称", "04 / 青少年 · 黑色是我的选择", new Vector2(0, -22), 22, true);
            ScreenText(canvasObject.transform, "关卡提示", "拿起穿耳器，给自己打耳洞", new Vector2(0, -76), 34, true);
            ScreenText(canvasObject.transform, "人物态度", "喜欢黑色，讨厌粉色。不想被规定成什么样。", new Vector2(0, -130), 22, true);
            Text feedback = ScreenText(canvasObject.transform, "操作提示", "拖动女孩的手，碰到散放的穿耳器后拿起。", new Vector2(0, 22), 23, false);
            var controller = new GameObject("青少年 · 穿耳交互与完成时间设置").AddComponent<AdolescencePiercingLevel>();
            var data = new SerializedObject(controller);
            Ref(data, "interactionCamera", camera);
            Ref(data, "girl", girl);
            Ref(data, "leftHandAnchor", leftHand);
            Ref(data, "rightHandAnchor", rightHand);
            Ref(data, "piercingTool", tool);
            Ref(data, "toolCollider", toolCollider);
            Ref(data, "toolTip", tipCollider);
            Ref(data, "feedbackText", feedback);
            data.FindProperty("completionHoldSeconds").floatValue = 2f;
            data.FindProperty("nextScene").stringValue = "Level05_ComingOfAge";
            SerializedProperty targets = data.FindProperty("ears");
            targets.arraySize = earTargets.Count;
            for (int i = 0; i < earTargets.Count; i++)
            {
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("lobe").objectReferenceValue = earTargets[i].lobe;
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("earring").objectReferenceValue = earTargets[i].earring;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Scene childhood = EditorSceneManager.OpenScene("Assets/Scenes/Level03_ChildhoodPacking.unity");
            var childhoodData = new SerializedObject(Object.FindObjectOfType<ChildhoodPackingLevel>());
            childhoodData.FindProperty("nextScene").stringValue = "Level04_AdolescencePiercing";
            childhoodData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(childhood);
            EditorSceneManager.SaveScene(childhood);
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
                nextCamera.backgroundColor = background;
                new GameObject("第五关 · 内容预留（空场景）");
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
            Debug.Log("Saved independent adolescence level: black and grey punk character and props, contact-only tool pickup and ear contact on release, pre-authored earrings, and fifth-level placeholder. No Play mode or tests executed.");
        }

        private static void Solid(Transform root, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order, List<Vector2[]> paths)
        {
            SpriteRenderer renderer = Shape(root, name, sprite, position, size, color, order);
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

        private static void Chain(Transform parent, string name, Vector2 start, Vector2 end, int count, float size, int order)
        {
            for (int i = 0; i < count; i++)
                Ring(parent, name + " " + (i + 1), Vector2.Lerp(start, end, i / (float)(count - 1)), size, size * 0.16f, Silver, order);
        }

        private static void Ring(Transform parent, string name, Vector2 center, float diameter, float width, Color color, int order)
        {
            const int segments = 32;
            float radius = (diameter - width) * 0.5f;
            var line = Group(name, parent, center).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.sharedMaterial = material;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius);
            }
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

        private static void WorldText(Transform parent, string text, Vector2 position, int size)
        {
            var go = new GameObject(text + " · 标签", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 230;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.01f;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(230, 70);
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
            label.color = Black;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        private static void Ref(SerializedObject data, string name, Object value) =>
            data.FindProperty(name).objectReferenceValue = value;
    }
}
