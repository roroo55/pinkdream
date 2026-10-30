using UnityEngine;

namespace PinkDream
{
    // Capture visual copies only; never modify character bones or colliders.
    public sealed class PiercingInspectionLens : MonoBehaviour
    {
        [SerializeField] private AdolescencePiercingLevel level;
        [SerializeField] private Transform magnifier;
        [SerializeField] private Collider2D[] magnifierColliders;
        [SerializeField] private Camera lensCamera;
        [SerializeField] private GameObject captureRoot;
        [SerializeField] private SpriteRenderer sourceHead, copyHead;
        [SerializeField] private Transform[] sourceEars, copyEars, earSpots;
        [SerializeField] private GameObject[] captureSuggestions;
        [SerializeField, Min(1)] private float magnification = 3.2f;
        [SerializeField] private float lensRadius = .62f;
        private Vector2 grabOffset;
        public bool IsDragging { get; private set; }
        private bool rendering;
        private void Awake() { lensCamera.enabled = false; captureRoot.SetActive(true); }
        private void Start() { RefreshView(); }
        public bool BeginDrag(Vector2 pointer)
        {
            foreach (Collider2D collider in magnifierColliders) if (collider.OverlapPoint(pointer))
            { IsDragging = true; grabOffset = (Vector2)magnifier.position - pointer; return true; }
            return false;
        }
        public void MoveDrag(Vector2 pointer)
        {
            if (!IsDragging) return;
            Vector2 p = pointer + grabOffset; magnifier.position = new Vector3(p.x, p.y, magnifier.position.z);
            Physics2D.SyncTransforms(); RefreshViewAndInspect();
        }
        public void EndDrag() { IsDragging = false; }
        public void RefreshViewAndInspect()
        {
            for (int ear = 0; ear < earSpots.Length; ear++)
                if (Vector2.Distance(magnifier.position, earSpots[ear].position) <= lensRadius / magnification * .85f) level.RevealEar(ear);
            RefreshView();
        }
        public void RefreshView()
        {
            if (rendering || lensCamera == null || sourceHead == null) return;
            rendering = true;
            bool active = captureRoot.activeSelf; captureRoot.SetActive(true);
            CopyWorld(sourceHead.transform, copyHead.transform); copyHead.sprite = sourceHead.sprite; copyHead.color = sourceHead.color; copyHead.flipX = sourceHead.flipX; copyHead.flipY = sourceHead.flipY;
            for (int i = 0; i < sourceEars.Length; i++) { CopyWorld(sourceEars[i], copyEars[i]); CopyChildren(sourceEars[i], copyEars[i]); }
            for (int i = 0; i < captureSuggestions.Length; i++)
                if (!sourceEars[i].Find("Mark").gameObject.activeSelf && !HasEarring(sourceEars[i])) captureSuggestions[i].SetActive(true);
            if (!lensCamera.targetTexture.IsCreated()) lensCamera.targetTexture.Create();
            lensCamera.transform.position = new Vector3(magnifier.position.x, magnifier.position.y, -10);
            lensCamera.orthographicSize = lensRadius / magnification;
            lensCamera.Render(); captureRoot.SetActive(active); rendering = false;
        }
        private static bool HasEarring(Transform root)
        {
            for (int i = 0; i < 3; i++) if (root.Find("Style " + i).gameObject.activeSelf) return true;
            return false;
        }
        private static void CopyWorld(Transform source, Transform target)
        { target.SetPositionAndRotation(source.position, source.rotation); target.localScale = source.lossyScale; }
        private static void CopyChildren(Transform source, Transform target)
        {
            for (int i = 0; i < source.childCount; i++)
            {
                Transform a = source.GetChild(i), b = target.GetChild(i);
                b.localPosition = a.localPosition; b.localRotation = a.localRotation; b.localScale = a.localScale; b.gameObject.SetActive(a.gameObject.activeSelf);
                if (a.TryGetComponent(out SpriteRenderer sr) && b.TryGetComponent(out SpriteRenderer cr)) { cr.color = sr.color; cr.sprite = sr.sprite; }
                CopyChildren(a, b);
            }
        }
    }
}
