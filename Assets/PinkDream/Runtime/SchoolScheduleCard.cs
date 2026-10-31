using UnityEngine;

namespace PinkDream
{
    public sealed class SchoolScheduleCard : MonoBehaviour
    {
        [System.Serializable] public sealed class RequirementIcon
        {
            public ChildhoodPackingLevel.ItemKind item;
            public GameObject visual;
        }
        [SerializeField] private Transform magnifier;
        [SerializeField] private Collider2D[] magnifierColliders;
        [SerializeField] private Collider2D cardZone;
        [SerializeField] private Camera lensCamera;
        [SerializeField] private WorldMagnifier worldLens;
        [SerializeField] private GameObject requirements;
        [SerializeField] private GameObject hiddenMarks;
        [SerializeField] private GameObject rainyWeather, sunnyWeather;
        [SerializeField] private RequirementIcon[] icons;
        [SerializeField, Min(1)] private float magnification = 1.8f;
        [SerializeField] private float lensRadius = 0.63f;
        private Vector2 grabOffset;
        public bool IsDragging { get; private set; }
        public bool Revealed { get; private set; }

        private void Awake() { lensCamera.enabled = false; }
        private void Start() { RefreshLens(); }
        public void SetPlan(bool rainy, ChildhoodPackingLevel.ItemKind side, ChildhoodPackingLevel.ItemKind back, ChildhoodPackingLevel.ItemKind main)
        {
            Revealed = false;
            rainyWeather.SetActive(rainy); sunnyWeather.SetActive(!rainy);
            foreach (RequirementIcon icon in icons) icon.visual.SetActive(icon.item == side || icon.item == back || icon.item == main);
            // The camera layers keep the actual requirements visible only in the glass.
            requirements.SetActive(true); hiddenMarks.SetActive(true);
        }
        public bool BeginDrag(Vector2 pointer)
        {
            foreach (Collider2D collider in magnifierColliders)
                if (collider.OverlapPoint(pointer))
                {
                    IsDragging = true; grabOffset = (Vector2)magnifier.position - pointer; return true;
                }
            return false;
        }
        public void MoveDrag(Vector2 pointer)
        {
            if (!IsDragging) return;
            Vector2 position = pointer + grabOffset;
            magnifier.position = new Vector3(position.x, position.y, magnifier.position.z);
            Physics2D.SyncTransforms();
            if (!Revealed && cardZone.OverlapPoint(magnifier.position))
            {
                Revealed = true;
            }
            RefreshLens();
        }
        public void EndDrag() { IsDragging = false; }
        private void RefreshLens()
        {
            worldLens.RenderView(lensRadius, magnification);
        }
    }
}
