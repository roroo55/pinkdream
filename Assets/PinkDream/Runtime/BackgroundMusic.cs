using UnityEngine;

namespace PinkDream
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class BackgroundMusic : MonoBehaviour
    {
        private const string PrefabResource = "PinkDreamBackgroundMusic";
        private static BackgroundMusic current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (current != null)
                return;

            var existing = FindObjectOfType<BackgroundMusic>();
            if (existing != null)
            {
                existing.BeginPlayback();
                return;
            }

            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab == null)
            {
                Debug.LogError("Missing background music prefab: " + PrefabResource);
                return;
            }
            Instantiate(prefab);
        }

        private void Awake() => BeginPlayback();

        private void BeginPlayback()
        {
            var source = GetComponent<AudioSource>();
            if (current != null && current != this)
            {
                source.Stop();
                Destroy(gameObject);
                return;
            }

            current = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            source.loop = true;
            source.spatialBlend = 0;
            if (source.clip != null && !source.isPlaying)
                source.Play();
        }

        private void OnDestroy()
        {
            if (current == this)
                current = null;
        }
    }
}
