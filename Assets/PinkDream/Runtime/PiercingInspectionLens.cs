using UnityEngine;

namespace PinkDream
{
    // Guide copies are lens-only; the optical view magnifies the whole world.
    public sealed class PiercingInspectionLens : MonoBehaviour
    {
        [SerializeField] private AdolescencePiercingLevel level;
        [SerializeField] private Transform magnifier;
        [SerializeField] private Collider2D[] magnifierColliders;
        [SerializeField] private Camera lensCamera;
        [SerializeField] private WorldMagnifier worldLens;
        [SerializeField] private GameObject captureRoot;
        [SerializeField] private SpriteRenderer sourceHead, copyHead;
        [SerializeField] private Transform[] sourceEars, copyEars, earSpots;
        [SerializeField] private GameObject[] captureSuggestions;
        [SerializeField, Min(1)] private float magnification = 3.2f;
        [SerializeField] private float lensRadius = .62f;
        private Vector2 grabOffset;
        public bool IsDragging { get; private set; }
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
            captureRoot.SetActive(true); copyHead.enabled = false;
            for (int i = 0; i < sourceEars.Length; i++)
            {
                CopyWorld(sourceEars[i], copyEars[i]);
                bool suggest = !sourceEars[i].Find("Guide").gameObject.activeSelf && !sourceEars[i].Find("Mark").gameObject.activeSelf && !HasEarring(sourceEars[i]);
                foreach (Transform child in copyEars[i]) child.gameObject.SetActive(child.gameObject == captureSuggestions[i] && suggest);
            }
            worldLens.RenderView(lensRadius, magnification);
        }
        private static bool HasEarring(Transform root)
        {
            for (int i = 0; i < 3; i++) if (root.Find("Style " + i).gameObject.activeSelf) return true;
            return false;
        }
        private static void CopyWorld(Transform source, Transform target)
        { target.SetPositionAndRotation(source.position, source.rotation); target.localScale = source.lossyScale; }
    }
}
