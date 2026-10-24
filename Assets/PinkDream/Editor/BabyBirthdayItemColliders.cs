using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PinkDream.Editor
{
    public static class BabyBirthdayItemColliders
    {
        private const int CircleSegments = 32;

        // Bake in edit mode so both picking and grasping use the visible geometry.
        public static PolygonCollider2D Fit(Transform root, string itemName)
        {
            var paths = new List<Vector2[]>();
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>())
            {
                string name = renderer.name;
                if (itemName == "剪刀" && name == "指环轮廓")
                {
                    // The cream-coloured finger holes are empty space.
                    for (int i = 0; i < CircleSegments; i++)
                    {
                        float a = i * 2f * Mathf.PI / CircleSegments;
                        float b = (i + 1) * 2f * Mathf.PI / CircleSegments;
                        const float inner = 0.5f * (0.44f - 0.21f) / 0.44f;
                        AddPath(paths, root, renderer.transform, new[]
                        {
                            Polar(a, 0.5f), Polar(b, 0.5f), Polar(b, inner), Polar(a, inner)
                        });
                    }
                    continue;
                }

                bool silhouette = itemName == "剪刀"
                    ? name == "左剪刀刃轮廓" || name == "右剪刀刃轮廓" || name == "转轴"
                    : itemName == "鼠标" ? name == "鼠标轮廓"
                    : itemName == "画笔" ? name == "笔杆轮廓" || name == "金属箍" || name == "笔毛" || name == "笔尖颜料"
                    : itemName == "红包" ? name == "红包轮廓"
                    : itemName == "尤克里里" && (name == "琴身下部轮廓" || name == "琴身上部轮廓" || name == "琴颈" || name == "琴头");
                if (!silhouette)
                    continue;

                if (renderer.sprite.name == "Circle")
                    AddPath(paths, root, renderer.transform, Circle(0.5f));
                else
                    AddPath(paths, root, renderer.transform, new[]
                    {
                        new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f)
                    });
            }

            // The mouse cable is also visible, but its surrounding space is not solid.
            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>())
            {
                if (line.name != "鼠标线")
                    continue;
                for (int i = 0; i < line.positionCount; i++)
                {
                    Vector2 point = line.GetPosition(i);
                    float radius = line.startWidth * 0.5f;
                    Vector2[] cap = Circle(radius);
                    for (int j = 0; j < cap.Length; j++)
                        cap[j] += point;
                    AddPath(paths, root, line.transform, cap);
                    if (i == 0)
                        continue;
                    Vector2 previous = line.GetPosition(i - 1);
                    Vector2 delta = point - previous;
                    Vector2 normal = new Vector2(-delta.y, delta.x).normalized * radius;
                    AddPath(paths, root, line.transform, new[]
                    {
                        previous - normal, point - normal, point + normal, previous + normal
                    });
                }
            }

            if (paths.Count == 0)
                throw new System.InvalidOperationException("No visible item silhouette found: " + itemName);
            foreach (Collider2D old in root.GetComponents<Collider2D>())
                Object.DestroyImmediate(old);
            var collider = root.gameObject.AddComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            collider.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
                collider.SetPath(i, paths[i]);
            return collider;
        }

        public static void ApplyToExistingScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level02_FirstBirthday.unity");
            var level = Object.FindObjectOfType<BabyBirthdayLevel>();
            var data = new SerializedObject(level);
            SerializedProperty items = data.FindProperty("items");
            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                var root = (Transform)item.FindPropertyRelative("root").objectReferenceValue;
                string name = item.FindPropertyRelative("itemName").stringValue;
                item.FindPropertyRelative("collider").objectReferenceValue = Fit(root, name);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved five first-birthday item colliders fitted to visible geometry. No Play mode or tests executed.");
        }

        private static Vector2 Polar(float angle, float radius) =>
            new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        private static Vector2[] Circle(float radius)
        {
            var points = new Vector2[CircleSegments];
            for (int i = 0; i < points.Length; i++)
                points[i] = Polar(i * 2f * Mathf.PI / points.Length, radius);
            return points;
        }

        private static void AddPath(List<Vector2[]> paths, Transform root, Transform shape, Vector2[] points)
        {
            for (int i = 0; i < points.Length; i++)
                points[i] = root.InverseTransformPoint(shape.TransformPoint(points[i]));
            paths.Add(points);
        }
    }
}
