using System.Collections.Generic;
using UnityEngine;

namespace PinkDream
{
    [DisallowMultipleComponent]
    public sealed class FrozenRagdoll : MonoBehaviour
    {
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Shader geometryShader;

        [SerializeField] private List<Transform> partViews = new List<Transform>();
        private readonly Dictionary<Collider2D, int> pickers = new Dictionary<Collider2D, int>();
        private readonly List<Object> ownedAssets = new List<Object>();
        [SerializeField] private List<SpriteRenderer> bodyColors = new List<SpriteRenderer>();
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool showPrototypeOverlay = true;
        [Header("人物体型（成人保持默认值）")]
        [SerializeField, Min(0.1f)] private float bodyScale = 1;
        [SerializeField, Min(0.5f)] private float headScale = 1;
        [SerializeField] private bool seatedRestPose;
        private readonly List<Color> initialPartColors = new List<Color>();
#if UNITY_EDITOR
        private Sprite circle, square;
        private Material material;
#endif
        private Font uiFont;
        private int hoverPart = -1;
        private static readonly Color Ink = new Color32(91, 54, 73, 255);
        private static readonly Color Skin = new Color32(255, 219, 196, 255);
        private static readonly Color Rose = new Color32(215, 129, 161, 255);
        private static readonly Color Lilac = new Color32(169, 161, 204, 255);

        public PoseRagdollModel Model { get; private set; }
        public bool ReadMouseInput { get; set; } = true;

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = Camera.main;
            if (interactionCamera == null)
            {
                Debug.LogError("FrozenRagdoll needs an orthographic camera.", this);
                enabled = false;
                return;
            }

            Model = CreateModel();
            if (!BindSceneGeometry())
            {
                enabled = false;
                return;
            }
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
            ownedAssets.Add(uiFont);
            RefreshPose();
        }

        private bool BindSceneGeometry()
        {
            if (visualRoot == null || partViews.Count != Model.Parts.Count || bodyColors.Count != Model.Parts.Count)
            {
                Debug.LogError("FrozenRagdoll needs the character already saved in the scene.", this);
                return false;
            }
            for (int i = 0; i < partViews.Count; i++)
            {
                Transform view = partViews[i];
                if (view == null || bodyColors[i] == null)
                {
                    Debug.LogError("FrozenRagdoll has a missing scene part reference.", this);
                    return false;
                }
                Model.Parts[i].Position = view.position;
                Model.Parts[i].Angle = view.eulerAngles.z * Mathf.Deg2Rad;
                initialPartColors.Add(bodyColors[i].color);
                foreach (Collider2D collider in view.GetComponents<Collider2D>())
                    pickers.Add(collider, i);
            }
            Model.AlignHeadWithTorso();
            return true;
        }

        private PoseRagdollModel CreateModel()
            => new PoseRagdollModel(transform.position, bodyScale, headScale, seatedRestPose);

        private void Update()
        {
            if (!ReadMouseInput)
                return;

            Vector3 screen = Input.mousePosition;
            bool inside = interactionCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
            // Stop immediately when the pointer leaves Game view; re-entry must
            // start a new drag, including when release happened outside the app.
            if (!inside || !Application.isFocused)
            {
                Model.EndDrag();
                SetHighlight(-1);
                return;
            }

            Vector2 world = ScreenToWorld(screen);
            int hit = Pick(world);
            if (Input.GetMouseButtonDown(0) && hit >= 0)
                Model.BeginDrag(hit, world);

            if (Model.IsDragging)
            {
                if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
                {
                    if (Model.MoveDrag(world))
                        RefreshPose();
                }
                if (!Input.GetMouseButton(0))
                    Model.EndDrag();
            }
            SetHighlight(Model.IsDragging ? Model.GrabbedPart : -1);
        }

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            Ray ray = interactionCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
            return plane.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : Vector2.zero;
        }

        public int Pick(Vector2 world)
        {
            // Explicit overlap queries do not depend on a physics update or on
            // Physics2D.autoSyncTransforms; idle poses never need simulation.
            int selected = -1, frontOrder = -1;
            foreach (KeyValuePair<Collider2D, int> item in pickers)
            {
                int order = item.Value == 1 ? 110 : item.Value * 10;
                if (item.Key.OverlapPoint(world) && order > frontOrder)
                {
                    selected = item.Value;
                    frontOrder = order;
                }
            }
            return selected;
        }

        public void RefreshPose()
        {
            for (int i = 0; i < partViews.Count; i++)
            {
                PoseRagdollModel.Part part = Model.Parts[i];
                partViews[i].SetPositionAndRotation(new Vector3(part.Position.x, part.Position.y, transform.position.z),
                    Quaternion.Euler(0, 0, part.Angle * Mathf.Rad2Deg));
            }
            Physics2D.SyncTransforms();
        }

        // Level interactions use the same grab point and pose solver as the
        // original mouse input; they only coordinate which character is held.
        public void BeginControlledDrag(int part, Vector2 pointer)
        {
            Model.BeginDrag(part, pointer);
            SetHighlight(part);
        }

        public void MoveControlledDrag(Vector2 pointer)
        {
            if (Model.MoveDrag(pointer))
                RefreshPose();
        }

        public void EndControlledDrag()
        {
            Model?.EndDrag();
            SetHighlight(-1);
        }

        public Vector2 HandPosition(int forearm)
        {
            PoseRagdollModel.Part part = Model.Parts[forearm];
            return part.WorldPoint(new Vector2(0, -part.Length * 0.5f));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Model?.EndDrag();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Model?.EndDrag();
        }

        private void OnDisable()
        {
            Model?.EndDrag();
        }

#if UNITY_EDITOR
        public void PoseHandInEditor(int hand, Vector2 destination)
        {
            Model = CreateModel();
            for (int i = 0; i < partViews.Count; i++)
            {
                Model.Parts[i].Position = partViews[i].position;
                Model.Parts[i].Angle = partViews[i].eulerAngles.z * Mathf.Deg2Rad;
            }
            Model.AlignHeadWithTorso();
            PoseRagdollModel.Part forearm = Model.Parts[hand];
            Model.BeginDrag(hand, forearm.WorldPoint(new Vector2(0, -forearm.Length * 0.5f)));
            Model.MoveDrag(destination);
            Model.EndDrag();
            RefreshPose();
            foreach (Transform part in partViews)
                UnityEditor.EditorUtility.SetDirty(part);
        }

        // Authoring only: never called by Awake or included in a player build.
        public void BakeSceneGeometry()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("Bake character geometry in Edit mode only.");
            if (transform.Find("Geometric ragdoll") != null)
                throw new System.InvalidOperationException("The scene already contains the character geometry.");
            Model = CreateModel();
            partViews.Clear();
            bodyColors.Clear();
            pickers.Clear();
            const string folder = "Assets/PinkDream/Geometry";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/PinkDream", "Geometry");
            visualRoot = new GameObject("Geometric ragdoll").transform;
            visualRoot.SetParent(transform, false);
            circle = MakeSprite(true);
            square = MakeSprite(false);
            Shader shader = geometryShader != null ? geometryShader : Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            float scale = Mathf.Max(0.1f, bodyScale);
            float faceScale = scale * Mathf.Max(0.5f, headScale);
            string materialPath = folder + "/RagdollGeometry.mat";
            material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Ragdoll geometry unlit" };
                UnityEditor.AssetDatabase.CreateAsset(material, materialPath);
            }

            for (int i = 0; i < Model.Parts.Count; i++)
            {
                PoseRagdollModel.Part part = Model.Parts[i];
                Transform view = new GameObject(part.Name).transform;
                view.SetParent(visualRoot, false);
                partViews.Add(view);
                Color fill = PartColor(i);
                int order = i * 10;
                SpriteRenderer primary;
                if (part.IsCircle)
                {
                    Disc(view, "Head outline", Vector2.zero, part.Width + 0.05f * scale, Ink, 110);
                    primary = Disc(view, "Head", Vector2.zero, part.Width, fill, 111);
                    Vector2 neckCenter = new Vector2(0, -part.Width * 0.5f - 0.1f * scale);
                    Capsule(view, "Neck outline", neckCenter, 0.19f * scale, 0.4f * scale, Ink, 5);
                    Capsule(view, "Neck", neckCenter, 0.14f * scale, 0.37f * scale, Skin, 6);
                    Disc(view, "Left eye", new Vector2(-0.17f, 0.05f) * faceScale, 0.07f * faceScale, Ink, 113);
                    Disc(view, "Right eye", new Vector2(0.17f, 0.05f) * faceScale, 0.07f * faceScale, Ink, 113);
                    Shape(view, "Mouth", square, new Vector2(0, -0.17f) * faceScale, new Vector2(0.13f, 0.035f) * faceScale, Ink, 113);
                    var collider = view.gameObject.AddComponent<CircleCollider2D>();
                    collider.radius = part.Width * 0.5f + 0.03f * scale;
                    pickers.Add(collider, i);
                    var neck = view.gameObject.AddComponent<CapsuleCollider2D>();
                    neck.offset = neckCenter;
                    neck.size = new Vector2(0.2f, 0.4f) * scale;
                    pickers.Add(neck, i);
                }
                else
                {
                    float height = part.Length + (i == 0 ? 0.24f * scale : part.Width);
                    Capsule(view, "Outline", Vector2.zero, part.Width + 0.05f * scale, height + 0.05f * scale, Ink, order);
                    primary = Capsule(view, "Body", Vector2.zero, part.Width, height, fill, order + 1);
                    var collider = view.gameObject.AddComponent<CapsuleCollider2D>();
                    collider.size = new Vector2(part.Width + 0.04f * scale, height + 0.04f * scale);
                    pickers.Add(collider, i);
                }
                bodyColors.Add(primary);

                if (i == 3 || i == 5)
                {
                    Vector2 end = new Vector2(0, -part.Length * 0.5f);
                    Disc(view, "Hand outline", end, 0.36f * scale, Ink, order + 3);
                    Disc(view, "Hand", end, 0.31f * scale, Skin, order + 4);
                    var hand = view.gameObject.AddComponent<CircleCollider2D>();
                    hand.offset = end;
                    hand.radius = 0.18f * scale;
                    pickers.Add(hand, i);
                }
                if (i == 7 || i == 9)
                {
                    Vector2 end = new Vector2((i == 7 ? -0.1f : 0.1f) * scale, -part.Length * 0.5f);
                    Shape(view, "Foot outline", circle, end, new Vector2(0.55f, 0.30f) * scale, Ink, order + 3);
                    Shape(view, "Foot", circle, end, new Vector2(0.50f, 0.25f) * scale, Lilac, order + 4);
                    var foot = view.gameObject.AddComponent<CapsuleCollider2D>();
                    foot.direction = CapsuleDirection2D.Horizontal;
                    foot.offset = end;
                    foot.size = new Vector2(0.56f, 0.31f) * scale;
                    pickers.Add(foot, i);
                }
            }

            foreach (PoseRagdollModel.Joint joint in Model.Joints)
            {
                if (joint.B == 1)
                    continue;
                Disc(partViews[joint.B], "Joint outline", joint.AnchorB, 0.18f * scale, Ink, 115);
                Disc(partViews[joint.B], "Joint pin", joint.AnchorB, 0.10f * scale, new Color32(255, 242, 227, 255), 116);
            }
            RefreshPose();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        private static Color PartColor(int index) => index == 1 || index == 3 || index == 5 ? Skin : index >= 6 ? Lilac : Rose;

        private void SetHighlight(int part)
        {
            if (hoverPart == part)
                return;
            if (hoverPart >= 0)
                TintPart(hoverPart, initialPartColors.Count > hoverPart ? initialPartColors[hoverPart] : PartColor(hoverPart));
            hoverPart = part;
            if (hoverPart >= 0)
                TintPart(hoverPart, Color.Lerp(initialPartColors.Count > hoverPart ? initialPartColors[hoverPart] : PartColor(hoverPart), Color.white, 0.32f));
        }

        private void TintPart(int index, Color color)
        {
            SpriteRenderer primary = bodyColors[index];
            primary.color = color;
            // Capsule end caps share the same logical fill as the rectangle.
            foreach (SpriteRenderer child in primary.GetComponentsInChildren<SpriteRenderer>())
                child.color = color;
        }

#if UNITY_EDITOR
        private SpriteRenderer Capsule(Transform parent, string label, Vector2 center, float width, float height, Color color, int order)
        {
            float straight = Mathf.Max(0.001f, height - width);
            SpriteRenderer body = Shape(parent, label, square, center, new Vector2(width, straight), color, order);
            // Keep the caps in the fill group while preserving their world sizes.
            SpriteRenderer top = Disc(parent, label + " top", center + Vector2.up * straight * 0.5f, width, color, order);
            SpriteRenderer bottom = Disc(parent, label + " bottom", center - Vector2.up * straight * 0.5f, width, color, order);
            top.transform.SetParent(body.transform, true);
            bottom.transform.SetParent(body.transform, true);
            return body;
        }

        private SpriteRenderer Disc(Transform parent, string label, Vector2 center, float diameter, Color color, int order)
            => Shape(parent, label, circle, center, Vector2.one * diameter, color, order);

        private SpriteRenderer Shape(Transform parent, string label, Sprite sprite, Vector2 center, Vector2 size, Color color, int order)
        {
            Transform shape = new GameObject(label).transform;
            shape.SetParent(parent, false);
            shape.localPosition = center;
            shape.localScale = new Vector3(size.x, size.y, 1);
            SpriteRenderer renderer = shape.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private Sprite MakeSprite(bool round)
        {
            string path = "Assets/PinkDream/Geometry/" + (round ? "Circle.asset" : "Square.asset");
            foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite savedSprite)
                    return savedSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = round ? "Procedural circle" : "Procedural square", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float alpha = round ? Mathf.Clamp01((size * 0.5f - 1f) - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f)) : 1f;
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            UnityEditor.AssetDatabase.CreateAsset(texture, path);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
            sprite.name = round ? "Circle" : "Square";
            UnityEditor.AssetDatabase.AddObjectToAsset(sprite, texture);
            UnityEditor.AssetDatabase.ImportAsset(path);
            return sprite;
        }
#endif

        private void OnGUI()
        {
            if (!showPrototypeOverlay || Model == null || uiFont == null)
                return;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1100f, Screen.height / 740f), 0.55f, 1.8f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            var small = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 14, normal = { textColor = Ink } };
            var title = new GUIStyle(small) { fontSize = 30, fontStyle = FontStyle.Bold };
            var hint = new GUIStyle(small) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(36, 25, w - 72, 28), "01  /  RAGDOLL PROTOTYPE", small);
            GUI.Label(new Rect(34, 55, w - 68, 50), "Pink Dream", title);
            small.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(w - 290, 32, 250, 30), Model.IsDragging ? "●  DRAGGING" : "●  FROZEN", small);
            GUI.Label(new Rect(24, h - 83, w - 48, 32), "按住头、身体或四肢拖拽，摆出你想要的姿势", hint);
            hint.fontSize = 14;
            GUI.Label(new Rect(24, h - 53, w - 48, 30), "鼠标停住就停住 · 松手保持姿势", hint);
            GUI.matrix = previous;
        }

        private void OnDestroy()
        {
            foreach (Object asset in ownedAssets)
                if (asset != null)
                    Destroy(asset);
        }
    }
}
