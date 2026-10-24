using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream
{
    // Every playable scene owns its saved overlay. Only the destination name
    // and background colour travel through the independently editable blank scene.
    public sealed class SceneFadeTransition : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private CanvasGroup cover;
        [SerializeField] private Image coverImage;
        [InspectorName("淡出至空白时间（秒）"), Min(0)]
        [SerializeField] private float fadeOutSeconds = 0.7f;
        [InspectorName("从空白淡入时间（秒）"), Min(0)]
        [SerializeField] private float fadeInSeconds = 0.7f;

        private static SceneFadeTransition current;
        private static string pendingDestination, arrivalScene;
        private static Color? pendingBackground;
        private bool arriving;
        public static bool InputBlocked { get; private set; }
        public static Color? PendingBackground => pendingBackground;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            current = null;
            pendingDestination = arrivalScene = null;
            pendingBackground = null;
            InputBlocked = false;
        }

        private void Awake()
        {
            current = this;
            arriving = arrivalScene == gameObject.scene.name;
            if (!arriving)
            {
                pendingDestination = arrivalScene = null;
                pendingBackground = null;
            }
            coverImage.color = arriving && pendingBackground.HasValue ? pendingBackground.Value : sceneCamera.backgroundColor;
            cover.gameObject.SetActive(true);
            SetAlpha(arriving ? 1 : 0);
            InputBlocked = arriving;
            if (arriving)
                arrivalScene = null;
        }

        private IEnumerator Start()
        {
            if (!arriving)
                yield break;
            yield return Fade(1, 0, fadeInSeconds);
            pendingBackground = null;
            InputBlocked = false;
        }

        public static bool Load(string destination, bool alreadyCovered = false, string blankScene = "Transition_Blank")
        {
            if (InputBlocked)
                return false;
            if (current == null || string.IsNullOrWhiteSpace(destination) ||
                !Application.CanStreamedLevelBeLoaded(destination) || !Application.CanStreamedLevelBeLoaded(blankScene))
            {
                Debug.LogError("Scene fade requires a saved overlay and enabled destination/blank scenes: " + destination);
                return false;
            }
            InputBlocked = true;
            current.StartCoroutine(current.Leave(destination, alreadyCovered, blankScene));
            return true;
        }

        private IEnumerator Leave(string destination, bool alreadyCovered, string blankScene)
        {
            coverImage.color = sceneCamera.backgroundColor;
            if (alreadyCovered)
                SetAlpha(1);
            else
                yield return Fade(cover.alpha, 1, fadeOutSeconds);
            pendingDestination = destination;
            pendingBackground = coverImage.color;
            yield return SceneManager.LoadSceneAsync(blankScene, LoadSceneMode.Single);
        }

        public static string TakeDestination(string fallback)
        {
            string destination = string.IsNullOrWhiteSpace(pendingDestination) ? fallback : pendingDestination;
            pendingDestination = null;
            return destination;
        }

        public static void PrepareArrival(string destination)
        {
            arrivalScene = destination;
            InputBlocked = true;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            SetAlpha(from);
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / seconds))));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            cover.alpha = alpha;
            cover.blocksRaycasts = alpha > 0;
        }

        private void OnDestroy()
        {
            if (current == this)
                current = null;
        }
    }
}
