using System.Collections.Generic;
using UnityEngine;

namespace PinkDream
{
    public interface IInteractionHintSource
    {
        bool CanOfferHints { get; }
        void CollectHintTargets(List<Transform> targets);
    }

    [DisallowMultipleComponent]
    public sealed class PlayerInteractionAssist : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour hintSource;
        [InspectorName("无操作后显示光晕的时间（秒）"), Min(.1f)]
        [SerializeField] private float idleSeconds = 5f;
        [SerializeField] private SpriteRenderer[] halos;
        [SerializeField] private Color tint = new Color(1f, .86f, .55f, 1f);
        [SerializeField, Range(0, 1)] private float opacity = .48f;
        [SerializeField, Min(.1f)] private float breathSeconds = 1.6f;
        [SerializeField, Min(0)] private float padding = .28f;
        [SerializeField, Min(.01f)] private float fadeSeconds = .2f;

        private readonly List<Transform> targets = new List<Transform>();
        private readonly Dictionary<Collider2D, Collider2D[]> released = new Dictionary<Collider2D, Collider2D[]>();
        private readonly List<Collider2D> separated = new List<Collider2D>();
        private IInteractionHintSource source;
        private float lastActivity, weight;
        private Vector3 lastPointer;
        private bool wasInteracting;

        private void Awake() { source = hintSource as IInteractionHintSource; MarkActivity(); Hide(); }

        public void MarkActivity() { lastActivity = Time.unscaledTime; }

        // A stationary held mouse and movement over empty space do not postpone a hint.
        public void ObserveGesture(bool interacting)
        {
            Vector3 pointer = Input.mousePosition;
            if (interacting && (!wasInteracting || Input.GetMouseButtonDown(0) ||
                Input.GetMouseButtonUp(0) || (pointer - lastPointer).sqrMagnitude > .01f)) MarkActivity();
            wasInteracting = interacting;
            lastPointer = pointer;
        }

        // Release keeps the exact world pose. Contact must break before automatic pickup resumes.
        public void SuppressPickup(Collider2D item, params Collider2D[] hands)
        {
            if (item != null) released[item] = hands;
        }

        public bool CanPickup(Collider2D item) => item != null && !released.ContainsKey(item);

        private void LateUpdate()
        {
            separated.Clear();
            foreach (var entry in released)
            {
                bool touching = false;
                if (entry.Key != null && entry.Key.enabled && entry.Key.gameObject.activeInHierarchy)
                    foreach (Collider2D hand in entry.Value)
                    {
                        if (hand == null || !hand.enabled || !hand.gameObject.activeInHierarchy) continue;
                        ColliderDistance2D contact = hand.Distance(entry.Key);
                        if (contact.isValid && contact.distance <= .015f) { touching = true; break; }
                    }
                if (!touching) separated.Add(entry.Key);
            }
            foreach (Collider2D item in separated) released.Remove(item);

            if (!Application.isFocused || SceneFadeTransition.InputBlocked || source == null || !source.CanOfferHints)
            { MarkActivity(); weight = 0; Hide(); return; }
            bool idle = Time.unscaledTime - lastActivity >= idleSeconds;
            weight = Mathf.MoveTowards(weight, idle ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
            targets.Clear();
            if (weight > 0) source.CollectHintTargets(targets);
            float breath = .5f - .5f * Mathf.Cos((Time.unscaledTime - lastActivity - idleSeconds) * Mathf.PI * 2 / breathSeconds);
            for (int i = 0; i < halos.Length; i++)
            {
                SpriteRenderer halo = halos[i];
                if (halo == null) continue;
                if (i >= targets.Count || targets[i] == null || !targets[i].gameObject.activeInHierarchy ||
                    !TryBounds(targets[i], out Bounds bounds, out int order, out int layer))
                { halo.enabled = false; continue; }
                halo.enabled = true;
                halo.transform.position = new Vector3(bounds.center.x, bounds.center.y, targets[i].position.z + .01f);
                halo.transform.rotation = Quaternion.identity;
                Vector2 size = (Vector2)bounds.size + Vector2.one * padding * 2;
                float expansion = 1.05f + breath * .13f;
                halo.transform.localScale = new Vector3(size.x / halo.sprite.bounds.size.x * expansion,
                    size.y / halo.sprite.bounds.size.y * expansion, 1);
                halo.sortingLayerID = layer; halo.sortingOrder = order - 1;
                Color color = tint; color.a = opacity * weight * (.55f + .45f * breath); halo.color = color;
            }
        }

        private static bool TryBounds(Transform target, out Bounds bounds, out int order, out int layer)
        {
            bool found = false; bounds = default; order = int.MaxValue; layer = 0;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.gameObject.layer == 2 ||
                    !(renderer is SpriteRenderer || renderer is MeshRenderer)) continue;
                if (renderer is SpriteRenderer sprite && sprite.color.a <= .01f) continue;
                if (!found) { bounds = renderer.bounds; layer = renderer.sortingLayerID; found = true; }
                else bounds.Encapsulate(renderer.bounds);
                order = Mathf.Min(order, renderer.sortingOrder);
            }
            if (found) return true;
            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null || !collider.enabled) return false;
            bounds = collider.bounds; order = 250; return true;
        }

        private void Hide()
        {
            if (halos == null) return;
            foreach (SpriteRenderer halo in halos) if (halo != null) halo.enabled = false;
        }
        private void OnDisable() { weight = 0; wasInteracting = false; Hide(); }
    }
}
