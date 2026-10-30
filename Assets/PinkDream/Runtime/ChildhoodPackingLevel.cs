using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class ChildhoodPackingLevel : MonoBehaviour
    {
        [System.Serializable]
        public sealed class PackingItem
        {
            public string itemName;
            public Transform root;
            public PolygonCollider2D collider;
        }

        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll girl;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private PackingItem[] items;
        [InspectorName("书包内部放置区域")]
        [SerializeField] private BoxCollider2D bagInterior;
        [SerializeField] private Transform packedItemsRoot;
        [SerializeField] private Text progressText;
        [SerializeField] private Text feedbackText;
        [InspectorName("全部收好后的停留时间（秒）"), Min(0)]
        [SerializeField] private float completionHoldSeconds = 2f;
        [SerializeField] private string nextScene = "Level04_AdolescencePiercing";

        private readonly int[] heldItems = { -1, -1 };
        private readonly CircleCollider2D[] palms = new CircleCollider2D[2];
        private bool[] packed;
        private bool dragging;
        private int packedCount;
        public bool IsComplete { get; private set; }

        private void Awake()
        {
            girl.ReadMouseInput = false;
            palms[0] = leftHandAnchor.parent.GetComponent<CircleCollider2D>();
            palms[1] = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
            packed = new bool[items.Length];
            UpdateProgress();
        }

        private void Update()
        {
            if (IsComplete || SceneFadeTransition.InputBlocked)
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
                    TryTakeItems();
                }
            }
            if (!Input.GetMouseButton(0))
                EndGesture(Input.GetMouseButtonUp(0));
        }

        private void BeginGesture(Vector2 pointer)
        {
            EndGesture(false);
            int part = girl.Pick(pointer);
            // A held object can also serve as the handle for dragging its hand.
            if (part < 0)
                for (int hand = 0; hand < heldItems.Length; hand++)
                    if (heldItems[hand] >= 0 && items[heldItems[hand]].collider.OverlapPoint(pointer))
                    {
                        part = hand == 0 ? 3 : 5;
                        break;
                    }
            if (part < 0)
                return;
            dragging = true;
            girl.BeginControlledDrag(part, pointer);
        }

        private void TryTakeItems()
        {
            for (int hand = 0; hand < heldItems.Length; hand++)
            {
                if (heldItems[hand] >= 0)
                    continue;
                for (int i = 0; i < items.Length; i++)
                {
                    if (packed[i] || heldItems[0] == i || heldItems[1] == i)
                        continue;
                    ColliderDistance2D contact = palms[hand].Distance(items[i].collider);
                    if (!contact.isValid || contact.distance > 0f)
                        continue;
                    heldItems[hand] = i;
                    // Parenting preserves the actual touch position; no snap or attraction.
                    items[i].root.SetParent(hand == 0 ? leftHandAnchor : rightHandAnchor, true);
                    SetItemOrder(items[i], 300);
                    foreach (Canvas label in items[i].root.GetComponentsInChildren<Canvas>())
                        label.enabled = false;
                    feedbackText.text = "Picked up " + items[i].itemName + "   Put it in the bag then release";
                    break;
                }
            }
        }

        private bool IsInsideBag(PolygonCollider2D item)
        {
            // Check the actual silhouette in the bag's local space, including rotation.
            for (int path = 0; path < item.pathCount; path++)
                foreach (Vector2 point in item.GetPath(path))
                    if (!bagInterior.OverlapPoint(item.transform.TransformPoint(point + item.offset)))
                        return false;
            return true;
        }

        private void EndGesture(bool putAway)
        {
            bool wasDragging = dragging;
            dragging = false;
            girl.EndControlledDrag();
            if (!putAway || !wasDragging || IsComplete)
                return;
            for (int hand = 0; hand < heldItems.Length; hand++)
            {
                int index = heldItems[hand];
                if (index < 0 || !IsInsideBag(items[index].collider))
                    continue;
                PackingItem item = items[index];
                item.root.SetParent(packedItemsRoot, true);
                item.collider.enabled = false;
                SetItemOrder(item, 115); // Behind the existing front of the bag.
                heldItems[hand] = -1;
                packed[index] = true;
                packedCount++;
                feedbackText.text = item.itemName + " packed   Keep going";
                UpdateProgress();
            }
            if (packedCount != items.Length)
                return;
            IsComplete = true;
            feedbackText.text = "All packed Moving to the next stage";
            StartCoroutine(ContinueToNextLevel());
        }

        private void UpdateProgress() => progressText.text = "Packed " + EnglishText.Number(packedCount) + " of " + EnglishText.Number(items.Length);

        private static void SetItemOrder(PackingItem item, int order)
        {
            foreach (SpriteRenderer renderer in item.root.GetComponentsInChildren<SpriteRenderer>())
                renderer.sortingOrder = order + renderer.sortingOrder % 10;
        }

        private IEnumerator ContinueToNextLevel()
        {
            if (completionHoldSeconds > 0)
                yield return new WaitForSecondsRealtime(completionHoldSeconds);
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
                SceneFadeTransition.Load(nextScene);
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
