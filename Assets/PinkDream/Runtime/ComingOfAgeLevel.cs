using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class ComingOfAgeLevel : MonoBehaviour
    {
        public enum DecorationMotion { Still, Flower, Star, Candy }
        public enum ShapeKind { Heart, Star, Flower, Bow, Diamond }
        public enum ColorKind { Pink, Gold, Blue, Green, Purple }
        [System.Serializable] public sealed class Choice
        {
            public string itemName;
            public Transform item;
            public PolygonCollider2D collider;
            // Kept for compatibility with the original scene authoring tools.
            public Transform decoration;
            public DecorationMotion motion;
            public Transform sparkle;
            public ShapeKind shape;
            public ColorKind color;
        }
        [System.Serializable] public sealed class DecorationSlot
        {
            public Transform anchor;
            public CircleCollider2D zone;
        }
        [System.Serializable] public sealed class RuleIndicator
        {
            public GameObject neutral, passed, failed;
        }
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll girl;
        [SerializeField] private Transform leftHandAnchor, rightHandAnchor, looseItems;
        [SerializeField] private Choice[] choices;
        [SerializeField] private Collider2D cakeArea;
        [SerializeField] private GameObject finishControl;
        [SerializeField] private Transform pullHandle;
        [SerializeField] private CircleCollider2D handleCollider;
        [SerializeField] private LineRenderer pullRope;
        [SerializeField, Min(.1f)] private float pullDistance = .65f;
        [SerializeField, Min(.01f)] private float decorationRevealSeconds = .22f;
        [SerializeField] private Text combinationText, feedbackText;
        [SerializeField] private GameObject[] preparationHints;
        [SerializeField] private ComingOfAgeCelebration celebration;
        [SerializeField] private DecorationSlot[] slots;
        [SerializeField] private RuleIndicator[] ruleIndicators;
        [SerializeField] private BirthdayRuleCard birthdayCard;

        // Center is zero. The four corners follow clockwise from upper left.
        private static readonly int[,] Neighbors = { {0,1},{0,2},{0,3},{0,4},{1,2},{2,3},{3,4},{4,1} };
        private readonly int[] held = { -1, -1 };
        private readonly CircleCollider2D[] palms = new CircleCollider2D[2];
        private int[] occupants, slotOf;
        private bool[] settling, mustSeparate;
        private Renderer[][] renderers;
        private int[][] orders;
        private bool draggingGirl, draggingCord;
        private Vector3 handleRest;
        private float cordGrabOffset;
        private Tween cordReturn;
        public bool HasFinished { get; private set; }

        private void Awake()
        {
            girl.ReadMouseInput = false;
            palms[0] = leftHandAnchor.parent.GetComponent<CircleCollider2D>(); palms[1] = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
            occupants = new int[slots.Length]; slotOf = new int[choices.Length]; settling = new bool[choices.Length]; mustSeparate = new bool[choices.Length];
            renderers = new Renderer[choices.Length][]; orders = new int[choices.Length][];
            for (int s = 0; s < occupants.Length; s++) occupants[s] = -1;
            for (int i = 0; i < choices.Length; i++)
            {
                slotOf[i] = -1; renderers[i] = choices[i].item.GetComponentsInChildren<Renderer>(true); orders[i] = new int[renderers[i].Length];
                for (int r = 0; r < orders[i].Length; r++) orders[i][r] = renderers[i][r].sortingOrder;
            }
            handleRest = pullHandle.position; ClearIndicators(); UpdateCombination();
            feedbackText.text = "Use the magnifier to read the birthday card";
        }
        private static bool Touches(Collider2D a, Collider2D b)
        {
            if (a == null || b == null || !a.enabled || !b.enabled || !a.gameObject.activeInHierarchy || !b.gameObject.activeInHierarchy) return false;
            var d = a.Distance(b); return d.isValid && d.distance <= 0;
        }
        private void Update()
        {
            if (HasFinished || SceneFadeTransition.InputBlocked) return;
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
            if (birthdayCard.BeginDrag(pointer)) return;
            if (handleCollider.OverlapPoint(pointer) && (cordReturn == null || !cordReturn.IsActive()))
            { draggingCord = true; cordGrabOffset = pointer.y - pullHandle.position.y; return; }
            int part = -1;
            for (int hand = 0; hand < 2; hand++) if (held[hand] >= 0 && choices[held[hand]].collider.OverlapPoint(pointer)) { part = hand == 0 ? 3 : 5; break; }
            if (part < 0)
                for (int i = 0; i < choices.Length; i++)
                {
                    if (!Eligible(i) || !choices[i].collider.OverlapPoint(pointer)) continue;
                    for (int hand = 0; hand < 2; hand++) if (held[hand] < 0 && Touches(palms[hand], choices[i].collider)) { Take(i, hand); part = hand == 0 ? 3 : 5; break; }
                    if (part >= 0) break;
                    feedbackText.text = "Use her hand to take or move a decoration";
                }
            if (part < 0) part = girl.Pick(pointer);
            if (part >= 0) { draggingGirl = true; girl.BeginControlledDrag(part, pointer); }
        }
        private void MoveGesture(Vector2 pointer)
        {
            if (HasFinished) return;
            if (birthdayCard.IsDragging)
            {
                bool known = birthdayCard.Revealed; birthdayCard.MoveDrag(pointer);
                if (!known && birthdayCard.Revealed) feedbackText.text = "Use her hands to fill five places   Pull the cord to check";
                return;
            }
            if (draggingCord)
            {
                float pull = Mathf.Clamp(handleRest.y - (pointer.y - cordGrabOffset), 0, pullDistance);
                pullHandle.position = handleRest + Vector3.down * pull; UpdateRope();
                if (pull >= pullDistance) { draggingCord = false; FinishDecorating(); }
                return;
            }
            if (!draggingGirl || girl.Model.GrabWorld == pointer) return;
            girl.MoveControlledDrag(pointer); Physics2D.SyncTransforms(); TryTakeItems();
        }
        private bool Eligible(int index) => !settling[index] && held[0] != index && held[1] != index;
        private void TryTakeItems()
        {
            for (int i = 0; i < choices.Length; i++)
            {
                if (!Eligible(i)) continue;
                bool contact = Touches(palms[0], choices[i].collider) || Touches(palms[1], choices[i].collider);
                if (!contact) { mustSeparate[i] = false; continue; }
                if (mustSeparate[i]) continue;
                for (int hand = 0; hand < 2; hand++) if (held[hand] < 0 && Touches(palms[hand], choices[i].collider)) { Take(i, hand); break; }
            }
        }
        private void Take(int index, int hand)
        {
            if (slotOf[index] >= 0) { occupants[slotOf[index]] = -1; slotOf[index] = -1; ClearIndicators(); UpdateCombination(); }
            held[hand] = index;
            choices[index].item.SetParent(hand == 0 ? leftHandAnchor : rightHandAnchor, true);
            int min = int.MaxValue; foreach (int order in orders[index]) min = Mathf.Min(min, order);
            for (int r = 0; r < orders[index].Length; r++) renderers[index][r].sortingOrder = 400 + orders[index][r] - min;
            ItemContactFeedback.Play(choices[index].item);
            feedbackText.text = "Touch an empty cake place with the decoration then release";
        }
        private void Release(int index)
        {
            for (int hand = 0; hand < 2; hand++) if (held[hand] == index) held[hand] = -1;
            choices[index].item.SetParent(looseItems, true); mustSeparate[index] = true;
            for (int r = 0; r < orders[index].Length; r++) renderers[index][r].sortingOrder = orders[index][r];
        }
        private void Place(int index, int slot)
        {
            Release(index); occupants[slot] = index; slotOf[index] = slot; settling[index] = true;
            var choice = choices[index]; choice.collider.enabled = false;
            var feedback = choice.item.GetComponent<ItemContactFeedback>(); if (feedback != null) feedback.enabled = false;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            sequence.Append(choice.item.DOMove(slots[slot].anchor.position, decorationRevealSeconds).SetEase(Ease.OutQuad));
            sequence.Join(choice.item.DORotateQuaternion(Quaternion.identity, decorationRevealSeconds).SetEase(Ease.OutQuad));
            sequence.OnComplete(() => { settling[index] = false; choice.collider.enabled = true; if (feedback != null) feedback.enabled = true; Physics2D.SyncTransforms(); });
            ClearIndicators(); UpdateCombination(); feedbackText.text = "Pull the cord to check   Use her hands to adjust decorations";
        }
        private void EndGesture(bool confirm)
        {
            bool wasGirl = draggingGirl, wasCord = draggingCord; draggingGirl = draggingCord = false;
            girl.EndControlledDrag(); birthdayCard?.EndDrag();
            if (wasCord && !HasFinished) ReturnCord();
            if (!confirm || !wasGirl || HasFinished) return;
            Physics2D.SyncTransforms();
            for (int hand = 0; hand < 2; hand++)
            {
                int index = held[hand]; if (index < 0) continue;
                int touched = -1;
                for (int s = 0; s < slots.Length; s++)
                    if (Touches(palms[hand], slots[s].zone) && Touches(choices[index].collider, slots[s].zone)) { touched = s; break; }
                if (touched >= 0)
                {
                    if (occupants[touched] < 0) Place(index, touched);
                    else feedbackText.text = "That place is full   Take its decoration out first";
                }
                else Release(index);
            }
        }
        // Results identify rules only, never an individual piece or a suggested move.
        public bool[] EvaluateRules()
        {
            bool full = true; foreach (int item in occupants) if (item < 0) full = false;
            bool heart = occupants[0] >= 0 && choices[occupants[0]].shape == ShapeKind.Heart && choices[occupants[0]].color == ColorKind.Pink;
            bool mirror = SameShape(1, 2) && SameShape(4, 3);
            var colors = new HashSet<ColorKind>(); foreach (int item in occupants) if (item >= 0) colors.Add(choices[item].color);
            bool colorRule = colors.Count >= 3;
            for (int edge = 0; edge < Neighbors.GetLength(0); edge++)
            {
                int a = occupants[Neighbors[edge,0]], b = occupants[Neighbors[edge,1]];
                if (a >= 0 && b >= 0 && choices[a].color == choices[b].color) colorRule = false;
            }
            return new[] { full, heart, mirror, colorRule };
        }
        private bool SameShape(int a, int b) => occupants[a] >= 0 && occupants[b] >= 0 && choices[occupants[a]].shape == choices[occupants[b]].shape;
        private void ClearIndicators()
        {
            foreach (var rule in ruleIndicators) { rule.neutral.SetActive(true); rule.passed.SetActive(false); rule.failed.SetActive(false); }
        }
        private void UpdateCombination()
        {
            int count = 0; foreach (int index in occupants) if (index >= 0) count++;
            combinationText.text = "Placed " + EnglishText.Number(count) + " of five";
        }
        private void FinishDecorating()
        {
            bool[] result = EvaluateRules(); bool valid = true;
            for (int i = 0; i < result.Length; i++)
            { ruleIndicators[i].neutral.SetActive(false); ruleIndicators[i].passed.SetActive(result[i]); ruleIndicators[i].failed.SetActive(!result[i]); valid &= result[i]; }
            if (!valid)
            {
                feedbackText.text = "Check the red rule icons   Keep your cake and try again"; ReturnCord(); return;
            }
            HasFinished = true; EndGesture(false);
            for (int hand = 0; hand < 2; hand++) if (held[hand] >= 0) Release(held[hand]);
            finishControl.SetActive(false); birthdayCard.HideForCelebration();
            foreach (var hint in preparationHints) hint.SetActive(false);
            feedbackText.text = string.Empty; celebration.Begin();
        }
        private void UpdateRope() => pullRope.SetPosition(1, pullRope.transform.InverseTransformPoint(pullHandle.position));
        private void ReturnCord()
        {
            cordReturn?.Kill(); cordReturn = pullHandle.DOMove(handleRest, .24f).SetEase(Ease.OutSine).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable).OnUpdate(UpdateRope).OnKill(() => cordReturn = null);
        }
        private void OnApplicationFocus(bool focused) { if (!focused && girl != null) EndGesture(false); }
        private void OnDisable() { if (girl != null) EndGesture(false); cordReturn?.Kill(); }
    }
}
