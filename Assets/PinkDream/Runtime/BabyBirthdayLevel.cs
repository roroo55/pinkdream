using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class BabyBirthdayLevel : MonoBehaviour
    {
        [System.Serializable]
        public sealed class GraspItem
        {
            public string itemName;
            public Transform root;
            public Collider2D collider;
            public Transform gripPoint;
        }

        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll baby;
        [SerializeField] private GraspItem[] items;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private Text feedbackText;
        [InspectorName("抓到后的展示时间（秒）"), Min(0)]
        [SerializeField] private float completionHoldSeconds = 2f;
        [SerializeField] private string nextScene = "Level03_ChildhoodPacking";

        private int draggingItem = -1;
        private bool draggingBaby;
        private Vector2 itemGrabOffset;
        private CircleCollider2D leftPalm;
        private CircleCollider2D rightPalm;
        public bool IsComplete { get; private set; }

        private void Awake()
        {
            baby.ReadMouseInput = false;
            leftPalm = leftHandAnchor.parent.GetComponent<CircleCollider2D>();
            rightPalm = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
        }

        private void Update()
        {
            if (IsComplete || SceneFadeTransition.InputBlocked)
                return;
            Vector2 screen = Input.mousePosition;
            if (!Application.isFocused || !interactionCamera.pixelRect.Contains(screen))
            {
                EndGesture();
                return;
            }
            Vector2 pointer = baby.ScreenToWorld(screen);
            if (Input.GetMouseButtonDown(0))
                BeginGesture(pointer);
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
                MoveGesture(pointer);
            if (!Input.GetMouseButton(0))
                EndGesture();
        }

        private void BeginGesture(Vector2 pointer)
        {
            EndGesture();
            for (int i = items.Length - 1; i >= 0; i--)
            {
                if (!items[i].collider.OverlapPoint(pointer))
                    continue;
                draggingItem = i;
                itemGrabOffset = (Vector2)items[i].root.position - pointer;
                return;
            }
            int part = baby.Pick(pointer);
            if (part >= 0)
            {
                draggingBaby = true;
                baby.BeginControlledDrag(part, pointer);
            }
        }

        private void MoveGesture(Vector2 pointer)
        {
            if (IsComplete)
                return;
            if (draggingItem >= 0)
            {
                Transform item = items[draggingItem].root;
                Vector2 position = pointer + itemGrabOffset;
                if ((Vector2)item.position == position)
                    return;
                item.position = new Vector3(position.x, position.y, item.position.z);
                Physics2D.SyncTransforms();
            }
            else if (draggingBaby)
            {
                if (baby.Model.GrabWorld == pointer)
                    return;
                baby.MoveControlledDrag(pointer);
            }
            else
                return;
            TryGrasp();
        }

        private void TryGrasp()
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (draggingItem >= 0 && i != draggingItem)
                    continue;
                for (int hand = 3; hand <= 5; hand += 2)
                {
                    CircleCollider2D palm = hand == 3 ? leftPalm : rightPalm;
                    ColliderDistance2D contact = palm.Distance(items[i].collider);
                    if (!contact.isValid || contact.distance > 0f)
                        continue;
                    CompleteGrasp(i, hand);
                    return;
                }
            }
        }

        private void CompleteGrasp(int index, int hand)
        {
            IsComplete = true;
            EndGesture();
            GraspItem selected = items[index];
            // Keep the actual contact pose instead of snapping to a grip point.
            selected.root.SetParent(hand == 3 ? leftHandAnchor : rightHandAnchor, true);
            ItemContactFeedback.Play(selected.root);
            Physics2D.SyncTransforms();
            feedbackText.text = "Item caught Moving to the next stage";
            // Only a local display name is used. No preference, career or item
            // flag is persisted; every item advances through the same route.
            StartCoroutine(ContinueToNextLevel());
        }

        private IEnumerator ContinueToNextLevel()
        {
            if (completionHoldSeconds > 0)
                yield return new WaitForSecondsRealtime(completionHoldSeconds);
            if (Application.CanStreamedLevelBeLoaded(nextScene))
                SceneFadeTransition.Load(nextScene);
            else
                Debug.LogError("The next scene is missing from Build Settings: " + nextScene, this);
        }

        private void EndGesture()
        {
            draggingBaby = false;
            draggingItem = -1;
            baby.EndControlledDrag();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && baby != null)
                EndGesture();
        }

        private void OnDisable()
        {
            if (baby != null)
                EndGesture();
        }
    }
}
