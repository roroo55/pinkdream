using DG.Tweening;
using UnityEngine;

namespace PinkDream
{
    [DisallowMultipleComponent]
    public sealed class ItemContactFeedback : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Collider2D[] itemColliders;
        [SerializeField] private Collider2D[] handColliders;
        [SerializeField, Range(1f, 1.2f)] private float peakScale = 1.1f;
        [SerializeField, Range(0.9f, 1f)] private float reboundScale = 0.97f;
        [SerializeField, Min(0.05f)] private float duration = 0.3f;
        [InspectorName("放下后的轻微下落距离"), SerializeField, Min(0)] private float dropDistance = .14f;
        [InspectorName("轻微下落时间（秒）"), SerializeField, Min(.01f)] private float dropSeconds = .18f;

        private Vector3 restScale;
        private Sequence pulse;
        private Tween drop;
        private bool initialized, touching;

        private void Awake()
        {
            Initialize();
            touching = TouchesHand();
        }

        private void Initialize()
        {
            if (initialized || visualRoot == null) return;
            restScale = visualRoot.localScale;
            initialized = true;
        }

        private void Update()
        {
            // Starting a drag does not animate anything. Only a new hand contact
            // during the gesture, or an explicit successful pickup, can pulse.
            if (!Application.isFocused || SceneFadeTransition.InputBlocked) return;
            if (Input.GetMouseButtonDown(0))
            {
                touching = TouchesHand();
                return;
            }
            if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) return;
            bool contact = TouchesHand();
            if (contact && !touching) PlayPulse();
            touching = contact;
        }

        private bool TouchesHand()
        {
            if (itemColliders == null || handColliders == null) return false;
            foreach (Collider2D item in itemColliders)
            {
                if (item == null || !item.enabled || !item.gameObject.activeInHierarchy) continue;
                foreach (Collider2D hand in handColliders)
                {
                    if (hand == null || !hand.enabled || !hand.gameObject.activeInHierarchy) continue;
                    ColliderDistance2D contact = hand.Distance(item);
                    if (contact.isValid && contact.distance <= 0) return true;
                }
            }
            return false;
        }

        public static void Play(Transform item)
        {
            if (item == null) return;
            GameplaySoundEffects.PlayItemPickup();
            if (!item.TryGetComponent(out ItemContactFeedback feedback)) return;
            feedback.StopDrop();
            feedback.touching = feedback.TouchesHand();
            feedback.PlayPulse();
        }

        public static void CancelDrop(Transform item)
        {
            if (item != null && item.TryGetComponent(out ItemContactFeedback feedback)) feedback.StopDrop();
        }

        public static void PlayDrop(Transform item, bool moveDown = true)
        {
            GameplaySoundEffects.PlayItemDrop();
            if (item == null || !item.TryGetComponent(out ItemContactFeedback feedback)) return;
            feedback.touching = feedback.TouchesHand();
            // Drops must still respond when a pickup pulse is running, or when
            // contact detection is disabled during a rejection animation.
            feedback.StartPulse(true, true);
            if (moveDown) feedback.StartDrop();
        }

        private void StartDrop()
        {
            StopDrop();
            if (!isActiveAndEnabled || dropDistance <= 0) return;
            float distance = dropDistance;
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float lowest = float.PositiveInfinity;
                foreach (Collider2D item in itemColliders ?? System.Array.Empty<Collider2D>())
                    if (item != null && item.enabled && item.gameObject.activeInHierarchy)
                        lowest = Mathf.Min(lowest, item.bounds.min.y);
                float depth = Mathf.Abs(transform.position.z - camera.transform.position.z);
                float bottom = camera.ViewportToWorldPoint(new Vector3(0, .025f, depth)).y;
                if (!float.IsPositiveInfinity(lowest)) distance = Mathf.Min(distance, Mathf.Max(0, lowest - bottom));
            }
            if (distance <= 0) return;
            // Move the loose item and its unchanged colliders together. This is a
            // short settling motion, with no continuing simulation or effect on bones.
            drop = transform.DOMoveY(transform.position.y - distance, dropSeconds)
                .SetEase(Ease.OutSine).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnUpdate(Physics2D.SyncTransforms).OnKill(() => drop = null);
        }

        private void StopDrop()
        {
            if (drop != null) drop.Kill(false);
            drop = null;
        }

        public void PlayPulse() => StartPulse(false, false);

        private void StartPulse(bool restart, bool allowDisabled)
        {
            Initialize();
            if (!initialized || !gameObject.activeInHierarchy || !allowDisabled && !isActiveAndEnabled) return;
            if (pulse != null && pulse.IsActive())
            {
                if (!restart) return;
                pulse.Kill(false);
            }
            visualRoot.localScale = restScale;
            pulse = DOTween.Sequence();
            pulse.SetAutoKill(true).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            pulse.Append(visualRoot.DOScale(Vector3.Scale(restScale, new Vector3(peakScale, peakScale, 1)), duration * 0.24f).SetEase(Ease.OutQuad));
            pulse.Append(visualRoot.DOScale(Vector3.Scale(restScale, new Vector3(reboundScale, reboundScale, 1)), duration * 0.26f).SetEase(Ease.InOutSine));
            pulse.Append(visualRoot.DOScale(restScale, duration * 0.5f).SetEase(Ease.OutBack));
            pulse.OnComplete(() => { if (visualRoot != null) visualRoot.localScale = restScale; });
            pulse.OnKill(() => pulse = null);
        }

        private void OnDisable()
        {
            StopDrop();
            if (pulse != null) pulse.Kill(false);
            pulse = null;
            if (initialized && visualRoot != null) visualRoot.localScale = restScale;
            touching = false;
        }
    }
}
