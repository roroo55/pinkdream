using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    public static class ComingOfAgeSceneAuthoring
    {
        private const string OldPath = "Assets/Scenes/Level05_Placeholder.unity";
        private const string LevelPath = "Assets/Scenes/Level05_ComingOfAge.unity";
        private const string StarPath = "Assets/PinkDream/Geometry/ComingOfAgeStar.asset";
        private const string StarTexturePath = "Assets/PinkDream/Geometry/ComingOfAgeStarTexture.asset";
        private static Sprite circle, square, star;
        private static Material material;
        private static Font font;
        private static readonly Color Ink = new Color32(80, 61, 78, 255);
        private static readonly Color Pink = new Color32(231, 151, 188, 255);
        private static readonly Color Gold = new Color32(250, 204, 87, 255);
        private static readonly Color Green = new Color32(116, 177, 126, 255);
        private static readonly Color Cream = new Color32(255, 246, 224, 255);
        private static readonly Color[] Palette =
        {
            new Color32(251, 201, 80, 255), new Color32(127, 195, 151, 255),
            new Color32(131, 190, 225, 255), new Color32(239, 151, 192, 255),
            new Color32(180, 156, 223, 255)
        };

        public static void Apply()
        {
            if (File.Exists(LevelPath))
                throw new System.InvalidOperationException("The coming-of-age level already exists; preserve its scene edits.");
            circle = LoadSprite("Assets/PinkDream/Geometry/Circle.asset");
            square = LoadSprite("Assets/PinkDream/Geometry/Square.asset");
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PinkDream/Geometry/RagdollGeometry.mat");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/art/Date Night.ttf");
            star = LoadOrCreateStar();
            string error = AssetDatabase.MoveAsset(OldPath, LevelPath);
            if (!string.IsNullOrEmpty(error))
                throw new System.InvalidOperationException(error);
            Scene scene = EditorSceneManager.OpenScene(LevelPath);
            Camera camera = Camera.main;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != camera.gameObject)
                    Object.DestroyImmediate(root);
            camera.orthographicSize = 4.4f;

            Transform floor = Group("成年礼 · 自由组合区域", null, new Vector2(0, -1.8f));
            Shape(floor, "地毯外缘", circle, Vector2.zero, new Vector2(10.6f, 3.1f), new Color32(217, 195, 206, 255), -100);
            Shape(floor, "奶油色地毯", circle, Vector2.zero, new Vector2(10.45f, 2.95f), Cream, -99);
            var girlObject = new GameObject("女孩 · 成年礼布娃娃");
            girlObject.transform.position = new Vector3(-1.55f, 0.35f, 0);
            var girl = girlObject.AddComponent<FrozenRagdoll>();
            var girlData = new SerializedObject(girl);
            Ref(girlData, "interactionCamera", camera);
            Ref(girlData, "geometryShader", material.shader);
            girlData.FindProperty("showPrototypeOverlay").boolValue = false;
            girlData.FindProperty("bodyScale").floatValue = 1;
            girlData.FindProperty("headScale").floatValue = 1;
            girlData.ApplyModifiedPropertiesWithoutUndo();
            girl.BakeSceneGeometry();
            girlData = new SerializedObject(girl);
            var colors = girlData.FindProperty("bodyColors");
            for (int i = 0; i < colors.arraySize; i++)
            {
                if (i == 1 || i == 3 || i == 5)
                    continue;
                var body = (SpriteRenderer)colors.GetArrayElementAtIndex(i).objectReferenceValue;
                foreach (SpriteRenderer renderer in body.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = i >= 6 ? new Color32(78, 75, 89, 255) : new Color32(201, 192, 213, 255);
            }
            foreach (SpriteRenderer renderer in girl.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.name == "Foot")
                    renderer.color = Ink;
                if (renderer.name == "Hand outline")
                    renderer.sortingOrder = 350;
                if (renderer.name == "Hand")
                    renderer.sortingOrder = 351;
            }
            Transform head = girl.transform.Find("Geometric ragdoll/Head");
            Shape(head, "深色短发", circle, new Vector2(0, 0.30f), new Vector2(1.05f, 0.50f), new Color32(58, 55, 65, 255), 112);
            Shape(head, "保留的银色耳钉", circle, new Vector2(0.51f, -0.12f), Vector2.one * 0.08f, new Color32(203, 207, 216, 255), 114);
            Transform leftArm = girl.transform.Find("Geometric ragdoll/Left forearm");
            Transform rightArm = girl.transform.Find("Geometric ragdoll/Right forearm");
            Transform leftHand = Group("左手抓取锚点", leftArm, new Vector2(0, -girl.Model.Parts[3].Length * 0.5f));
            Transform rightHand = Group("右手抓取锚点", rightArm, new Vector2(0, -girl.Model.Parts[5].Length * 0.5f));
            var neutralFace = new[] { head.Find("Left eye").gameObject, head.Find("Right eye").gameObject, head.Find("Mouth").gameObject };
            Transform happyFace = Group("庆祝时显示 · 开心表情", head, Vector2.zero);
            for (int side = -1; side <= 1; side += 2)
                Line(happyFace, side < 0 ? "开心左眼" : "开心右眼", new[]
                {
                    new Vector3(side * 0.17f - 0.07f, 0.04f, 0),
                    new Vector3(side * 0.17f, 0.10f, 0),
                    new Vector3(side * 0.17f + 0.07f, 0.04f, 0)
                }, 0.028f, Ink, 114);
            Line(happyFace, "开心微笑", new[] { new Vector3(-0.11f, -0.13f, 0), new Vector3(-0.06f, -0.20f, 0), new Vector3(0.06f, -0.20f, 0), new Vector3(0.11f, -0.13f, 0) }, 0.03f, Ink, 114);
            happyFace.gameObject.SetActive(false);
            Transform poseRoot = Group("庆祝目标姿势 · 可编辑参数", null, Vector2.zero);
            var happyPose = new Transform[girl.Model.Parts.Count];
            float[] angles = { 0, 0, -135, -155, 135, 155, -12, 0, 12, 0 };
            for (int i = 0; i < happyPose.Length; i++)
            {
                happyPose[i] = Group(girl.Model.Parts[i].Name, poseRoot, girl.Model.Parts[i].Position);
                happyPose[i].rotation = Quaternion.Euler(0, 0, angles[i]);
            }
            happyPose[0].position = new Vector3(-1.8f, 0.1f, 0);
            foreach (PoseRagdollModel.Joint joint in girl.Model.Joints)
                happyPose[joint.B].position = happyPose[joint.A].TransformPoint(joint.AnchorA) - happyPose[joint.B].TransformVector(joint.AnchorB);
            poseRoot.gameObject.SetActive(false);

            Transform cake = Group("成年礼蛋糕 · 自由组合", null, new Vector2(2.45f, -1.15f));
            Transform glow = Group("庆祝时点亮 · 蛋糕暖光", cake, Vector2.zero);
            Shape(glow, "柔和暖光", circle, new Vector2(0, 0.35f), new Vector2(4.45f, 3.2f), new Color(1, 0.83f, 0.42f, 0.19f), 90);
            glow.gameObject.SetActive(false);
            Shape(cake, "蛋糕托盘", circle, new Vector2(0, -0.77f), new Vector2(3.95f, 0.42f), new Color32(190, 178, 195, 255), 100);
            Shape(cake, "蛋糕外缘", square, new Vector2(0, -0.06f), new Vector2(3.35f, 1.30f), Ink, 101);
            Shape(cake, "奶油蛋糕侧面", square, new Vector2(0, -0.06f), new Vector2(3.25f, 1.20f), Cream, 102);
            Shape(cake, "蛋糕顶面外缘", circle, new Vector2(0, 0.56f), new Vector2(3.35f, 0.80f), Ink, 103);
            Shape(cake, "奶油顶面", circle, new Vector2(0, 0.57f), new Vector2(3.25f, 0.70f), new Color32(255, 252, 242, 255), 104);
            for (int i = 0; i < 12; i++)
                Shape(cake, "基础奶油边 " + (i + 1), circle, new Vector2(-1.43f + i * 0.26f, 0.35f), Vector2.one * 0.25f, Cream, 105);
            Transform dropZone = Group("蛋糕旁 · 拿着物品的手进入后自动变成装饰", cake, new Vector2(0, 0.48f));
            var cakeArea = dropZone.gameObject.AddComponent<BoxCollider2D>();
            cakeArea.isTrigger = true;
            cakeArea.size = new Vector2(3.50f, 1.35f);
            var flames = new List<GameObject>();
            Transform numberOne = Group("十八岁 · 数字一蜡烛", cake, new Vector2(-0.27f, 0.86f));
            Line(numberOne, "数字一", new[] { new Vector3(-0.12f, 0.43f, 0), new Vector3(0, 0.59f, 0), Vector3.zero }, 0.085f, Gold, 220);
            flames.Add(Flame(numberOne, new Vector2(0, 0.77f)));
            Transform numberEight = Group("十八岁 · 数字八蜡烛", cake, new Vector2(0.25f, 0.85f));
            Ring(numberEight, "八的上半部", new Vector2(0, 0.45f), 0.32f, 0.075f, Gold, 220);
            Ring(numberEight, "八的下半部", new Vector2(0, 0.15f), 0.35f, 0.075f, Gold, 220);
            flames.Add(Flame(numberEight, new Vector2(0, 0.78f)));

            Transform candle = Group("选择画笔后 · 黄色蜡烛", cake, new Vector2(-1.00f, 0.75f));
            Shape(candle, "黄色蜡烛", square, new Vector2(0, 0.34f), new Vector2(0.19f, 0.68f), Gold, 221);
            Shape(candle, "蜡烛亮边", square, new Vector2(-0.045f, 0.34f), new Vector2(0.04f, 0.64f), Cream, 222);
            flames.Add(Flame(candle, new Vector2(0, 0.85f)));
            candle.gameObject.SetActive(false);
            Transform flower = Group("选择种子后 · 绿色小花", cake, new Vector2(1.02f, 0.78f));
            Shape(flower, "花茎", square, new Vector2(0, 0.24f), new Vector2(0.055f, 0.48f), Green, 221);
            Shape(flower, "左叶片", circle, new Vector2(-0.13f, 0.20f), new Vector2(0.29f, 0.12f), Green, 222).transform.localRotation = Quaternion.Euler(0, 0, -30);
            Shape(flower, "右叶片", circle, new Vector2(0.13f, 0.30f), new Vector2(0.29f, 0.12f), Green, 222).transform.localRotation = Quaternion.Euler(0, 0, 30);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 2f * Mathf.PI / 5;
                Shape(flower, "绿色花瓣 " + i, circle, new Vector2(Mathf.Cos(a) * 0.15f, 0.60f + Mathf.Sin(a) * 0.15f), Vector2.one * 0.24f, new Color32(151, 206, 143, 255), 223);
            }
            Shape(flower, "花心", circle, new Vector2(0, 0.60f), Vector2.one * 0.20f, new Color32(220, 232, 157, 255), 224);
            flower.gameObject.SetActive(false);
            Transform floatingStar = Group("选择星星后 · 蛋糕上方的星星", cake, new Vector2(0, 2.12f));
            Shape(floatingStar, "漂浮星星", star, Vector2.zero, Vector2.one * 0.72f, Gold, 225);
            Shape(floatingStar, "星星亮光", star, new Vector2(-0.08f, 0.10f), Vector2.one * 0.18f, Cream, 226);
            floatingStar.gameObject.SetActive(false);
            Transform creamBow = Group("选择蝴蝶结后 · 粉色奶油装饰", cake, new Vector2(-0.72f, -0.12f));
            Bow(creamBow, 0.82f, 221, null);
            creamBow.gameObject.SetActive(false);
            Transform candy = Group("选择相机后 · 闪光小糖片", cake, new Vector2(0.76f, -0.12f));
            Shape(candy, "糖片", square, Vector2.zero, new Vector2(0.42f, 0.42f), new Color32(239, 216, 165, 255), 221).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Shape(candy, "糖片亮面", square, new Vector2(-0.04f, 0.05f), new Vector2(0.24f, 0.24f), Cream, 222).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Transform sparkle = Group("糖片的闪光", candy, new Vector2(0.27f, 0.25f));
            Shape(sparkle, "闪光", star, Vector2.zero, Vector2.one * 0.28f, Gold, 223);
            candy.gameObject.SetActive(false);

            Transform looseItems = Group("可自由选择的五件物品", null, Vector2.zero);
            var choices = new List<ComingOfAgeLevel.Choice>();
            var paths = new List<Vector2[]>();
            Transform brush = Group("画笔", looseItems, new Vector2(-4.12f, 1.25f));
            Solid(brush, "木质笔杆", square, new Vector2(0, -0.13f), new Vector2(0.15f, 1.0f), new Color32(187, 141, 105, 255), 211, paths);
            Solid(brush, "金属箍", square, new Vector2(0, 0.38f), new Vector2(0.20f, 0.22f), new Color32(175, 184, 197, 255), 212, paths);
            Solid(brush, "黄色笔毛", circle, new Vector2(0, 0.64f), new Vector2(0.28f, 0.37f), Gold, 213, paths);
            choices.Add(Choice(brush, "画笔", paths, candle, ComingOfAgeLevel.DecorationMotion.Still));
            paths = new List<Vector2[]>();
            Transform seed = Group("种子", looseItems, new Vector2(-4.14f, -0.54f));
            Solid(seed, "种子本体", circle, Vector2.zero, new Vector2(0.40f, 0.57f), Green, 211, paths);
            Shape(seed, "种子亮边", circle, new Vector2(-0.055f, 0.07f), new Vector2(0.10f, 0.27f), new Color32(194, 219, 161, 255), 212);
            choices.Add(Choice(seed, "种子", paths, flower, ComingOfAgeLevel.DecorationMotion.Flower));
            paths = new List<Vector2[]>();
            Transform starItem = Group("星星", looseItems, new Vector2(-3.75f, -2.15f));
            Solid(starItem, "金色星星", star, Vector2.zero, Vector2.one * 0.68f, Gold, 211, paths);
            choices.Add(Choice(starItem, "星星", paths, floatingStar, ComingOfAgeLevel.DecorationMotion.Star));
            paths = new List<Vector2[]>();
            Transform bowItem = Group("蝴蝶结", looseItems, new Vector2(-1.15f, -2.96f));
            Bow(bowItem, 0.93f, 211, paths);
            choices.Add(Choice(bowItem, "蝴蝶结", paths, creamBow, ComingOfAgeLevel.DecorationMotion.Still));
            paths = new List<Vector2[]>();
            Transform cameraItem = Group("相机", looseItems, new Vector2(0.65f, -2.95f));
            Solid(cameraItem, "相机机身", square, Vector2.zero, new Vector2(0.90f, 0.59f), new Color32(91, 94, 109, 255), 211, paths);
            Solid(cameraItem, "相机顶部", square, new Vector2(-0.18f, 0.32f), new Vector2(0.31f, 0.12f), Ink, 211, paths);
            Shape(cameraItem, "镜头外圈", circle, new Vector2(0.08f, 0), Vector2.one * 0.41f, new Color32(188, 198, 210, 255), 212);
            Shape(cameraItem, "相机镜头", circle, new Vector2(0.08f, 0), Vector2.one * 0.29f, Ink, 213);
            Shape(cameraItem, "闪光灯", square, new Vector2(-0.29f, 0.17f), new Vector2(0.16f, 0.09f), Cream, 213);
            var cameraChoice = Choice(cameraItem, "相机", paths, candy, ComingOfAgeLevel.DecorationMotion.Candy);
            cameraChoice.sparkle = sparkle;
            choices.Add(cameraChoice);

            Transform cord = Group("装扮完毕 · 向下拉的拉绳", null, new Vector2(5.12f, 2.9f));
            Transform handle = Group("拉绳握环", cord, new Vector2(0, -1.1f));
            Shape(handle, "握环轮廓", circle, Vector2.zero, Vector2.one * 0.39f, Ink, 260);
            Shape(handle, "握环颜色", circle, Vector2.zero, Vector2.one * 0.29f, Gold, 261);
            var handleCollider = handle.gameObject.AddComponent<CircleCollider2D>();
            handleCollider.isTrigger = true;
            handleCollider.radius = 0.195f;
            LineRenderer rope = Line(cord, "拉绳", new[] { Vector3.zero, new Vector3(0, -1.1f, 0) }, 0.035f, Ink, 259);
            WorldText(cord, "向下拉\n装扮完毕", new Vector2(-0.30f, -1.94f));

            var ui = new GameObject("成年礼 · 可编辑提示与庆祝文字", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            Text preparationHint = ScreenText(ui.transform, "关卡提示", "拿起喜欢的物品，把它变成蛋糕上的装饰", new Vector2(0, -76), 32, true);
            Text combination = ScreenText(ui.transform, "自由组合进度", "已组合 0 种装饰 · 可以继续，也可以随时完成", new Vector2(0, -130), 22, true);
            Text feedback = ScreenText(ui.transform, "操作提示", "拖动女孩的手碰到物品，拿起后把手带到蛋糕旁。", new Vector2(0, 19), 22, false);

            var party = new GameObject("成年礼 · 点亮与彩色庆祝设置").AddComponent<ComingOfAgeCelebration>();
            var partyData = new SerializedObject(party);
            Ref(partyData, "sceneCamera", camera);
            Ref(partyData, "girl", girl);
            Refs(partyData, "happyPose", happyPose);
            Refs(partyData, "neutralFace", neutralFace);
            Ref(partyData, "happyFace", happyFace.gameObject);
            Ref(partyData, "restartHint", feedback);
            Ref(partyData, "cakeGlow", glow.gameObject);
            Refs(partyData, "flames", flames);
            Transform effects = Group("庆祝时启用 · 彩色礼花与气球", null, Vector2.zero);
            Scene birth = EditorSceneManager.OpenScene("Assets/Scenes/Level01_BeforeBirth.unity", OpenSceneMode.Additive);
            var sourceData = new SerializedObject(Object.FindObjectOfType<BirthCompletionSequence>());
            var confetti = new List<ParticleSystem>();
            var balloons = new List<Transform>();
            var gradient = new Gradient();
            var keys = new GradientColorKey[Palette.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new GradientColorKey(Palette[i], i / (float)(keys.Length - 1));
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            SerializedProperty sourceParticles = sourceData.FindProperty("confetti");
            for (int i = 0; i < 2; i++)
            {
                var source = (ParticleSystem)sourceParticles.GetArrayElementAtIndex(i).objectReferenceValue;
                ParticleSystem copy = Object.Instantiate(source, effects);
                copy.name = i == 0 ? "左侧向中间 · 彩色礼花" : "右侧向中间 · 彩色礼花";
                copy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = copy.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var colorsGradient = new ParticleSystem.MinMaxGradient(gradient);
                colorsGradient.mode = ParticleSystemGradientMode.RandomColor;
                main.startColor = colorsGradient;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2, 3);
                var emission = copy.emission;
                emission.rateOverTime = 18;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)12) });
                var velocity = copy.velocityOverLifetime;
                velocity.enabled = false;
                confetti.Add(copy);
            }
            SerializedProperty sourceBalloons = sourceData.FindProperty("balloons");
            for (int i = 0; i < 10; i++)
            {
                var source = (Transform)sourceBalloons.GetArrayElementAtIndex(i % sourceBalloons.arraySize).objectReferenceValue;
                Transform copy = Object.Instantiate(source, effects);
                copy.name = "底部释放 · 彩色气球 " + (i + 1);
                foreach (SpriteRenderer sprite in copy.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!sprite.name.Contains("轮廓"))
                        sprite.color = Palette[i % Palette.Length];
                    sprite.sortingOrder = 430 + sprite.sortingOrder % 10;
                }
                copy.gameObject.SetActive(false);
                balloons.Add(copy);
            }
            EditorSceneManager.CloseScene(birth, true);
            SceneManager.SetActiveScene(scene);
            Refs(partyData, "confetti", confetti);
            Refs(partyData, "balloons", balloons);
            partyData.ApplyModifiedPropertiesWithoutUndo();

            var controller = new GameObject("成年礼 · 自由组合与拉绳交互").AddComponent<ComingOfAgeLevel>();
            var data = new SerializedObject(controller);
            Ref(data, "interactionCamera", camera);
            Ref(data, "girl", girl);
            Ref(data, "leftHandAnchor", leftHand);
            Ref(data, "rightHandAnchor", rightHand);
            Ref(data, "looseItems", looseItems);
            Ref(data, "cakeArea", cakeArea);
            Ref(data, "finishControl", cord.gameObject);
            Ref(data, "pullHandle", handle);
            Ref(data, "handleCollider", handleCollider);
            Ref(data, "pullRope", rope);
            Ref(data, "combinationText", combination);
            Ref(data, "feedbackText", feedback);
            Refs(data, "preparationHints", new[] { preparationHint.gameObject, combination.gameObject });
            Ref(data, "celebration", party);
            SerializedProperty list = data.FindProperty("choices");
            list.arraySize = choices.Count;
            for (int i = 0; i < choices.Count; i++)
            {
                SerializedProperty choice = list.GetArrayElementAtIndex(i);
                choice.FindPropertyRelative("itemName").stringValue = choices[i].itemName;
                choice.FindPropertyRelative("item").objectReferenceValue = choices[i].item;
                choice.FindPropertyRelative("collider").objectReferenceValue = choices[i].collider;
                choice.FindPropertyRelative("decoration").objectReferenceValue = choices[i].decoration;
                choice.FindPropertyRelative("motion").enumValueIndex = (int)choices[i].motion;
                choice.FindPropertyRelative("sparkle").objectReferenceValue = choices[i].sparkle;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Scene adolescence = EditorSceneManager.OpenScene("Assets/Scenes/Level04_AdolescencePiercing.unity");
            var adolescenceData = new SerializedObject(Object.FindObjectOfType<AdolescencePiercingLevel>());
            adolescenceData.FindProperty("nextScene").stringValue = "Level05_ComingOfAge";
            adolescenceData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(adolescence);
            EditorSceneManager.SaveScene(adolescence);
            var buildScenes = new List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                buildScenes.Add(entry.path == OldPath || entry.path == LevelPath ? new EditorBuildSettingsScene(LevelPath, true) : entry);
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Saved independent coming-of-age level: five freely combinable cake decorations, contact-only hand pickup, a manual finish pull cord, lit cake, multicolour side confetti and balloons, and a connected happy pose. No Play mode or tests executed.");
        }

        private static ComingOfAgeLevel.Choice Choice(Transform root, string name, List<Vector2[]> paths, Transform decoration, ComingOfAgeLevel.DecorationMotion motion)
        {
            var collider = root.gameObject.AddComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            collider.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
                collider.SetPath(i, paths[i]);
            return new ComingOfAgeLevel.Choice { itemName = EnglishTypographyAuthoring.Translate(name), item = root, collider = collider, decoration = decoration, motion = motion };
        }

        private static void Bow(Transform root, float size, int order, List<Vector2[]> paths)
        {
            var shapes = paths ?? new List<Vector2[]>();
            Solid(root, "左侧蝴蝶结", circle, new Vector2(-size * 0.25f, 0.03f), new Vector2(size * 0.54f, size * 0.35f), Pink, order, shapes, -25);
            Solid(root, "右侧蝴蝶结", circle, new Vector2(size * 0.25f, 0.03f), new Vector2(size * 0.54f, size * 0.35f), Pink, order, shapes, 25);
            Solid(root, "左侧缎带", square, new Vector2(-0.11f, -size * 0.21f), new Vector2(size * 0.18f, size * 0.40f), Pink, order, shapes, -15);
            Solid(root, "右侧缎带", square, new Vector2(0.11f, -size * 0.21f), new Vector2(size * 0.18f, size * 0.40f), Pink, order, shapes, 15);
            Solid(root, "中间的结", circle, Vector2.zero, Vector2.one * size * 0.22f, new Color32(203, 116, 158, 255), order + 1, shapes);
        }

        private static GameObject Flame(Transform parent, Vector2 position)
        {
            Transform flame = Group("庆祝时点亮 · 烛火", parent, position);
            Shape(flame, "烛火暖晕", circle, Vector2.zero, new Vector2(0.46f, 0.55f), new Color(1, 0.8f, 0.3f, 0.20f), 241);
            Shape(flame, "橙色火焰", circle, Vector2.zero, new Vector2(0.14f, 0.26f), new Color32(250, 177, 68, 255), 242);
            Shape(flame, "明亮火焰", circle, new Vector2(0, -0.02f), new Vector2(0.075f, 0.17f), Cream, 243);
            flame.gameObject.SetActive(false);
            return flame.gameObject;
        }

        private static void Solid(Transform root, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order, List<Vector2[]> paths, float angle = 0)
        {
            SpriteRenderer renderer = Shape(root, name, sprite, position, size, color, order);
            renderer.transform.localRotation = Quaternion.Euler(0, 0, angle);
            Vector2[] points;
            if (sprite == star)
                points = StarPoints();
            else if (sprite == circle)
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

        private static Vector2[] StarPoints()
        {
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (90 + i * 36) * Mathf.Deg2Rad;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 0.5f : 0.22f);
            }
            return points;
        }

        private static Sprite LoadOrCreateStar()
        {
            Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
            if (result != null)
                return result;
            Texture2D texture = LoadOrCreateStarTexture();
            result = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, texture.width, 0, SpriteMeshType.FullRect);
            result.name = "ComingOfAgeStar";
            AssetDatabase.CreateAsset(result, StarPath);
            return result;
        }

        internal static Texture2D LoadOrCreateStarTexture()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StarTexturePath);
            if (texture != null)
                return texture;
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural five-point star", wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            Vector2[] outline = StarPoints();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int coverage = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            Vector2 point = new Vector2((x + 0.25f + sx * 0.5f) / size - 0.5f, (y + 0.25f + sy * 0.5f) / size - 0.5f);
                            bool inside = false;
                            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
                            {
                                Vector2 a = outline[i], b = outline[j];
                                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                                    inside = !inside;
                            }
                            if (inside)
                                coverage++;
                        }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(coverage * 255 / 4));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, StarTexturePath);
            return texture;
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

        private static LineRenderer Line(Transform parent, string name, Vector3[] points, float width, Color color, int order)
        {
            var line = Group(name, parent, Vector2.zero).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.positionCount = points.Length;
            line.SetPositions(points);
            return line;
        }

        private static void Ring(Transform parent, string name, Vector2 center, float diameter, float width, Color color, int order)
        {
            var points = new Vector3[32];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * 2f * Mathf.PI / points.Length;
                points[i] = (Vector3)center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * ((diameter - width) * 0.5f);
            }
            Line(parent, name, points, width, color, order).loop = true;
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

        private static void WorldText(Transform parent, string text, Vector2 position)
        {
            var go = new GameObject(text + " · 标签", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 250;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.01f;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(210, 88);
            Text label = TextObject(go.transform, "文字", text, 23);
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
            label.text = EnglishTypographyAuthoring.Translate(text);
            label.fontSize = size;
            label.color = Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        private static void Ref(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;

        private static void Refs<T>(SerializedObject data, string name, IList<T> values) where T : Object
        {
            SerializedProperty list = data.FindProperty(name);
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
