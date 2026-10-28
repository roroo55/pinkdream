using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PinkDream.Editor
{
    public static class CharacterArtAuthoring
    {
        private static readonly string[] Names = { "head", "body", "左上臂", "左下臂", "左手", "右上臂", "右下臂", "右手", "左腿", "右腿", "跨" };
        private static readonly string[] BoneNames = { "Torso", "Head", "Left upper arm", "Left forearm", "Right upper arm", "Right forearm", "Left thigh", "Left shin", "Right thigh", "Right shin" };
        private static readonly float[] Masses = { 4, 1.2f, 0.65f, 0.52f, 0.65f, 0.52f, 1, 0.8f, 1, 0.8f };
        private static readonly int[] Orders = { 100, 60, 80, 110, 82, 112, 20, 21, 22, 23 };

        private sealed class PartImage
        {
            public string name, path;
            public int width, height;
            public byte[] alpha;
            public Color32[] pixels;
            public RectInt bounds;
            public readonly Dictionary<string, RectInt> rects = new Dictionary<string, RectInt>();
            public readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        }

        [MenuItem("Pink Dream/匹配第四、第五关人物原画")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            ApplyStage("青少年", "Assets/Scenes/Level04_AdolescencePiercing.unity");
            ApplyStage("成年", "Assets/Scenes/Level05_ComingOfAge.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved both original-art characters, alpha-centred sprites, matching hand and ear targets, and the original ten-part frozen joint structure. Original PNG files preserved. No Play mode or tests executed.");
        }

        private static void ApplyStage(string stage, string path)
        {
            var images = new Dictionary<string, PartImage>();
            foreach (string name in Names)
                images.Add(name, ReadImage("Assets/art/" + stage + "/" + name + ".png", name));
            RectInt union = images[Names[0]].bounds;
            foreach (PartImage part in images.Values)
                union = Union(union, part.bounds);
            float pixelsPerUnit = union.height / 5.2f;
            Vector2 reference = new Vector2(images["body"].bounds.center.x, union.center.y + pixelsPerUnit * 0.1f);
            foreach (PartImage part in images.Values)
                ImportSprites(part, pixelsPerUnit);
            RagdollArtProfile profile = BuildProfile(stage, images, reference, pixelsPerUnit);

            var scene = EditorSceneManager.OpenScene(path);
            FrozenRagdoll girl = Object.FindObjectOfType<FrozenRagdoll>();
            var data = new SerializedObject(girl);
            float scale = data.FindProperty("bodyScale").floatValue;
            var boneList = data.FindProperty("partViews");
            var bones = new Transform[10];
            for (int i = 0; i < bones.Length; i++)
                bones[i] = (Transform)boneList.GetArrayElementAtIndex(i).objectReferenceValue;
            Vector3 origin = girl.transform.position;
            Vector3 World(Vector2 pixel) => origin + (Vector3)((pixel - reference) / pixelsPerUnit * scale);

            var keep = new HashSet<Transform>();
            var adolescence = Object.FindObjectOfType<AdolescencePiercingLevel>();
            var party = Object.FindObjectOfType<ComingOfAgeCelebration>();
            SerializedObject levelData = new SerializedObject(adolescence != null ? (Object)adolescence : Object.FindObjectOfType<ComingOfAgeLevel>());
            Transform leftHand = (Transform)levelData.FindProperty("leftHandAnchor").objectReferenceValue;
            Transform rightHand = (Transform)levelData.FindProperty("rightHandAnchor").objectReferenceValue;
            keep.Add(leftHand);
            keep.Add(rightHand);

            RectInt[] ears = FindEars(images["head"]);
            if (adolescence != null)
            {
                SerializedProperty targets = levelData.FindProperty("ears");
                for (int i = 0; i < targets.arraySize; i++)
                {
                    var target = (CircleCollider2D)targets.GetArrayElementAtIndex(i).FindPropertyRelative("lobe").objectReferenceValue;
                    var earring = (GameObject)targets.GetArrayElementAtIndex(i).FindPropertyRelative("earring").objectReferenceValue;
                    Transform oldParent = target.transform.parent;
                    target.transform.SetParent(bones[1], true);
                    if (oldParent != bones[1] && (oldParent.name == "左耳" || oldParent.name == "右耳"))
                        Object.DestroyImmediate(oldParent.gameObject);
                    foreach (Transform child in target.transform.Cast<Transform>().ToArray())
                        if (child.gameObject != earring)
                            Object.DestroyImmediate(child.gameObject);
                    target.name = i == 0 ? "左耳垂 · 原画接触位置" : "右耳垂 · 原画接触位置";
                    target.transform.localScale = Vector3.one;
                    target.radius = Mathf.Max(0.02f, ears[i].width * 0.4f / pixelsPerUnit * scale);
                    target.isTrigger = true;
                    earring.transform.localScale = Vector3.one * (0.6f * scale);
                    earring.SetActive(false);
                    keep.Add(target.transform);
                }
            }

            Transform happyFace = null, adultStud = null;
            SerializedObject partyData = party != null ? new SerializedObject(party) : null;
            if (party != null)
            {
                happyFace = ((GameObject)partyData.FindProperty("happyFace").objectReferenceValue).transform;
                foreach (Transform child in happyFace.Cast<Transform>().ToArray())
                    Object.DestroyImmediate(child.gameObject);
                happyFace.name = "开心表情 · 使用成年原画笑脸";
                happyFace.gameObject.SetActive(false);
                keep.Add(happyFace);
                partyData.FindProperty("neutralFace").arraySize = 0;
                adultStud = bones[1].Find("保留的银色耳钉");
                if (adultStud != null)
                    keep.Add(adultStud);
                partyData.ApplyModifiedPropertiesWithoutUndo();
            }

            for (int i = 0; i < bones.Length; i++)
            {
                foreach (Transform child in bones[i].Cast<Transform>().ToArray())
                    if (!keep.Contains(child))
                        Object.DestroyImmediate(child.gameObject);
                foreach (Collider2D old in bones[i].GetComponents<Collider2D>())
                    Object.DestroyImmediate(old);
                bones[i].SetPositionAndRotation(origin + (Vector3)(profile.parts[i].position * scale), Quaternion.Euler(0, 0, profile.parts[i].angleDegrees));
                bones[i].localScale = Vector3.one;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PinkDream/Geometry/RagdollGeometry.mat");
            var renderers = new SpriteRenderer[10];
            var outlines = new List<Vector2[]>[10];
            for (int i = 0; i < outlines.Length; i++) outlines[i] = new List<Vector2[]>();
            void Attach(int bone, string source, string spriteName, int order, bool primary)
            {
                PartImage part = images[source];
                Transform view = new GameObject(spriteName + " · 原画").transform;
                view.SetParent(bones[bone], false);
                view.position = World(part.rects[spriteName].center);
                view.rotation = Quaternion.identity;
                view.localScale = Vector3.one * scale;
                SpriteRenderer renderer = view.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = part.sprites[spriteName];
                renderer.sharedMaterial = material;
                renderer.color = Color.white;
                renderer.sortingOrder = primary ? Orders[bone] : order;
                if (primary) renderers[bone] = renderer;
                foreach (Vector2[] outline in TraceOutlines(part, part.rects[spriteName]))
                {
                    for (int p = 0; p < outline.Length; p++)
                        outline[p] = bones[bone].InverseTransformPoint(World(outline[p]));
                    outlines[bone].Add(outline);
                }
            }
            Attach(0, "body", "body", 100, true);
            Attach(0, "跨", "跨", 50, false);
            Attach(1, "head", "head", 60, true);
            Attach(2, "左上臂", "左上臂", 80, true);
            Attach(3, "左下臂", "左下臂", 110, true);
            Attach(3, "左手", "左手", 350, false);
            Attach(4, "右上臂", "右上臂", 80, true);
            Attach(5, "右下臂", "右下臂", 110, true);
            Attach(5, "右手", "右手", 351, false);
            Attach(6, "左腿", "左腿_大腿", 20, true);
            Attach(7, "左腿", "左腿_小腿", 21, true);
            Attach(8, "右腿", "右腿_大腿", 20, true);
            Attach(9, "右腿", "右腿_小腿", 21, true);
            for (int i = 0; i < bones.Length; i++)
            {
                var collider = bones[i].gameObject.AddComponent<PolygonCollider2D>();
                collider.isTrigger = true;
                collider.pathCount = outlines[i].Count;
                for (int p = 0; p < outlines[i].Count; p++) collider.SetPath(p, outlines[i][p]);
            }
            for (int hand = 0; hand < 2; hand++)
            {
                int bone = hand == 0 ? 3 : 5;
                string name = hand == 0 ? "左手" : "右手";
                var palm = bones[bone].gameObject.AddComponent<CircleCollider2D>();
                palm.isTrigger = true;
                palm.offset = new Vector2(0, -profile.parts[bone].length * scale * 0.5f);
                palm.radius = Mathf.Min(images[name].bounds.width, images[name].bounds.height) * 0.44f / pixelsPerUnit * scale;
                Transform anchor = hand == 0 ? leftHand : rightHand;
                anchor.localPosition = palm.offset;
                anchor.localRotation = Quaternion.identity;
            }
            if (adolescence != null)
            {
                SerializedProperty targets = levelData.FindProperty("ears");
                for (int i = 0; i < targets.arraySize; i++)
                {
                    var target = (CircleCollider2D)targets.GetArrayElementAtIndex(i).FindPropertyRelative("lobe").objectReferenceValue;
                    target.transform.position = World(EarLobe(ears[i]));
                    target.transform.localRotation = Quaternion.identity;
                }
            }
            if (adultStud != null)
            {
                adultStud.position = World(EarLobe(ears[1]));
                adultStud.localScale = Vector3.one * (0.045f * scale);
                adultStud.GetComponent<SpriteRenderer>().sortingOrder = 360;
            }

            // Keep the familiar arms-down starting pose; the original T-pose is
            // the measured bind pose, so artwork is rotated without being stretched.
            bones[2].rotation = Quaternion.Euler(0, 0, -44);
            bones[3].rotation = Quaternion.Euler(0, 0, -12);
            bones[4].rotation = Quaternion.Euler(0, 0, 44);
            bones[5].rotation = Quaternion.Euler(0, 0, 12);
            RebuildPositions(bones, profile, scale);
            data = new SerializedObject(girl);
            data.FindProperty("artProfile").objectReferenceValue = profile;
            SerializedProperty bodyColors = data.FindProperty("bodyColors");
            for (int i = 0; i < renderers.Length; i++)
                bodyColors.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            if (party != null)
            {
                partyData.Update();
                SerializedProperty targetPose = partyData.FindProperty("happyPose");
                var targets = new Transform[10];
                for (int i = 0; i < targets.Length; i++)
                    targets[i] = (Transform)targetPose.GetArrayElementAtIndex(i).objectReferenceValue;
                RebuildPositions(targets, profile, scale);
                foreach (Transform target in targets) EditorUtility.SetDirty(target);
            }
            EditorUtility.SetDirty(girl);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(stage + ": saved 11 source parts as 13 art renderers, alpha-centred sprite rectangles, measured joints, and source-colour materials. Hand/ear anchors follow the artwork.");
        }

        private static void RebuildPositions(Transform[] bones, RagdollArtProfile profile, float scale)
        {
            foreach (RagdollArtProfile.Connection joint in profile.joints)
                bones[joint.child].position = bones[joint.parent].TransformPoint(joint.parentAnchor * scale) - bones[joint.child].TransformVector(joint.childAnchor * scale);
        }

        private static RagdollArtProfile BuildProfile(string stage, Dictionary<string, PartImage> images, Vector2 reference, float ppu)
        {
            string assetPath = "Assets/art/" + stage + "/" + stage + "人物关节.asset";
            RagdollArtProfile result = AssetDatabase.LoadAssetAtPath<RagdollArtProfile>(assetPath);
            if (result == null)
            {
                result = ScriptableObject.CreateInstance<RagdollArtProfile>();
                AssetDatabase.CreateAsset(result, assetPath);
            }
            result.parts = new RagdollArtProfile.BodyPart[10];
            result.name = stage + "人物关节";
            Vector2 Point(Vector2 pixel) => (pixel - reference) / ppu;
            void Part(int index, Vector2 a, Vector2 b, RectInt bounds)
            {
                Vector2 up = a - b;
                result.parts[index] = new RagdollArtProfile.BodyPart
                {
                    name = BoneNames[index], position = Point((a + b) * 0.5f),
                    angleDegrees = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90,
                    length = Mathf.Max(0.01f, up.magnitude / ppu),
                    width = Mathf.Min(bounds.width, bounds.height) / ppu,
                    mass = Masses[index]
                };
            }
            for (int i = 0; i < 2; i++)
            {
                RectInt bounds = images[i == 0 ? "body" : "head"].bounds;
                result.parts[i] = new RagdollArtProfile.BodyPart { name = BoneNames[i], position = Point(bounds.center), angleDegrees = 0,
                    length = bounds.height / ppu, width = bounds.width / ppu, mass = Masses[i], isCircle = i == 1 };
            }
            var connections = new List<RagdollArtProfile.Connection>();
            void Connect(int parent, int child, Vector2 pixel)
            {
                Vector2 p = Point(pixel);
                connections.Add(new RagdollArtProfile.Connection
                {
                    parent = parent, child = child,
                    parentAnchor = Rotate(p - result.parts[parent].position, -result.parts[parent].angleDegrees),
                    childAnchor = Rotate(p - result.parts[child].position, -result.parts[child].angleDegrees)
                });
            }
            Connect(0, 1, new Vector2(images["body"].bounds.center.x, images["body"].bounds.yMax - 8));
            for (int side = 0; side < 2; side++)
            {
                string prefix = side == 0 ? "左" : "右";
                PartImage upper = images[prefix + "上臂"], lower = images[prefix + "下臂"];
                int inner = side == 0 ? upper.bounds.xMax - 8 : upper.bounds.xMin + 8;
                Vector2 shoulder = new Vector2(inner, ColumnCentre(upper, inner));
                int outer = side == 0 ? upper.bounds.xMin + 3 : upper.bounds.xMax - 4;
                int forearmInner = side == 0 ? lower.bounds.xMax - 4 : lower.bounds.xMin + 3;
                Vector2 elbow = new Vector2((outer + forearmInner) * 0.5f, (ColumnCentre(upper, outer) + ColumnCentre(lower, forearmInner)) * 0.5f);
                Vector2 palm = images[prefix + "手"].bounds.center;
                int arm = side == 0 ? 2 : 4;
                Part(arm, shoulder, elbow, upper.bounds);
                Part(arm + 1, elbow, palm, lower.bounds);
                Connect(0, arm, shoulder);
                Connect(arm, arm + 1, elbow);
            }
            for (int side = 0; side < 2; side++)
            {
                string name = side == 0 ? "左腿" : "右腿";
                PartImage leg = images[name];
                float kneeY = leg.bounds.center.y;
                Vector2 hip = new Vector2(RowCentre(leg, leg.bounds.yMax - 12), leg.bounds.yMax - 8);
                Vector2 knee = new Vector2(RowCentre(leg, Mathf.RoundToInt(kneeY)), kneeY);
                Vector2 toe = new Vector2(RowCentre(leg, leg.bounds.yMin + 16), leg.bounds.yMin + 12);
                int thigh = side == 0 ? 6 : 8;
                Part(thigh, hip, knee, leg.rects[name + "_大腿"]);
                Part(thigh + 1, knee, toe, leg.rects[name + "_小腿"]);
                Connect(0, thigh, hip);
                Connect(thigh, thigh + 1, knee);
            }
            result.joints = connections.ToArray();
            EditorUtility.SetDirty(result);
            return result;
        }

        private static PartImage ReadImage(string path, string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read " + path);
            Color32[] pixels = texture.GetPixels32();
            var data = new PartImage { name = name, path = path, width = texture.width, height = texture.height, pixels = name == "head" ? pixels : null };
            data.alpha = new byte[pixels.Length];
            for (int i = 0; i < data.alpha.Length; i++) data.alpha[i] = pixels[i].a;
            Object.DestroyImmediate(texture);
            data.bounds = AlphaBounds(data, new RectInt(0, 0, data.width, data.height));
            data.rects.Add(name, data.bounds);
            if (name == "左腿" || name == "右腿")
            {
                int knee = Mathf.RoundToInt(data.bounds.center.y);
                data.rects.Add(name + "_大腿", AlphaBounds(data, new RectInt(data.bounds.xMin, knee - 6, data.bounds.width, data.bounds.yMax - knee + 6)));
                data.rects.Add(name + "_小腿", AlphaBounds(data, new RectInt(data.bounds.xMin, data.bounds.yMin, data.bounds.width, knee + 6 - data.bounds.yMin)));
            }
            return data;
        }

        private static RectInt AlphaBounds(PartImage image, RectInt clip)
        {
            int x0 = clip.xMax, y0 = clip.yMax, x1 = -1, y1 = -1;
            for (int y = clip.yMin; y < clip.yMax; y++)
                for (int x = clip.xMin; x < clip.xMax; x++)
                    if (image.alpha[y * image.width + x] >= 8)
                    {
                        x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y);
                    }
            if (x1 < x0) throw new InvalidOperationException("Empty art region: " + image.path);
            x0 = Mathf.Max(clip.xMin, x0 - 2); y0 = Mathf.Max(clip.yMin, y0 - 2);
            x1 = Mathf.Min(clip.xMax, x1 + 3); y1 = Mathf.Min(clip.yMax, y1 + 3);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        private static void ImportSprites(PartImage part, float pixelsPerUnit)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(part.path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = new Vector2(part.bounds.center.x / part.width, part.bounds.center.y / part.height);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
#pragma warning disable 618
            SpriteMetaData[] previous = importer.spritesheet;
            var sprites = new List<SpriteMetaData>();
            foreach (var entry in part.rects)
            {
                SpriteMetaData meta = previous.FirstOrDefault(p => p.name == entry.Key);
                meta.name = entry.Key;
                RectInt r = entry.Value;
                meta.rect = new Rect(r.x, r.y, r.width, r.height);
                meta.alignment = (int)SpriteAlignment.Custom;
                meta.pivot = Vector2.one * 0.5f;
                sprites.Add(meta);
            }
            importer.spritesheet = sprites.ToArray();
#pragma warning restore 618
            importer.SaveAndReimport();
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(part.path).OfType<Sprite>())
                part.sprites.Add(sprite.name, sprite);
            if (part.sprites.Count != part.rects.Count) throw new InvalidOperationException("Sprite import mismatch: " + part.path);
        }

        private static float ColumnCentre(PartImage image, int x)
        {
            int lo = image.height, hi = -1;
            for (int y = image.bounds.yMin; y < image.bounds.yMax; y++)
                if (image.alpha[y * image.width + x] >= 16) { lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            return hi >= lo ? (lo + hi) * 0.5f : image.bounds.center.y;
        }

        private static float RowCentre(PartImage image, int y)
        {
            int lo = image.width, hi = -1;
            for (int x = image.bounds.xMin; x < image.bounds.xMax; x++)
                if (image.alpha[y * image.width + x] >= 16) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
            return hi >= lo ? (lo + hi) * 0.5f : image.bounds.center.x;
        }

        private static RectInt[] FindEars(PartImage head)
        {
            var mask = new bool[head.alpha.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                Color32 p = head.pixels[i];
                mask[i] = p.a > 128 && p.r > 185 && p.g > 130 && p.b > 100 && p.r - p.g > 8 && p.g - p.b > 3;
            }
            var areas = new List<KeyValuePair<int, RectInt>>();
            var queue = new Queue<int>();
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i]) continue;
                int x0 = head.width, y0 = head.height, x1 = 0, y1 = 0, count = 0;
                queue.Enqueue(i); mask[i] = false;
                while (queue.Count > 0)
                {
                    int pixel = queue.Dequeue(), x = pixel % head.width, y = pixel / head.width;
                    x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); count++;
                    void Visit(int next) { if (next >= 0 && next < mask.Length && mask[next]) { mask[next] = false; queue.Enqueue(next); } }
                    if (x > 0) Visit(pixel - 1); if (x < head.width - 1) Visit(pixel + 1);
                    if (y > 0) Visit(pixel - head.width); if (y < head.height - 1) Visit(pixel + head.width);
                }
                if (count > 100 && x1 - x0 < 80 && y1 - y0 < 150)
                    areas.Add(new KeyValuePair<int, RectInt>(count, new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1)));
            }
            var left = areas.Where(p => p.Value.center.x < head.bounds.center.x - 70).OrderByDescending(p => p.Key).First().Value;
            var right = areas.Where(p => p.Value.center.x > head.bounds.center.x + 70).OrderByDescending(p => p.Key).First().Value;
            return new[] { left, right };
        }

        private static Vector2 EarLobe(RectInt ear) => new Vector2(ear.center.x, ear.yMin + ear.height * 0.18f);

        private static List<Vector2[]> TraceOutlines(PartImage image, RectInt rect)
        {
            const int step = 4;
            int width = (rect.width + step - 1) / step, height = (rect.height + step - 1) / step;
            var cells = new bool[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    for (int sy = 0; sy < step; sy++)
                        for (int sx = 0; sx < step; sx++)
                        {
                            int px = rect.x + x * step + sx, py = rect.y + y * step + sy;
                            if (px < rect.xMax && py < rect.yMax && image.alpha[py * image.width + px] >= 16) cells[x, y] = true;
                        }
            var edges = new Dictionary<Vector2Int, List<Vector2Int>>();
            void Edge(int x0, int y0, int x1, int y1)
            {
                var a = new Vector2Int(x0, y0);
                if (!edges.TryGetValue(a, out var list)) edges.Add(a, list = new List<Vector2Int>());
                list.Add(new Vector2Int(x1, y1));
            }
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && cells[x, y];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (!cells[x, y]) continue;
                    if (!Solid(x, y - 1)) Edge(x, y, x + 1, y);
                    if (!Solid(x + 1, y)) Edge(x + 1, y, x + 1, y + 1);
                    if (!Solid(x, y + 1)) Edge(x + 1, y + 1, x, y + 1);
                    if (!Solid(x - 1, y)) Edge(x, y + 1, x, y);
                }
            var result = new List<Vector2[]>();
            while (edges.Count > 0)
            {
                Vector2Int start = edges.First().Key, current = start;
                var loop = new List<Vector2>();
                int safety = 0;
                do
                {
                    loop.Add(new Vector2(Mathf.Min(rect.xMax, rect.x + current.x * step), Mathf.Min(rect.yMax, rect.y + current.y * step)));
                    if (!edges.TryGetValue(current, out var list)) break;
                    Vector2Int next = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);
                    if (list.Count == 0) edges.Remove(current);
                    current = next;
                } while (current != start && ++safety < 100000);
                if (loop.Count < 3) continue;
                float area = 0;
                for (int i = 0; i < loop.Count; i++)
                {
                    Vector2 a = loop[i], b = loop[(i + 1) % loop.Count];
                    area += a.x * b.y - b.x * a.y;
                }
                if (area <= 64) continue;
                Vector2[] simplified = Simplify(loop);
                if (simplified.Length >= 3) result.Add(simplified);
            }
            if (result.Count == 0)
                result.Add(new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) });
            return result;
        }

        private static Vector2[] Simplify(List<Vector2> points)
        {
            int split = 1;
            for (int i = 2; i < points.Count; i++)
                if ((points[i] - points[0]).sqrMagnitude > (points[split] - points[0]).sqrMagnitude) split = i;
            points.Add(points[0]);
            var keep = new bool[points.Count];
            keep[0] = keep[split] = keep[points.Count - 1] = true;
            void Reduce(int first, int last)
            {
                Vector2 a = points[first], delta = points[last] - a;
                float max = 4; int index = -1;
                for (int i = first + 1; i < last; i++)
                {
                    float t = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(points[i] - a, delta) / delta.sqrMagnitude) : 0;
                    float distance = (points[i] - a - delta * t).sqrMagnitude;
                    if (distance > max) { max = distance; index = i; }
                }
                if (index < 0) return;
                keep[index] = true; Reduce(first, index); Reduce(index, last);
            }
            Reduce(0, split); Reduce(split, points.Count - 1);
            return points.Where((p, i) => i < points.Count - 1 && keep[i]).ToArray();
        }

        private static Vector2 Rotate(Vector2 p, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
        }

        private static RectInt Union(RectInt a, RectInt b) => new RectInt(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax) - Mathf.Min(a.xMin, b.xMin), Mathf.Max(a.yMax, b.yMax) - Mathf.Min(a.yMin, b.yMin));
    }
}
