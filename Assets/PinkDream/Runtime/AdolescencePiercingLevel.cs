using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class AdolescencePiercingLevel : MonoBehaviour
    {
        [System.Serializable]
        public sealed class EarTarget
        {
            public CircleCollider2D lobe;
            public GameObject earring;
        }

        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll girl;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private Transform piercingTool;
        [SerializeField] private PolygonCollider2D toolCollider;
        [InspectorName("穿耳器前端接触点")]
        [SerializeField] private CircleCollider2D toolTip;
        [SerializeField] private EarTarget[] ears;
        [SerializeField] private Text feedbackText;
        [InspectorName("完成后的停留时间（秒）"), Min(0)]
        [SerializeField] private float completionHoldSeconds = 2f;
        [SerializeField] private string nextScene = "Level05_ComingOfAge";

        private readonly CircleCollider2D[] palms = new CircleCollider2D[2];
        private int heldHand = -1;
        private bool dragging;
        public bool IsComplete { get; private set; }

        private void Awake()
        {
            girl.ReadMouseInput = false;
            palms[0] = leftHandAnchor.parent.GetComponent<CircleCollider2D>();
            palms[1] = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
        }

        private void Update()
        {
            if (IsComplete)
                return;
            Vector2 screen = Input.mousePosition;
            if (!Application.isFocused || !interactionCamera.pixelRect.Contains(screen))
            {
                EndGesture(false);
                return;
            }
            Vector2 pointer = girl.ScreenToWorld(screen);
            if (Input.GetMouseButtonDown(0))
                BeginGesture(pointer);
            if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)))
            {
                if (girl.Model.GrabWorld != pointer)
                {
                    girl.MoveControlledDrag(pointer);
                    TryTakeTool();
                }
            }
            if (!Input.GetMouseButton(0))
                EndGesture(Input.GetMouseButtonUp(0));
        }

        private void BeginGesture(Vector2 pointer)
        {
            EndGesture(false);
            int part = heldHand >= 0 && toolCollider.OverlapPoint(pointer)
                ? (heldHand == 0 ? 3 : 5) : girl.Pick(pointer);
            if (part < 0)
                foreach (EarTarget ear in ears)
                    if (ear.lobe.OverlapPoint(pointer))
                    {
                        part = 1;
                        break;
                    }
            if (part < 0)
                return;
            dragging = true;
            girl.BeginControlledDrag(part, pointer);
        }

        private void TryTakeTool()
        {
            if (heldHand >= 0)
                return;
            for (int hand = 0; hand < palms.Length; hand++)
            {
                ColliderDistance2D contact = palms[hand].Distance(toolCollider);
                if (!contact.isValid || contact.distance > 0f)
                    continue;
                heldHand = hand;
                // Keep the exact contact pose, with no attraction or grip-point snap.
                piercingTool.SetParent(hand == 0 ? leftHandAnchor : rightHandAnchor, true);
                foreach (SpriteRenderer renderer in piercingTool.GetComponentsInChildren<SpriteRenderer>())
                    renderer.sortingOrder = 340 + renderer.sortingOrder % 10;
                foreach (Canvas label in piercingTool.GetComponentsInChildren<Canvas>())
                    label.enabled = false;
                feedbackText.text = "让穿耳器的银色前端碰到任一侧耳垂，再松手。";
                return;
            }
        }

        private void EndGesture(bool confirm)
        {
            bool wasDragging = dragging;
            dragging = false;
            girl.EndControlledDrag();
            if (!confirm || !wasDragging || heldHand < 0 || IsComplete)
                return;
            foreach (EarTarget ear in ears)
            {
                ColliderDistance2D contact = toolTip.Distance(ear.lobe);
                if (!contact.isValid || contact.distance > 0f)
                    continue;
                IsComplete = true;
                // The symbolic result is pre-authored on the head, without injury effects.
                ear.earring.SetActive(true);
                feedbackText.text = "这是我自己的选择。";
                StartCoroutine(ContinueToNextLevel());
                return;
            }
        }

        private IEnumerator ContinueToNextLevel()
        {
            if (completionHoldSeconds > 0)
                yield return new WaitForSecondsRealtime(completionHoldSeconds);
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
                yield return SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && girl != null)
                EndGesture(false);
        }

        private void OnDisable()
        {
            if (girl != null)
                EndGesture(false);
        }
    }
}
