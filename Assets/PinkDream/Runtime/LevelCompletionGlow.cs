using DG.Tweening;
using UnityEngine;

namespace PinkDream
{
    [DisallowMultipleComponent]
    public sealed class LevelCompletionGlow : MonoBehaviour
    {
        [SerializeField] private Renderer[] targets;
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private LineRenderer ring;
        [SerializeField] private Color tint = new Color(1f, .84f, .58f, 1f);
        [SerializeField, Min(.4f)] private float duration = 1.45f;
        [SerializeField, Range(0, 1)] private float haloOpacity = .6f;
        [SerializeField, Min(0)] private float padding = .45f;
        private Sequence pulse;
        private bool played;
        private Vector3 haloScale;

        public void Play()
        {
            if (played || !isActiveAndEnabled || halo == null || ring == null) return;
            bool found = false;
            Bounds bounds = default;
            int backOrder = int.MaxValue;
            foreach (Renderer target in targets)
            {
                if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = target.bounds; found = true; }
                else bounds.Encapsulate(target.bounds);
                backOrder = Mathf.Min(backOrder, target.sortingOrder);
            }
            if (!found) return;
            played = true;
            // Position only this independent visual root. Target bones and props are read-only.
            transform.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
            Vector2 size = (Vector2)bounds.size + Vector2.one * padding * 2;
            haloScale = new Vector3(size.x * 1.3f / halo.sprite.bounds.size.x, size.y * 1.3f / halo.sprite.bounds.size.y, 1);
            halo.transform.localScale = haloScale * .92f;
            halo.sortingOrder = backOrder - 2; ring.sortingOrder = backOrder - 1;
            halo.sortingLayerID = ring.sortingLayerID = targets[0].sortingLayerID;
            ring.positionCount = 96;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * size.x * .5f, Mathf.Sin(angle) * size.y * .5f, 0));
            }
            ring.transform.localScale = Vector3.one * .88f;
            SetOpacity(halo, 0); SetRingOpacity(0);
            pulse = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            pulse.Insert(0, DOTween.To(() => halo.color.a, a => SetOpacity(halo, a), haloOpacity, duration * .16f).SetEase(Ease.OutSine));
            pulse.Insert(duration * .28f, DOTween.To(() => halo.color.a, a => SetOpacity(halo, a), 0, duration * .72f).SetEase(Ease.InOutSine));
            pulse.Insert(0, halo.transform.DOScale(haloScale * 1.12f, duration).SetEase(Ease.OutSine));
            pulse.Insert(0, DOTween.To(() => ring.startColor.a, SetRingOpacity, .82f, duration * .13f).SetEase(Ease.OutSine));
            pulse.Insert(duration * .24f, DOTween.To(() => ring.startColor.a, SetRingOpacity, 0, duration * .76f).SetEase(Ease.InOutSine));
            pulse.Insert(0, ring.transform.DOScale(Vector3.one * 1.15f, duration).SetEase(Ease.OutSine));
            pulse.OnComplete(() => { SetOpacity(halo, 0); SetRingOpacity(0); });
            pulse.OnKill(() => pulse = null);
        }
        private void SetOpacity(SpriteRenderer renderer, float alpha)
        { Color color = tint; color.a = alpha; renderer.color = color; }
        private void SetRingOpacity(float alpha)
        { Color color = tint; color.a = alpha; ring.startColor = ring.endColor = color; }
        private void OnDisable()
        {
            pulse?.Kill(); pulse = null;
            if (halo != null) SetOpacity(halo, 0);
            if (ring != null) SetRingOpacity(0);
        }
    }
}
