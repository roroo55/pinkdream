using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    // Scene authoring only. This command saves actual editable objects; it does
    // not enter Play mode, run tests, or generate a level during gameplay.
    public static class BeforeBirthSceneAuthoring
    {
        private const string Prototype = "Assets/Scenes/SampleScene.unity";
        private const string StartPath = "Assets/Scenes/Start.unity";
        private const string LevelPath = "Assets/Scenes/Level01_BeforeBirth.unity";
        private static Sprite circle, square;
        private static Material material;
        private static Font font;
        private static readonly Color Ink = new Color32(91, 54, 73, 255);
        private static readonly Color Cream = new Color32(255, 248, 234, 255);
        private static readonly Color Pink = new Color32(244, 137, 179, 255);

        public static void CreateScenes()
        {
            if (File.Exists(StartPath) || File.Exists(LevelPath))
                throw new System.InvalidOperationException("A level scene already exists; preserve its edits rather than rebuild it.");
            circle = LoadSprite("Assets/PinkDream/Geometry/Circle.asset");
            square = LoadSprite("Assets/PinkDream/Geometry/Square.asset");
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PinkDream/Geometry/RagdollGeometry.mat");
            const string fontPath = "Assets/art/Date Night.ttf";
            font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
                throw new System.InvalidOperationException("Import the scene UI font before creating scenes: " + fontPath);
            CreateStart();
            CreateBeforeBirth();
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(StartPath, true),
                new EditorBuildSettingsScene(LevelPath, true)
            };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path != StartPath && scene.path != LevelPath)
                    scenes.Add(scene);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Pink Dream Start and Before Birth scenes authored and saved. No Play mode or tests executed.");
        }

        private static Sprite LoadSprite(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite)
                    return sprite;
            throw new System.InvalidOperationException("Missing geometric sprite: " + path);
        }

        private static void CreateStart()
        {
            var scene = EditorSceneManager.OpenScene(Prototype);
            FrozenRagdoll character = Object.FindObjectOfType<FrozenRagdoll>();
            character.name = "开始关卡 · 布娃娃";
            character.transform.position = new Vector3(-1.0f, 0, 0);
            SetOverlay(character, false);
            Camera.main.orthographicSize = 4.1f;
            Canvas canvas = ScreenCanvas("开始界面 · 可编辑文字");
            Label(canvas.transform, "拖拽提示", "拖动手到 START，开始游戏", new Vector2(0, -125), new Vector2(1200, 60), 28);

            Transform target = Group("START · 把手拖到这里", null, new Vector3(2.8f, -0.55f, 0));
            Shape(target, "外圈", circle, Vector2.zero, Vector2.one * 1.32f, Ink, 180);
            Shape(target, "开始区域", circle, Vector2.zero, Vector2.one * 1.25f, new Color32(246, 203, 219, 255), 181);
            var collider = target.gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.64f;
            collider.isTrigger = true;
            WorldLabel(target, "START", Vector2.zero, 30);
            var controller = new GameObject("开始关卡 · 场景跳转").AddComponent<StartSceneController>();
            var data = new SerializedObject(controller);
            data.FindProperty("character").objectReferenceValue = character;
            data.FindProperty("startTarget").objectReferenceValue = collider;
            data.FindProperty("firstLevelScene").stringValue = "Level01_BeforeBirth";
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, StartPath);
        }

        private static void CreateBeforeBirth()
        {
            var scene = EditorSceneManager.OpenScene(Prototype);
            FrozenRagdoll mother = Object.FindObjectOfType<FrozenRagdoll>();
            mother.name = "母亲（怀孕）· 布娃娃";
            mother.transform.position = new Vector3(-3.0f, 0, 0);
            SetOverlay(mother, false);
            FrozenRagdoll father = Object.Instantiate(mother.gameObject).GetComponent<FrozenRagdoll>();
            father.name = "父亲 · 布娃娃";
            father.transform.position = new Vector3(3.0f, 0, 0);
            SetFatherColors(father);

            Transform torso = mother.transform.Find("Geometric ragdoll/Torso");
            Shape(torso, "孕肚 · 轮廓", circle, new Vector2(0.25f, -0.17f), new Vector2(1.08f, 1.02f), Ink, 2);
            Shape(torso, "孕肚", circle, new Vector2(0.25f, -0.17f), new Vector2(1.02f, 0.96f), new Color32(215, 129, 161, 255), 3);
            var bellyCollider = torso.gameObject.AddComponent<CircleCollider2D>();
            bellyCollider.offset = new Vector2(0.25f, -0.17f);
            bellyCollider.radius = 0.53f;
            Camera camera = Camera.main;
            camera.orthographicSize = 4.4f;
            Canvas canvas = ScreenCanvas("出生前 · 顶部提示");
            Label(canvas.transform, "顶部文字提示", "手牵手，一起握刀切开蛋糕", new Vector2(0, -66), new Vector2(1450, 75), 36);

            Transform table = Group("桌子", null, Vector3.zero);
            Shape(table, "桌面", square, new Vector2(0, -1.43f), new Vector2(4.15f, 0.20f), new Color32(181, 145, 141, 255), 140);
            Shape(table, "左桌腿", square, new Vector2(-1.64f, -2.22f), new Vector2(0.16f, 1.42f), new Color32(181, 145, 141, 255), 140);
            Shape(table, "右桌腿", square, new Vector2(1.64f, -2.22f), new Vector2(0.16f, 1.42f), new Color32(181, 145, 141, 255), 140);

            Transform cake = Group("蛋糕 · 切开前为白色", null, new Vector3(0, -0.25f, 0));
            Shape(cake, "蛋糕盘", circle, new Vector2(0, -1.03f), new Vector2(2.55f, 0.20f), new Color32(222, 203, 209, 255), 175);
            Transform left = CakeHalf(cake, "左半蛋糕", -0.48f);
            Transform right = CakeHalf(cake, "右半蛋糕", 0.48f);
            Transform filling = Group("粉色内馅 · 切开后显示", cake, Vector3.zero);
            Shape(filling, "女孩 · 粉色蛋糕切面", square, new Vector2(0, -0.48f), new Vector2(0.40f, 0.88f), Pink, 183);
            Shape(filling, "粉色切面顶部", circle, new Vector2(0, -0.04f), new Vector2(0.40f, 0.21f), Pink, 184);
            for (int i = 0; i < 3; i++)
                Shape(filling, "奶油夹层 " + (i + 1), square, new Vector2(0, -0.23f - 0.26f * i), new Vector2(0.40f, 0.055f), new Color32(255, 220, 235, 255), 185);
            filling.gameObject.SetActive(false);
            var cutZone = cake.gameObject.AddComponent<BoxCollider2D>();
            cutZone.offset = new Vector2(0, -0.45f);
            cutZone.size = new Vector2(1.6f, 0.85f);
            cutZone.isTrigger = true;

            Transform knife = Group("刀 · 可拖拽", null, new Vector3(0.68f, 0.65f, 0));
            Shape(knife, "刀刃轮廓", square, new Vector2(-0.68f, 0), new Vector2(1.02f, 0.28f), Ink, 230);
            Shape(knife, "刀刃", square, new Vector2(-0.68f, 0.015f), new Vector2(0.97f, 0.22f), new Color32(224, 233, 236, 255), 231);
            Shape(knife, "刀刃高光", square, new Vector2(-0.68f, 0.08f), new Vector2(0.92f, 0.025f), Color.white, 232);
            Shape(knife, "刀柄", square, new Vector2(0.09f, 0), new Vector2(0.53f, 0.20f), Ink, 233);
            Shape(knife, "刀柄铆钉", circle, new Vector2(0.13f, 0), Vector2.one * 0.055f, Cream, 234);
            Transform guide = Group("握刀位置提示", knife, Vector3.zero);
            Shape(guide, "握刀区域外圈", circle, Vector2.zero, Vector2.one * 0.45f, Ink, 227);
            Shape(guide, "握刀区域内圈", circle, Vector2.zero, Vector2.one * 0.39f, Cream, 228);
            Transform blade = Group("切割检测点", knife, new Vector3(-0.72f, 0, 0));
            var knifeCollider = knife.gameObject.AddComponent<BoxCollider2D>();
            knifeCollider.offset = new Vector2(-0.37f, 0);
            knifeCollider.size = new Vector2(1.7f, 0.40f);
            knifeCollider.isTrigger = true;

            Transform celebration = Group("性别揭晓 · 粉色礼花与气球", null, Vector3.zero);
            ParticleSystem[] confetti =
            {
                Confetti(celebration, "左侧粉色礼花", new Vector3(-1.8f, -1.0f, 0), -23),
                Confetti(celebration, "右侧粉色礼花", new Vector3(1.8f, -1.0f, 0), 23)
            };
            var balloons = new Transform[6];
            for (int i = 0; i < balloons.Length; i++)
                balloons[i] = Balloon(celebration, i);

            var controller = new GameObject("出生前 · 关卡交互").AddComponent<BeforeBirthLevel>();
            var data = new SerializedObject(controller);
            Ref(data, "interactionCamera", camera);
            Ref(data, "mother", mother);
            Ref(data, "father", father);
            Ref(data, "knife", knife);
            Ref(data, "knifeCollider", knifeCollider);
            Ref(data, "bladeCutPoint", blade);
            Ref(data, "cakeCutZone", cutZone);
            Ref(data, "leftCakeHalf", left);
            Ref(data, "rightCakeHalf", right);
            Ref(data, "pinkFilling", filling.gameObject);
            Ref(data, "gripGuide", guide.gameObject);
            Array(data, "pinkConfetti", confetti);
            Array(data, "pinkBalloons", balloons);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, LevelPath);
        }

        private static Transform CakeHalf(Transform cake, string name, float x)
        {
            Transform half = Group(name, cake, new Vector3(x, 0, 0));
            Shape(half, "蛋糕轮廓", square, new Vector2(0, -0.48f), new Vector2(0.98f, 1.0f), Ink, 177);
            Shape(half, "白色奶油外层", square, new Vector2(0, -0.48f), new Vector2(0.94f, 0.96f), Cream, 178);
            Shape(half, "顶部奶油", circle, new Vector2(0, 0), new Vector2(0.97f, 0.25f), Cream, 180);
            for (int i = 0; i < 4; i++)
                Shape(half, "奶油装饰 " + i, circle, new Vector2(-0.34f + i * 0.22f, 0.04f), new Vector2(0.13f, 0.14f), Color.white, 181);
            return half;
        }

        private static ParticleSystem Confetti(Transform parent, string name, Vector3 position, float tilt)
        {
            Transform root = Group(name, parent, position);
            root.localRotation = Quaternion.Euler(-90, 0, tilt);
            var particles = root.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 5.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.065f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color32(244, 117, 169, 255), new Color32(255, 198, 222, 255));
            main.gravityModifier = 0.35f;
            main.maxParticles = 150;
            var emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)85) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16;
            shape.radius = 0.1f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 400;
            return particles;
        }

        private static Transform Balloon(Transform parent, int index)
        {
            bool left = index < 3;
            int offset = index % 3;
            Transform balloon = Group("粉色气球 " + (index + 1), parent,
                new Vector3((left ? -1 : 1) * (2.0f + 0.55f * offset), -1.9f - 0.24f * offset, 0));
            Color color = index % 2 == 0 ? Pink : new Color32(255, 187, 214, 255);
            Shape(balloon, "气球轮廓", circle, Vector2.zero, new Vector2(0.55f, 0.73f), Ink, 210);
            Shape(balloon, "粉色气球", circle, Vector2.zero, new Vector2(0.51f, 0.69f), color, 211);
            SpriteRenderer knot = Shape(balloon, "气球结", square, new Vector2(0, -0.36f), Vector2.one * 0.08f, color, 211);
            knot.transform.localRotation = Quaternion.Euler(0, 0, 45);
            Transform stringRoot = Group("气球细绳", balloon, Vector3.zero);
            var line = stringRoot.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = 0.014f;
            line.startColor = line.endColor = Ink;
            line.sortingOrder = 209;
            line.positionCount = 4;
            line.SetPositions(new[] { new Vector3(0, -0.38f, 0), new Vector3(0.05f, -0.69f, 0),
                new Vector3(-0.04f, -0.97f, 0), new Vector3(0, -1.22f, 0) });
            balloon.gameObject.SetActive(false);
            return balloon;
        }

        private static void SetFatherColors(FrozenRagdoll father)
        {
            var data = new SerializedObject(father);
            SerializedProperty colors = data.FindProperty("bodyColors");
            for (int i = 0; i < colors.arraySize; i++)
            {
                if (i == 1 || i == 3 || i == 5)
                    continue;
                var body = (SpriteRenderer)colors.GetArrayElementAtIndex(i).objectReferenceValue;
                Color color = i >= 6 ? new Color32(123, 139, 166, 255) : new Color32(129, 177, 169, 255);
                foreach (SpriteRenderer renderer in body.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = color;
            }
            foreach (SpriteRenderer renderer in father.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.name == "Foot")
                    renderer.color = new Color32(123, 139, 166, 255);
        }

        private static void SetOverlay(FrozenRagdoll character, bool value)
        {
            var data = new SerializedObject(character);
            data.FindProperty("showPrototypeOverlay").boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Group(string name, Transform parent, Vector3 position)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
        }

        private static SpriteRenderer Shape(Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, int order)
        {
            Transform child = Group(name, parent, center);
            child.localScale = new Vector3(size.x, size.y, 1);
            var renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Canvas ScreenCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var label = go.GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.text = EnglishTypographyAuthoring.Translate(text);
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Ink;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return label;
        }

        private static void WorldLabel(Transform parent, string text, Vector2 position, int fontSize)
        {
            var go = new GameObject(text + " · 标签", typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 310;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.01f;
            ((RectTransform)go.transform).sizeDelta = new Vector2(280, 60);
            Text label = Label(go.transform, "文字", text, Vector2.zero, new Vector2(280, 60), fontSize);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * 0.5f;
            label.rectTransform.pivot = Vector2.one * 0.5f;
        }

        private static void Ref(SerializedObject data, string name, Object value)
            => data.FindProperty(name).objectReferenceValue = value;

        private static void Array<T>(SerializedObject data, string name, T[] values) where T : Object
        {
            SerializedProperty property = data.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
