using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    /// <summary>Authors the ultrasound discovery Start scene, without Play mode.</summary>
    public static class UltrasoundStartAuthoring
    {
        private const int ScanLayer = 2; // Built-in Ignore Raycast; scan visuals only.
        private const string Geometry = "Assets/PinkDream/Geometry/";
        private static Sprite circle, square;
        private static Material material;
        private static Font font;
        private static readonly Color Ink = new Color32(91, 54, 73, 255);
        private static readonly Color Rose = new Color32(229, 157, 185, 255);

        public static void Apply()
        {
            circle = LoadSprite(Geometry + "Circle.asset");
            square = LoadSprite(Geometry + "Square.asset");
            material = AssetDatabase.LoadAssetAtPath<Material>(Geometry + "RagdollGeometry.mat");
            font = AssetDatabase.LoadAssetAtPath<Font>(Geometry + "SceneUIFont.ttf");

            // Reuse the actual parents and their authored geometry, not doctors
            // or runtime-generated characters. The first level is never saved.
            Scene sourceScene = EditorSceneManager.OpenScene("Assets/Scenes/Level01_BeforeBirth.unity");
            var sourceData = new SerializedObject(Object.FindObjectOfType<BeforeBirthLevel>());
            var motherSource = (FrozenRagdoll)sourceData.FindProperty("mother").objectReferenceValue;
            var fatherSource = (FrozenRagdoll)sourceData.FindProperty("father").objectReferenceValue;
            var cameraSource = (Camera)sourceData.FindProperty("interactionCamera").objectReferenceValue;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Camera camera = Object.Instantiate(cameraSource.gameObject).GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.name = "Main Camera";
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographicSize = 4.8f;
            FrozenRagdoll mother = CopyParent(motherSource, scene, camera, "母亲（怀孕）· 可拖拽布娃娃", -4.4f);
            FrozenRagdoll father = CopyParent(fatherSource, scene, camera, "父亲 · 可拖拽布娃娃", 4.4f);
            EditorSceneManager.CloseScene(sourceScene, true);

            Transform monitor = Group("B 超屏幕 · 可编辑画面", null, new Vector3(0, 0.15f, 0));
            Shape(monitor, "显示器轮廓", square, Vector2.zero, new Vector2(7.0f, 4.35f), Ink, 140);
            Shape(monitor, "显示器外壳", square, Vector2.zero, new Vector2(6.93f, 4.28f), new Color32(228, 205, 213, 255), 141);
            Shape(monitor, "B 超黑色底板", square, Vector2.zero, new Vector2(6.62f, 3.92f), Color.black, 150, ScanLayer);
            Shape(monitor, "B 超扇形颗粒影像", MakeUltrasoundSprite(), Vector2.zero, new Vector2(6.6f, 3.9f), Color.white, 151, ScanLayer);
            var scanBounds = monitor.gameObject.AddComponent<BoxCollider2D>();
            scanBounds.size = new Vector2(6.6f, 3.9f);
            scanBounds.isTrigger = true;
            Shape(monitor, "显示器支架", square, new Vector2(0, -2.65f), new Vector2(0.28f, 1.25f), Ink, 130);
            Shape(monitor, "显示器底座", circle, new Vector2(0, -3.25f), new Vector2(2.6f, 0.24f), Ink, 130);

            Transform baby = Group("胎儿 · 放大镜发现目标", monitor, new Vector3(0.55f, -0.12f, 0));
            baby.localRotation = Quaternion.Euler(0, 0, -23);
            Shape(baby, "蜷曲身体外缘", circle, new Vector2(0.12f, -0.12f), new Vector2(1.03f, 0.76f), Gray(0.58f), 170, ScanLayer);
            Shape(baby, "蜷曲身体", circle, new Vector2(0.12f, -0.10f), new Vector2(0.88f, 0.62f), Gray(0.31f), 171, ScanLayer);
            Shape(baby, "腹部内侧弯曲", circle, new Vector2(-0.05f, 0.01f), new Vector2(0.47f, 0.33f), Gray(0.10f), 172, ScanLayer);
            Shape(baby, "头部轮廓", circle, new Vector2(-0.28f, 0.34f), new Vector2(0.65f, 0.70f), Gray(0.72f), 175, ScanLayer);
            Shape(baby, "头部", circle, new Vector2(-0.28f, 0.34f), new Vector2(0.55f, 0.60f), Gray(0.43f), 176, ScanLayer);
            Shape(baby, "面部侧影", circle, new Vector2(-0.02f, 0.32f), new Vector2(0.18f, 0.23f), Gray(0.51f), 177, ScanLayer);
            Shape(baby, "小手臂", circle, new Vector2(-0.04f, 0.06f), new Vector2(0.41f, 0.14f), Gray(0.65f), 178, ScanLayer);
            Shape(baby, "小腿", circle, new Vector2(0.34f, -0.34f), new Vector2(0.39f, 0.16f), Gray(0.63f), 178, ScanLayer);
            Shape(baby, "小脚", circle, new Vector2(0.12f, -0.35f), new Vector2(0.20f, 0.13f), Gray(0.74f), 179, ScanLayer);
            Transform focus = Group("发现 START 的对准中心", baby, Vector3.zero);

            RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(Geometry + "StartMagnifier.renderTexture");
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Start magnifier scan", filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
                };
                AssetDatabase.CreateAsset(renderTexture, Geometry + "StartMagnifier.renderTexture");
            }
            var captureObject = new GameObject("放大镜摄像机 · 仅拍摄 B 超层", typeof(Camera));
            Camera capture = captureObject.GetComponent<Camera>();
            capture.enabled = false;
            capture.orthographic = true;
            capture.orthographicSize = 0.88f / 2.1f;
            capture.clearFlags = CameraClearFlags.SolidColor;
            capture.backgroundColor = Color.black;
            capture.cullingMask = 1 << ScanLayer;
            capture.targetTexture = renderTexture;
            capture.nearClipPlane = 0.1f;
            capture.farClipPlane = 30;

            Material lensMaterial = AssetDatabase.LoadAssetAtPath<Material>(Geometry + "StartMagnifier.mat");
            if (lensMaterial == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(Geometry + "UltrasoundLens.shader");
                lensMaterial = new Material(shader) { name = "Start magnifier lens" };
                AssetDatabase.CreateAsset(lensMaterial, Geometry + "StartMagnifier.mat");
            }
            lensMaterial.SetTexture("_LensTex", renderTexture);
            EditorUtility.SetDirty(lensMaterial);

            Transform lens = Group("放大镜 · 主要拖拽物品", null, new Vector3(2.6f, -2.45f, 0));
            Transform handle = Group("放大镜手柄", lens, new Vector3(-0.67f, -0.90f, 0));
            handle.localRotation = Quaternion.Euler(0, 0, -40);
            Shape(handle, "手柄轮廓", square, Vector2.zero, new Vector2(0.27f, 1.12f), Ink, 1098);
            Shape(handle, "手柄", square, Vector2.zero, new Vector2(0.19f, 1.03f), Rose, 1099);
            Shape(lens, "镜框轮廓", circle, Vector2.zero, Vector2.one * 1.98f, Ink, 1100);
            Shape(lens, "镜框", circle, Vector2.zero, Vector2.one * 1.88f, Rose, 1101);
            SpriteRenderer glass = Shape(lens, "镜片 · 真实局部放大", square, Vector2.zero, Vector2.one * 1.76f, Color.white, 1102);
            glass.sharedMaterial = lensMaterial;
            var lensCollider = lens.gameObject.AddComponent<CircleCollider2D>();
            lensCollider.radius = 0.99f;
            lensCollider.isTrigger = true;
            var handleCollider = handle.gameObject.AddComponent<CapsuleCollider2D>();
            handleCollider.size = new Vector2(0.34f, 1.18f);
            handleCollider.isTrigger = true;
            Transform grip = Group("父母握持的位置", lens, new Vector3(-0.67f, -0.90f, 0));
            capture.transform.position = new Vector3(lens.position.x, lens.position.y, -10);

            CanvasGroup found = FoundStartLabel(lens, camera);
            Canvas canvas = ScreenCanvas();
            ScreenText(canvas.transform, "游戏名称", "Pink Dream", new Vector2(0, -26), new Vector2(1400, 65), 44, true);
            ScreenText(canvas.transform, "开始提示", "拖动放大镜，找到小小的她。", new Vector2(0, -99), new Vector2(1400, 52), 27, true);
            Text hint = ScreenText(canvas.transform, "操作说明", "把旁边的放大镜拖到 B 超屏幕上，父母也可以拖动。", new Vector2(0, 28), new Vector2(1480, 50), 22, false);

            var controller = new GameObject("Start · 发现与停留时间设置").AddComponent<StartSceneController>();
            var data = new SerializedObject(controller);
            Ref(data, "mother", mother);
            Ref(data, "father", father);
            Ref(data, "magnifier", lens);
            Ref(data, "magnifierGrip", grip);
            SerializedProperty pickers = data.FindProperty("magnifierColliders");
            pickers.arraySize = 2;
            pickers.GetArrayElementAtIndex(0).objectReferenceValue = lensCollider;
            pickers.GetArrayElementAtIndex(1).objectReferenceValue = handleCollider;
            Ref(data, "initialHolder", null);
            data.FindProperty("initialHolderHand").intValue = 5;
            Ref(data, "magnifierCamera", capture);
            Ref(data, "babyFocus", focus);
            Ref(data, "ultrasoundBounds", scanBounds);
            Ref(data, "discoveredStart", found);
            Ref(data, "instructionText", hint);
            data.FindProperty("revealHoldSeconds").floatValue = 1;
            data.FindProperty("firstLevelScene").stringValue = "Level01_BeforeBirth";
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Start.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved Start: father, mother, authored ultrasound, draggable magnifier with real local zoom, and stable START reveal for at least 1 second. No Play mode or tests executed.");
        }

        public static void ApplyLensAndHeadCorrections()
        {
            foreach (string path in new[] { "Assets/Scenes/Start.unity", "Assets/Scenes/Level01_BeforeBirth.unity", "Assets/Scenes/SampleScene.unity" })
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                if (path == "Assets/Scenes/Start.unity")
                {
                    var controller = Object.FindObjectOfType<StartSceneController>();
                    var data = new SerializedObject(controller);
                    var lens = (Transform)data.FindProperty("magnifier").objectReferenceValue;
                    var grip = (Transform)data.FindProperty("magnifierGrip").objectReferenceValue;
                    Transform handle = lens.Find("放大镜手柄");
                    Vector3 handlePosition = handle.localPosition;
                    handlePosition.x = -Mathf.Abs(handlePosition.x);
                    handle.localPosition = handlePosition;
                    float angle = Mathf.Abs(Mathf.DeltaAngle(0, handle.localEulerAngles.z));
                    handle.localRotation = Quaternion.Euler(0, 0, -angle);
                    Vector3 gripPosition = grip.localPosition;
                    gripPosition.x = -Mathf.Abs(gripPosition.x);
                    grip.localPosition = gripPosition;
                    EditorUtility.SetDirty(handle);
                    EditorUtility.SetDirty(grip);
                    var holder = (FrozenRagdoll)data.FindProperty("initialHolder").objectReferenceValue;
                    if (holder != null)
                        PoseHandInEditor(holder, data.FindProperty("initialHolderHand").intValue, grip.position);
                }
                foreach (FrozenRagdoll character in Object.FindObjectsOfType<FrozenRagdoll>())
                {
                    var data = new SerializedObject(character);
                    SerializedProperty views = data.FindProperty("partViews");
                    var torso = (Transform)views.GetArrayElementAtIndex(0).objectReferenceValue;
                    var head = (Transform)views.GetArrayElementAtIndex(1).objectReferenceValue;
                    var rest = new PoseRagdollModel(character.transform.position);
                    PoseRagdollModel.Joint neck = rest.Joints[0];
                    Vector2 offset = neck.AnchorA - neck.AnchorB;
                    head.SetPositionAndRotation(torso.position + torso.rotation * new Vector3(offset.x, offset.y, 0), torso.rotation);
                    EditorUtility.SetDirty(head);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved inward-facing magnifier handle/grip and aligned head-torso poses. No Play mode or tests executed.");
        }

        private static FrozenRagdoll CopyParent(FrozenRagdoll source, Scene scene, Camera camera, string name, float x)
        {
            FrozenRagdoll copy = Object.Instantiate(source.gameObject).GetComponent<FrozenRagdoll>();
            SceneManager.MoveGameObjectToScene(copy.gameObject, scene);
            copy.name = name;
            copy.transform.position = new Vector3(x, -0.2f, 0);
            var data = new SerializedObject(copy);
            Ref(data, "interactionCamera", camera);
            data.FindProperty("showPrototypeOverlay").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (SpriteRenderer renderer in copy.GetComponentsInChildren<SpriteRenderer>())
                renderer.sortingOrder += 400;
            foreach (Canvas label in copy.GetComponentsInChildren<Canvas>())
                label.sortingOrder = 700;
            return copy;
        }

        private static void PoseHandInEditor(FrozenRagdoll character, int hand, Vector2 destination)
        {
            character.PoseHandInEditor(hand, destination);
        }

        private static Sprite MakeUltrasoundSprite()
        {
            const string path = Geometry + "StartUltrasound.asset";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite existing)
                    return existing;
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Ultrasound speckle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            var random = new System.Random(157);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1), v = y / (float)(size - 1);
                float dx = (u - 0.5f) * 1.35f, dy = 0.93f - v;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                bool fan = dy > 0 && dy < 0.87f && Mathf.Abs(dx) < dy * 0.85f && radius < 0.94f;
                float grain = (float)random.NextDouble();
                float value = fan ? 0.055f + grain * grain * 0.30f + Mathf.Sin(radius * 110) * 0.012f : 0.025f;
                pixels[y * size + x] = new Color(value, value, value + 0.008f, 1);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
            sprite.name = "Ultrasound fan";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(path);
            return sprite;
        }

        private static CanvasGroup FoundStartLabel(Transform lens, Camera camera)
        {
            var go = new GameObject("发现的 START · 对准后显示", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 1200;
            go.transform.SetParent(lens, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.01f;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 56);
            var background = new GameObject("START 底衬", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(go.transform, false);
            RectTransform backRect = background.GetComponent<RectTransform>();
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.offsetMin = backRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.12f, 0.07f, 0.11f, 0.8f);
            background.GetComponent<Image>().raycastTarget = false;
            var word = new GameObject("START", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            word.transform.SetParent(go.transform, false);
            RectTransform wordRect = word.GetComponent<RectTransform>();
            wordRect.anchorMin = Vector2.zero;
            wordRect.anchorMax = Vector2.one;
            wordRect.offsetMin = wordRect.offsetMax = Vector2.zero;
            Text text = word.GetComponent<Text>();
            text.font = font;
            text.text = "START";
            text.fontSize = 40;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(255, 216, 234, 255);
            text.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
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

        private static SpriteRenderer Shape(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order, int layer = 0)
        {
            Transform child = Group(name, parent, position);
            child.gameObject.layer = layer;
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
            var go = new GameObject("Start 界面 · 可编辑提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Text ScreenText(Transform parent, string name, string line, Vector2 position, Vector2 size, int fontSize, bool top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = line;
            text.fontSize = fontSize;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, top ? 1 : 0);
            rect.pivot = new Vector2(0.5f, top ? 1 : 0);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return text;
        }

        private static Color Gray(float value) => new Color(value, value, value, 1);
        private static void Ref(SerializedObject data, string name, Object value)
            => data.FindProperty(name).objectReferenceValue = value;
    }
}
