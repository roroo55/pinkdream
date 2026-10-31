using UnityEngine;

namespace PinkDream
{
    public sealed class BirthdayRuleCard : MonoBehaviour
    {
        [SerializeField] private Transform magnifier;
        [SerializeField] private Collider2D[] magnifierColliders;
        [SerializeField] private Collider2D cardZone;
        [SerializeField] private GameObject rules, covers;
        [SerializeField] private Camera lensCamera;
        [SerializeField] private WorldMagnifier worldLens;
        [SerializeField, Min(1)] private float magnification = 1.6f;
        [SerializeField] private float lensRadius = .6f;
        private Vector2 grabOffset;
        public bool IsDragging { get; private set; }
        public bool Revealed { get; private set; }
        private void Awake() { lensCamera.enabled = false; rules.SetActive(true); covers.SetActive(true); }
        private void Start() { RefreshLens(); }
        public bool BeginDrag(Vector2 pointer)
        {
            foreach (var collider in magnifierColliders) if (collider.OverlapPoint(pointer))
            { IsDragging = true; grabOffset = (Vector2)magnifier.position - pointer; return true; }
            return false;
        }
        public void MoveDrag(Vector2 pointer)
        {
            if (!IsDragging) return;
            Vector2 p = pointer + grabOffset; magnifier.position = new Vector3(p.x, p.y, magnifier.position.z); Physics2D.SyncTransforms();
            if (!Revealed && cardZone.OverlapPoint(magnifier.position)) Revealed = true;
            RefreshLens();
        }
        public void EndDrag() { IsDragging = false; }
        public void HideForCelebration() { EndDrag(); magnifier.gameObject.SetActive(false); gameObject.SetActive(false); }
        private void RefreshLens()
        {
            worldLens.RenderView(lensRadius, magnification);
        }
    }
}
