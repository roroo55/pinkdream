using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class ChildhoodPackingLevel : MonoBehaviour
    {
        public enum ItemKind { Paintbrush, DrawingBook, Football, Umbrella, Toy, GameConsole, WaterCup, Storybook }
        public enum PocketKind { Side, Back, Main }
        [System.Serializable] public sealed class PackingItem
        {
            public string itemName;
            public Transform root;
            public PolygonCollider2D collider;
            public ItemKind kind;
            public Transform visual;
        }
        [System.Serializable] public sealed class BagSlot
        {
            public PocketKind kind;
            public BoxCollider2D zone;
            public Transform displayAnchor;
            public SpriteRenderer capacityDot;
        }
        [System.Serializable] public sealed class DayPlan
        {
            public string planName;
            public bool rainy;
            public ItemKind back;
            public ItemKind main;
        }
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll girl;
        [SerializeField] private Transform leftHandAnchor, rightHandAnchor;
        [SerializeField] private PackingItem[] items;
        [SerializeField] private BagSlot[] slots;
        [SerializeField] private DayPlan[] plans;
        [SerializeField] private SchoolScheduleCard scheduleCard;
        [SerializeField] private Transform packedItemsRoot, looseItemsRoot;
        [SerializeField] private Transform zipperHandle, zipperStart, zipperEnd;
        [SerializeField] private Collider2D zipperCollider;
        [SerializeField] private GameObject openBag, closedBag;
        [SerializeField] private Text progressText, feedbackText;
        [InspectorName("成功关闭书包后的停留时间（秒）"), Min(0)]
        [SerializeField] private float completionHoldSeconds = 2f;
        [SerializeField] private string nextScene = "Level04_AdolescencePiercing";

        private readonly int[] heldItems = { -1, -1 };
        private readonly CircleCollider2D[] palms = new CircleCollider2D[2];
        private int[] occupants;
        private bool[] packed;
        private bool dragging, draggingZip, resolving;
        private float zipGrabOffset, zipProgress;
        private DayPlan plan;
        private Vector3[] visualPositions, visualScales;
        private Quaternion[] visualRotations;
        private Tween[] settling;
        private Sequence resolution;
        public bool IsComplete { get; private set; }

        private void Awake()
        {
            girl.ReadMouseInput = false;
            palms[0] = leftHandAnchor.parent.GetComponent<CircleCollider2D>();
            palms[1] = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
            packed = new bool[items.Length]; occupants = new int[slots.Length];
            for (int i = 0; i < occupants.Length; i++) occupants[i] = -1;
            visualPositions = new Vector3[items.Length]; visualScales = new Vector3[items.Length]; visualRotations = new Quaternion[items.Length]; settling = new Tween[items.Length];
            for (int i = 0; i < items.Length; i++) { visualPositions[i] = items[i].visual.localPosition; visualScales[i] = items[i].visual.localScale; visualRotations[i] = items[i].visual.localRotation; }
            plan = plans[Random.Range(0, plans.Length)];
            scheduleCard.SetPlan(plan.rainy, Needed(PocketKind.Side), Needed(PocketKind.Back), Needed(PocketKind.Main));
            UpdateProgress();
        }
        private ItemKind Needed(PocketKind pocket) => pocket == PocketKind.Side ? (plan.rainy ? ItemKind.Umbrella : ItemKind.Paintbrush) : pocket == PocketKind.Back ? plan.back : plan.main;
        private void Update()
        {
            if (IsComplete || resolving || SceneFadeTransition.InputBlocked) return;
            Vector2 screen = Input.mousePosition;
            if (!Application.isFocused || !interactionCamera.pixelRect.Contains(screen)) { EndGesture(false); return; }
            Vector2 pointer = girl.ScreenToWorld(screen);
            if (Input.GetMouseButtonDown(0)) BeginGesture(pointer);
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)) MoveGesture(pointer);
            if (!Input.GetMouseButton(0)) EndGesture(Input.GetMouseButtonUp(0));
        }
        private void BeginGesture(Vector2 pointer)
        {
            EndGesture(false);
            if (scheduleCard.BeginDrag(pointer)) return;
            if (zipperCollider.OverlapPoint(pointer)) { draggingZip = true; zipGrabOffset = pointer.x - zipperHandle.position.x; return; }
            int part = girl.Pick(pointer);
            for (int hand = 0; hand < heldItems.Length; hand++)
                if (heldItems[hand] >= 0 && items[heldItems[hand]].collider.OverlapPoint(pointer)) { part = hand == 0 ? 3 : 5; break; }
            if (part < 0) return;
            dragging = true; girl.BeginControlledDrag(part, pointer);
        }
        private void MoveGesture(Vector2 pointer)
        {
            if (scheduleCard.IsDragging)
            {
                bool wasRevealed = scheduleCard.Revealed; scheduleCard.MoveDrag(pointer);
                if (!wasRevealed && scheduleCard.Revealed) feedbackText.text = "Follow the three icons and choose the right pockets";
                return;
            }
            if (draggingZip)
            {
                zipProgress = Mathf.Clamp01((pointer.x - zipGrabOffset - zipperStart.position.x) / (zipperEnd.position.x - zipperStart.position.x));
                zipperHandle.position = Vector3.Lerp(zipperStart.position, zipperEnd.position, zipProgress);
                if (zipProgress >= 1) { draggingZip = false; CheckBag(); }
                return;
            }
            if (!dragging || girl.Model.GrabWorld == pointer) return;
            girl.MoveControlledDrag(pointer); TryTakeItems();
        }
        private void TryTakeItems()
        {
            for (int hand = 0; hand < heldItems.Length; hand++)
            {
                if (heldItems[hand] >= 0) continue;
                for (int i = 0; i < items.Length; i++)
                {
                    if (packed[i] || heldItems[0] == i || heldItems[1] == i) continue;
                    ColliderDistance2D contact = palms[hand].Distance(items[i].collider);
                    if (!contact.isValid || contact.distance > 0) continue;
                    heldItems[hand] = i; items[i].root.SetParent(hand == 0 ? leftHandAnchor : rightHandAnchor, true);
                    ItemContactFeedback.Play(items[i].root); SetItemOrder(items[i], 300);
                    feedbackText.text = "Picked up " + items[i].itemName + "   Place it in a pocket and release";
                    break;
                }
            }
        }
        private void EndGesture(bool putAway)
        {
            bool wasDragging = dragging; dragging = false; girl.EndControlledDrag(); scheduleCard.EndDrag();
            if (draggingZip) { draggingZip = false; ReturnZipper("Pull the zipper all the way to close the bag"); }
            if (!putAway || !wasDragging || IsComplete || resolving) return;
            for (int hand = 0; hand < heldItems.Length; hand++)
            {
                int index = heldItems[hand]; if (index < 0) continue;
                bool full = true; foreach (int stored in occupants) if (stored < 0) full = false;
                if (full) { feedbackText.text = "The bag is full   Close the zipper to check"; break; }
                int slot = -1;
                for (int s = 0; s < slots.Length; s++)
                {
                    ColliderDistance2D contact = items[index].collider.Distance(slots[s].zone);
                    if (slots[s].zone.OverlapPoint(palms[hand].transform.TransformPoint(palms[hand].offset)) && contact.isValid && contact.distance <= 0) { slot = s; break; }
                }
                if (slot < 0) continue;
                if (occupants[slot] >= 0) { feedbackText.text = "That place is full   Try another pocket"; continue; }
                StoreItem(index, hand, slot);
            }
        }
        private void StoreItem(int index, int hand, int slot)
        {
            PackingItem item = items[index]; heldItems[hand] = -1; occupants[slot] = index; packed[index] = true;
            item.root.SetParent(packedItemsRoot, true); item.collider.enabled = false;
            if (item.root.TryGetComponent(out ItemContactFeedback contact)) contact.enabled = false;
            SetItemOrder(item, 160);
            // Only the drawing settles inside the pocket. Its physical root stays at release.
            item.visual.rotation = Quaternion.identity;
            Bounds drawing = item.visual.GetComponentInChildren<Renderer>().bounds;
            foreach (Renderer renderer in item.visual.GetComponentsInChildren<Renderer>()) drawing.Encapsulate(renderer.bounds);
            Vector2 space = slots[slot].zone.size * (Vector2)slots[slot].zone.transform.lossyScale;
            float scale = Mathf.Min(1, (space.x - 0.12f) / drawing.size.x, (space.y - 0.12f) / drawing.size.y);
            var settle = DOTween.Sequence().SetUpdate(true).SetLink(item.root.gameObject, LinkBehaviour.KillOnDisable);
            settle.Join(item.visual.DOMove(slots[slot].displayAnchor.position, 0.18f).SetEase(Ease.OutQuad));
            settle.Join(item.visual.DOScale(new Vector3(scale, scale, 1), 0.18f).SetEase(Ease.OutQuad)); settling[index] = settle;
            feedbackText.text = "Packed   Fill three places then close the zipper"; UpdateProgress();
        }
        private void CheckBag()
        {
            if (!scheduleCard.Revealed) { ReturnZipper("Read the schedule card with the magnifier first"); return; }
            var wrong = new List<int>();
            for (int s = 0; s < slots.Length; s++) if (occupants[s] >= 0 && items[occupants[s]].kind != Needed(slots[s].kind)) wrong.Add(s);
            if (wrong.Count > 0) { RejectItems(wrong); return; }
            foreach (int item in occupants) if (item < 0) { ReturnZipper("Fill all three places before closing the bag"); return; }
            IsComplete = true; openBag.SetActive(false); closedBag.SetActive(true);
            foreach (int index in occupants) items[index].root.gameObject.SetActive(false);
            feedbackText.text = "Ready for tomorrow   Moving to the next stage";
            StartCoroutine(ContinueToNextLevel());
        }
        private void ReturnZipper(string message)
        {
            feedbackText.text = message; resolving = true;
            resolution = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            resolution.Append(zipperHandle.DOMove(zipperStart.position, 0.2f).SetEase(Ease.OutQuad));
            resolution.OnComplete(() => { zipProgress = 0; resolving = false; }); resolution.OnKill(() => resolution = null);
        }
        private void RejectItems(List<int> wrongSlots)
        {
            resolving = true; feedbackText.text = "The wrong items came out   Keep the correct ones and try again";
            resolution = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            resolution.Join(zipperHandle.DOMove(zipperStart.position, 0.2f).SetEase(Ease.OutQuad));
            var rejected = new List<int>();
            for (int n = 0; n < wrongSlots.Count; n++)
            {
                int slot = wrongSlots[n], index = occupants[slot]; occupants[slot] = -1; packed[index] = false; rejected.Add(index);
                PackingItem item = items[index]; if (settling[index] != null) settling[index].Kill(false);
                Vector3 center = item.visual.position;
                item.root.SetParent(looseItemsRoot, true); item.root.rotation = Quaternion.identity;
                item.visual.localPosition = visualPositions[index]; item.visual.localRotation = visualRotations[index]; item.visual.localScale = visualScales[index];
                item.root.position += center - item.visual.position; SetItemOrder(item, 210);
                Vector3 landing = packedItemsRoot.position + new Vector3(-1.85f + n * 1.2f, -2.65f, 0);
                landing.z = item.root.position.z;
                resolution.Join(item.root.DOJump(landing, 0.6f, 1, 0.5f).SetEase(Ease.OutQuad));
            }
            UpdateProgress();
            resolution.OnComplete(() =>
            {
                foreach (int index in rejected) { items[index].collider.enabled = true; if (items[index].root.TryGetComponent(out ItemContactFeedback feedback)) feedback.enabled = true; }
                zipProgress = 0; resolving = false; Physics2D.SyncTransforms();
            });
            resolution.OnKill(() => resolution = null);
        }
        private void UpdateProgress()
        {
            int count = 0;
            for (int s = 0; s < occupants.Length; s++)
            {
                bool full = occupants[s] >= 0; if (full) count++;
                slots[s].capacityDot.color = full ? new Color32(184, 95, 137, 255) : new Color32(255, 238, 245, 255);
            }
            progressText.text = "Packed " + EnglishText.Number(count) + " of three";
        }
        private static void SetItemOrder(PackingItem item, int order)
        {
            foreach (Renderer renderer in item.root.GetComponentsInChildren<Renderer>()) renderer.sortingOrder = order + renderer.sortingOrder % 10;
        }
        private IEnumerator ContinueToNextLevel()
        {
            if (completionHoldSeconds > 0) yield return new WaitForSecondsRealtime(completionHoldSeconds);
            if (Application.CanStreamedLevelBeLoaded(nextScene)) SceneFadeTransition.Load(nextScene);
        }
        private void OnApplicationFocus(bool focused) { if (!focused && girl != null) EndGesture(false); }
        private void OnDisable()
        {
            if (resolution != null) resolution.Kill(false);
            if (settling != null) foreach (Tween tween in settling) if (tween != null) tween.Kill(false);
            if (girl != null) girl.EndControlledDrag(); if (scheduleCard != null) scheduleCard.EndDrag();
        }
    }
}
