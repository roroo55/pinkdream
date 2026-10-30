using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class BabyBirthdayLevel : MonoBehaviour
    {
        public enum PairKind { Painting, Building, Music, Medicine }
        [System.Serializable]
        public sealed class GraspItem
        {
            public string itemName;
            public Transform root;
            public Collider2D collider;
            public Transform gripPoint;
            public PairKind pair;
            public bool isMainItem = true;
        }
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll baby;
        [SerializeField] private GraspItem[] items;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Transform looseItemsRoot;
        [SerializeField] private BabyPairingEffects pairingEffects;
        [InspectorName("配对成功后的展示时间（秒）"), Min(0)]
        [SerializeField] private float completionHoldSeconds = 2f;
        [InspectorName("错误配对晃动时间（秒）"), Min(0.05f)]
        [SerializeField] private float mismatchShakeSeconds = 0.22f;
        [InspectorName("错误物品下落时间（秒）"), Min(0.05f)]
        [SerializeField] private float mismatchDropSeconds = 0.35f;
        [InspectorName("错误物品下落距离"), Min(0.1f)]
        [SerializeField] private float mismatchDropDistance = 0.7f;
        [SerializeField] private string nextScene = "Level03_ChildhoodPacking";

        private int draggingItem = -1, firstHand = -1;
        private bool draggingBaby, resolving;
        private Vector2 itemGrabOffset;
        private CircleCollider2D leftPalm, rightPalm;
        private readonly int[] held = { -1, -1 };
        private Sequence rejection;
        public bool IsComplete { get; private set; }

        private void Awake()
        {
            baby.ReadMouseInput = false;
            leftPalm = leftHandAnchor.parent.GetComponent<CircleCollider2D>();
            rightPalm = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
        }
        private void Update()
        {
            if (IsComplete || resolving || SceneFadeTransition.InputBlocked) return;
            Vector2 screen = Input.mousePosition;
            if (!Application.isFocused || !interactionCamera.pixelRect.Contains(screen)) { EndGesture(); return; }
            Vector2 pointer = baby.ScreenToWorld(screen);
            if (Input.GetMouseButtonDown(0)) BeginGesture(pointer);
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)) MoveGesture(pointer);
            if (!Input.GetMouseButton(0)) EndGesture();
        }
        private void BeginGesture(Vector2 pointer)
        {
            EndGesture();
            // A held object moves its actual hand; it never becomes a third slot.
            for (int hand = 0; hand < held.Length; hand++)
                if (held[hand] >= 0 && items[held[hand]].collider.OverlapPoint(pointer))
                {
                    draggingBaby = true;
                    baby.BeginControlledDrag(hand == 0 ? 3 : 5, pointer);
                    return;
                }
            for (int i = items.Length - 1; i >= 0; i--)
            {
                if (held[0] == i || held[1] == i || !items[i].collider.OverlapPoint(pointer)) continue;
                draggingItem = i;
                itemGrabOffset = (Vector2)items[i].root.position - pointer;
                return;
            }
            int part = baby.Pick(pointer);
            if (part >= 0) { draggingBaby = true; baby.BeginControlledDrag(part, pointer); }
        }
        private void MoveGesture(Vector2 pointer)
        {
            if (IsComplete || resolving) return;
            if (draggingItem >= 0)
            {
                Transform item = items[draggingItem].root;
                Vector2 position = pointer + itemGrabOffset;
                if ((Vector2)item.position == position) return;
                item.position = new Vector3(position.x, position.y, item.position.z);
                Physics2D.SyncTransforms();
            }
            else if (draggingBaby)
            {
                if (baby.Model.GrabWorld == pointer) return;
                baby.MoveControlledDrag(pointer);
            }
            else return;
            TryGrasp(pointer);
        }
        private void TryGrasp(Vector2 pointer)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (held[0] == i || held[1] == i || draggingItem >= 0 && i != draggingItem) continue;
                if (firstHand < 0 && !items[i].isMainItem) continue;
                for (int hand = 0; hand < held.Length; hand++)
                {
                    if (held[hand] >= 0) continue;
                    ColliderDistance2D contact = (hand == 0 ? leftPalm : rightPalm).Distance(items[i].collider);
                    if (!contact.isValid || contact.distance > 0) continue;
                    TakeItem(i, hand, pointer);
                    return;
                }
            }
        }
        private void TakeItem(int index, int hand, Vector2 pointer)
        {
            bool wasDraggingItem = draggingItem == index;
            held[hand] = index;
            items[index].root.SetParent(hand == 0 ? leftHandAnchor : rightHandAnchor, true);
            foreach (SpriteRenderer sprite in items[index].root.GetComponentsInChildren<SpriteRenderer>())
                sprite.sortingOrder = 300 + sprite.sortingOrder % 10;
            ItemContactFeedback.Play(items[index].root);
            Physics2D.SyncTransforms();
            if (firstHand < 0)
            {
                firstHand = hand;
                feedbackText.text = "Use the other hand to find a matching partner";
                if (wasDraggingItem)
                {
                    draggingItem = -1; draggingBaby = true;
                    baby.BeginControlledDrag(hand == 0 ? 3 : 5, pointer);
                }
                return;
            }
            EndGesture();
            SetContactFeedback(held[0], false); SetContactFeedback(held[1], false);
            if (items[held[0]].pair == items[held[1]].pair)
            {
                IsComplete = true;
                feedbackText.text = "A matching pair Moving to the next stage";
                pairingEffects.PlayMatch(held[0], held[1], items[index].pair);
                // This choice is local to the level and never changes the ending.
                StartCoroutine(ContinueToNextLevel());
            }
            else RejectPair();
        }
        private void RejectPair()
        {
            resolving = true;
            feedbackText.text = "Not a match Try another pair";
            int a = held[0], b = held[1];
            Vector3[] targets = DropTargets(a, b);
            items[a].collider.enabled = false; items[b].collider.enabled = false;
            rejection = DOTween.Sequence().SetAutoKill(true).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            pairingEffects.AppendMismatch(rejection, a, b, mismatchShakeSeconds);
            rejection.AppendCallback(() =>
            {
                items[a].root.SetParent(looseItemsRoot, true); items[b].root.SetParent(looseItemsRoot, true);
                held[0] = held[1] = -1; firstHand = -1;
            });
            rejection.Append(items[a].root.DOMove(targets[0], mismatchDropSeconds).SetEase(Ease.InQuad));
            rejection.Join(items[b].root.DOMove(targets[1], mismatchDropSeconds).SetEase(Ease.InQuad));
            rejection.Append(items[a].root.DOMoveY(targets[0].y + 0.055f, 0.07f).SetEase(Ease.OutQuad));
            rejection.Join(items[b].root.DOMoveY(targets[1].y + 0.055f, 0.07f).SetEase(Ease.OutQuad));
            rejection.Append(items[a].root.DOMoveY(targets[0].y, 0.07f).SetEase(Ease.InQuad));
            rejection.Join(items[b].root.DOMoveY(targets[1].y, 0.07f).SetEase(Ease.InQuad));
            rejection.OnComplete(() =>
            {
                items[a].collider.enabled = true; items[b].collider.enabled = true;
                SetContactFeedback(a, true); SetContactFeedback(b, true);
                resolving = false; Physics2D.SyncTransforms();
            });
            rejection.OnKill(() => rejection = null);
        }
        private Vector3[] DropTargets(int a, int b)
        {
            Bounds ba = items[a].collider.bounds, bb = items[b].collider.bounds;
            float depth = Mathf.Abs(interactionCamera.transform.position.z - items[a].root.position.z);
            Vector3 min = interactionCamera.ViewportToWorldPoint(new Vector3(0.055f, 0.1f, depth));
            Vector3 max = interactionCamera.ViewportToWorldPoint(new Vector3(0.945f, 0.9f, depth));
            bool aLeft = ba.center.x <= bb.center.x;
            Bounds left = aLeft ? ba : bb, right = aLeft ? bb : ba;
            float separation = left.extents.x + right.extents.x + 0.12f;
            float middle = (left.center.x + right.center.x) * 0.5f;
            float lx = Mathf.Clamp(middle - separation * 0.5f, min.x + left.extents.x, max.x - left.extents.x);
            float rx = Mathf.Clamp(lx + separation, min.x + right.extents.x, max.x - right.extents.x);
            lx = Mathf.Clamp(rx - separation, min.x + left.extents.x, max.x - left.extents.x);
            Vector3 Target(int index, Bounds bounds, float x)
            {
                float y = Mathf.Clamp(bounds.center.y - mismatchDropDistance, min.y + bounds.extents.y, max.y - bounds.extents.y);
                return items[index].root.position + new Vector3(x - bounds.center.x, y - bounds.center.y, 0);
            }
            return new[] { Target(a, ba, aLeft ? lx : rx), Target(b, bb, aLeft ? rx : lx) };
        }
        private void SetContactFeedback(int index, bool enabled)
        {
            if (items[index].root.TryGetComponent(out ItemContactFeedback effect)) effect.enabled = enabled;
        }
        private IEnumerator ContinueToNextLevel()
        {
            if (completionHoldSeconds > 0) yield return new WaitForSecondsRealtime(completionHoldSeconds);
            if (Application.CanStreamedLevelBeLoaded(nextScene)) SceneFadeTransition.Load(nextScene);
            else Debug.LogError("The next scene is missing from Build Settings: " + nextScene, this);
        }
        private void EndGesture() { draggingBaby = false; draggingItem = -1; baby.EndControlledDrag(); }
        private void OnApplicationFocus(bool focused) { if (!focused && baby != null) EndGesture(); }
        private void OnDisable()
        {
            if (rejection != null) rejection.Kill(false);
            if (baby != null) EndGesture();
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
                if (items[i].root != null)
                {
                    if (items[i].collider != null) items[i].collider.enabled = true;
                    SetContactFeedback(i, true);
                }
        }
    }
}
