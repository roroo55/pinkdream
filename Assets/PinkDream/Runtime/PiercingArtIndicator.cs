using UnityEngine;

namespace PinkDream
{
    // Read the existing feedback signal; only the replacement artwork is written.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PiercingArtIndicator : MonoBehaviour
    {
        public enum Display { Machine, Progress, Mirror }
        [SerializeField] private SpriteRenderer signal, artwork;
        [SerializeField] private Display display;
        [SerializeField] private Sprite idle, ready, warning, success;
        private static readonly Color Neutral = new Color32(86, 93, 99, 255);
        private static readonly Color Gold = new Color32(244, 205, 117, 255);
        private static readonly Color Red = new Color32(240, 85, 98, 255);
        private static readonly Color Green = new Color32(148, 228, 185, 255);
        private static readonly Color Cyan = new Color32(113, 221, 222, 255);

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();
        private void Update() => Refresh();

        public void Refresh()
        {
            if (signal == null || artwork == null) return;
            Color input = signal.color;
            if (display == Display.Mirror)
            {
                float weight = Mathf.Clamp01(Distance(input, Neutral) / Distance(Cyan, Neutral));
                Color tint = Color.Lerp(Color.white, Cyan, weight);
                tint.a = input.a;
                artwork.color = tint;
                return;
            }
            Sprite selected = idle;
            if (display == Display.Progress)
                selected = Distance(input, Neutral) < .015f ? idle : success;
            else
            {
                float nearest = Distance(input, Neutral);
                Select(input, Gold, ready, ref nearest, ref selected);
                Select(input, Red, warning, ref nearest, ref selected);
                Select(input, Green, success, ref nearest, ref selected);
            }
            if (selected != null) artwork.sprite = selected;
            artwork.color = new Color(1, 1, 1, input.a);
        }

        private static float Distance(Color a, Color b)
            => (new Vector3(a.r, a.g, a.b) - new Vector3(b.r, b.g, b.b)).sqrMagnitude;
        private static void Select(Color input, Color color, Sprite sprite, ref float nearest, ref Sprite selected)
        {
            float distance = Distance(input, color);
            if (sprite == null || distance >= nearest) return;
            nearest = distance;
            selected = sprite;
        }
    }
}
