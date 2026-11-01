using UnityEngine;
using UnityEngine.UI;

namespace PinkDream
{
    // Each state is an actual saved Text object. Switching never changes its text.
    public sealed class SceneTextGroup : MonoBehaviour
    {
        [System.Serializable] public sealed class View
        {
            public string key;
            public Text text;
        }
        [SerializeField] private View[] views;
        [SerializeField] private string initialKey;
        private string currentKey;
        public View[] Views => views;
        public string InitialKey => initialKey;
        private void Awake() { Show(initialKey); }
        public void Show(string key)
        {
            if (currentKey == key && Application.isPlaying) return;
            bool found = false;
            foreach (View view in views)
            {
                bool visible = view.key == key; found |= visible;
                if (view.text != null && view.text.gameObject.activeSelf != visible) view.text.gameObject.SetActive(visible);
            }
            currentKey = key;
            if (!found) Debug.LogError("Missing authored text state on " + name + ": " + key, this);
        }
        public void Hide()
        {
            foreach (View view in views) if (view.text != null) view.text.gameObject.SetActive(false);
            currentKey = null;
        }
    }
}
