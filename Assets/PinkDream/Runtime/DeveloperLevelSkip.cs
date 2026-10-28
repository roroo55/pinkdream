#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinkDream
{
    // Developer-only input, independent of the authored characters and scene contents.
    public sealed class DeveloperLevelSkip : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindObjectOfType<DeveloperLevelSkip>() != null)
                return;
            var shortcuts = new GameObject("Developer shortcuts · T 跳过当前关卡");
            DontDestroyOnLoad(shortcuts);
            shortcuts.AddComponent<DeveloperLevelSkip>();
        }

        private void Update()
        {
            if (Application.isFocused && Input.GetKeyDown(KeyCode.T))
                TrySkipCurrentLevel();
        }

        public static bool TrySkipCurrentLevel()
        {
            if (SceneFadeTransition.InputBlocked)
                return false;

            string destination;
            switch (SceneManager.GetActiveScene().name)
            {
                case "Start": destination = "Level01_BeforeBirth"; break;
                case "Level01_BeforeBirth": destination = "Level02_FirstBirthday"; break;
                case "Level02_FirstBirthday": destination = "Level03_ChildhoodPacking"; break;
                case "Level03_ChildhoodPacking": destination = "Level04_AdolescencePiercing"; break;
                case "Level04_AdolescencePiercing": destination = "Level05_ComingOfAge"; break;
                case "Level05_ComingOfAge": destination = "Start"; break;
                default: return false;
            }

            if (!SceneFadeTransition.Load(destination))
                return false;
            foreach (FrozenRagdoll character in FindObjectsOfType<FrozenRagdoll>())
                character.EndControlledDrag();
            return true;
        }
    }
}
#endif
