using DG.Tweening;
using UnityEngine;

namespace PinkDream
{
    public sealed class BabyPairingEffects : MonoBehaviour
    {
        [System.Serializable]
        public sealed class ItemVisual { public Transform visualRoot; public SpriteRenderer glow; }
        [SerializeField] private ItemVisual[] items;
        [SerializeField] private Transform[] themedEffects;
        [SerializeField] private Renderer headVisual;
        private Sequence match;

        public void AppendMismatch(Sequence sequence, int first, int second, float seconds)
        {
            Transform a = items[first].visualRoot, b = items[second].visualRoot;
            Vector3 ra = a.localEulerAngles, rb = b.localEulerAngles;
            sequence.Append(a.DOLocalRotate(ra + Vector3.forward * 9, seconds * 0.25f).SetEase(Ease.InOutSine).SetLoops(4, LoopType.Yoyo));
            sequence.Join(b.DOLocalRotate(rb - Vector3.forward * 9, seconds * 0.25f).SetEase(Ease.InOutSine).SetLoops(4, LoopType.Yoyo));
            sequence.AppendCallback(() => { if (a != null) a.localEulerAngles = ra; if (b != null) b.localEulerAngles = rb; });
        }
        public void PlayMatch(int first, int second, BabyBirthdayLevel.PairKind kind)
        {
            if (match != null) match.Kill(false);
            match = DOTween.Sequence().SetAutoKill(true).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            foreach (int index in new[] { first, second })
            {
                SpriteRenderer glow = items[index].glow;
                glow.gameObject.SetActive(true); SetAlpha(glow, 0);
                match.Insert(0, Alpha(glow, 0.9f, 0.18f).SetEase(Ease.OutSine));
                match.Insert(0.18f, Alpha(glow, 0.55f, 0.4f).SetEase(Ease.InOutSine));
                match.Insert(0.58f, Alpha(glow, 0.9f, 0.4f).SetEase(Ease.InOutSine));
                match.Insert(0, items[index].visualRoot.DOScale(new Vector3(1.075f, 1.075f, 1), 0.18f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo));
            }
            Transform effect = themedEffects[(int)kind];
            Vector3 effectPosition = (items[first].visualRoot.position + items[second].visualRoot.position) * 0.5f + Vector3.up * 0.6f;
            if (headVisual != null) effectPosition.y = Mathf.Max(effectPosition.y, headVisual.bounds.max.y + 0.25f);
            effect.position = effectPosition;
            effect.localScale = Vector3.one * 0.05f; effect.gameObject.SetActive(true);
            foreach (SpriteRenderer sprite in effect.GetComponentsInChildren<SpriteRenderer>(true))
            {
                SetAlpha(sprite, 1);
                match.Insert(0.7f, Alpha(sprite, 0, 0.5f));
            }
            match.Insert(0, effect.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack));
            match.Insert(0, effect.DOMoveY(effect.position.y + 0.5f, 1.2f).SetEase(Ease.OutSine));
            match.OnComplete(() => { if (effect != null) effect.gameObject.SetActive(false); });
            match.OnKill(() => match = null);
        }
        private static Tween Alpha(SpriteRenderer sprite, float target, float seconds)
            => DOTween.To(() => sprite.color.a, value => SetAlpha(sprite, value), target, seconds);
        private static void SetAlpha(SpriteRenderer sprite, float value)
        {
            if (sprite == null) return;
            Color color = sprite.color; color.a = value; sprite.color = color;
        }
        private void OnDisable() { if (match != null) match.Kill(false); match = null; }
    }
}
