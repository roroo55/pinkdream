using UnityEngine;

namespace PinkDream
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class GameplaySoundEffects : MonoBehaviour
    {
        private const string PrefabResource = "PinkDreamGameplaySoundEffects";
        [SerializeField] private AudioClip itemDrop;
        [SerializeField] private AudioClip levelSuccess;
        [SerializeField] private AudioClip itemPickup;
        [InspectorName("物品掉落音量"), SerializeField, Range(0, 1)] private float dropVolume = .8f;
        [InspectorName("关卡成功音量"), SerializeField, Range(0, 1)] private float successVolume = .9f;
        [InspectorName("物品拾取音量"), SerializeField, Range(0, 1)] private float pickupVolume = .8f;
        private static GameplaySoundEffects current;
        private AudioSource source;
        private int lastDropFrame = -1, lastSuccessFrame = -1;
        private int lastPickupFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => GetOrCreate();

        private static GameplaySoundEffects GetOrCreate()
        {
            if (!Application.isPlaying) return null;
            if (current != null) return current;
            var existing = FindObjectOfType<GameplaySoundEffects>();
            if (existing != null) { existing.Initialize(); return current; }
            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab == null)
            {
                Debug.LogError("Missing gameplay sound effects prefab: " + PrefabResource);
                return null;
            }
            return Instantiate(prefab).GetComponent<GameplaySoundEffects>();
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (current != null && current != this) { Destroy(gameObject); return; }
            current = this; source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
            transform.SetParent(null); DontDestroyOnLoad(gameObject);
            // The supplied importers do not preload. Load before the first gesture,
            // so an event never has to wait for its first audio decode.
            if (itemDrop != null) itemDrop.LoadAudioData();
            if (levelSuccess != null) levelSuccess.LoadAudioData();
            if (itemPickup != null) itemPickup.LoadAudioData();
        }

        public static void PlayItemPickup()
        {
            var audio = GetOrCreate();
            if (audio == null || audio.itemPickup == null || audio.lastPickupFrame == Time.frameCount) return;
            audio.lastPickupFrame = Time.frameCount;
            audio.source.PlayOneShot(audio.itemPickup, audio.pickupVolume);
        }

        public static void PlayItemDrop()
        {
            var audio = GetOrCreate();
            if (audio == null || audio.itemDrop == null || audio.lastDropFrame == Time.frameCount) return;
            // Releasing two hands in one gesture produces one immediate cue.
            audio.lastDropFrame = Time.frameCount;
            audio.source.PlayOneShot(audio.itemDrop, audio.dropVolume);
        }

        public static void PlayLevelSuccess()
        {
            var audio = GetOrCreate();
            if (audio == null || audio.levelSuccess == null || audio.lastSuccessFrame == Time.frameCount) return;
            audio.lastSuccessFrame = Time.frameCount;
            audio.source.PlayOneShot(audio.levelSuccess, audio.successVolume);
        }

        private void OnDestroy() { if (current == this) current = null; }
    }
}
