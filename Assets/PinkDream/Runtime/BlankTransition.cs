using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinkDream
{
    public sealed class BlankTransition : MonoBehaviour
    {
        [InspectorName("空镜停留时间（秒）"), Min(0)]
        [SerializeField] private float blankHoldSeconds = 0.8f;
        [SerializeField] private string nextScene = "Level02_FirstBirthday";

        private IEnumerator Start()
        {
            string destination = SceneFadeTransition.TakeDestination(nextScene);
            if (SceneFadeTransition.PendingBackground.HasValue)
                Camera.main.backgroundColor = SceneFadeTransition.PendingBackground.Value;
            if (blankHoldSeconds > 0)
                yield return new WaitForSecondsRealtime(blankHoldSeconds);
            if (!string.IsNullOrWhiteSpace(destination))
            {
                if (Application.CanStreamedLevelBeLoaded(destination))
                {
                    SceneFadeTransition.PrepareArrival(destination);
                    yield return SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
                }
                else
                    Debug.LogError("The next scene is missing from Build Settings: " + destination, this);
            }
        }
    }
}
