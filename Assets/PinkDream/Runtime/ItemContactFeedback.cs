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

        private Vector3 restScale;
        private Sequence pulse;
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
            if (item == null || !item.TryGetComponent(out ItemContactFeedback feedback)) return;
            feedback.touching = feedback.TouchesHand();
            feedback.PlayPulse();
        }

        public void PlayPulse()
        {
            Initialize();
            if (!initialized || !isActiveAndEnabled || pulse != null && pulse.IsActive()) return;
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
            if (pulse != null) pulse.Kill(false);
            pulse = null;
            if (initialized && visualRoot != null) visualRoot.localScale = restScale;
            touching = false;
        }
    }
}
