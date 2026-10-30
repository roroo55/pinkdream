using UnityEngine;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class ComingOfAgeLevel : MonoBehaviour
    {
        public enum DecorationMotion { Still, Flower, Star, Candy }

        [System.Serializable]
        public sealed class Choice
        {
            public string itemName;
            public Transform item;
            public PolygonCollider2D collider;
            public Transform decoration;
            public DecorationMotion motion;
            public Transform sparkle;
        }

        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll girl;
        [SerializeField] private Transform leftHandAnchor, rightHandAnchor;
        [SerializeField] private Transform looseItems;
        [SerializeField] private Choice[] choices;
        [InspectorName("蛋糕旁的装饰触发区域")]
        [SerializeField] private Collider2D cakeArea;
        [SerializeField] private GameObject finishControl;
        [SerializeField] private Transform pullHandle;
        [SerializeField] private CircleCollider2D handleCollider;
        [SerializeField] private LineRenderer pullRope;
        [InspectorName("拉绳向下拉多远开始庆祝"), Min(0.1f)]
        [SerializeField] private float pullDistance = 0.65f;
        [InspectorName("装饰出现时间（秒）"), Min(0.01f)]
        [SerializeField] private float decorationRevealSeconds = 0.45f;
        [SerializeField] private Text combinationText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private GameObject[] preparationHints;
        [SerializeField] private ComingOfAgeCelebration celebration;

        private readonly int[] held = { -1, -1 };
        private readonly CircleCollider2D[] palms = new CircleCollider2D[2];
        private bool[] placed;
        private float[] placedAt;
        private Vector3[] decorationPositions, decorationScales, sparkleScales;
        private bool draggingGirl, draggingCord;
        private Vector3 handleRest;
        private float cordGrabOffset;
        private int decoratedCount;
        public bool HasFinished { get; private set; }

        private void Awake()
        {
            girl.ReadMouseInput = false;
            palms[0] = leftHandAnchor.parent.GetComponent<CircleCollider2D>();
            palms[1] = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
            placed = new bool[choices.Length];
            placedAt = new float[choices.Length];
            decorationPositions = new Vector3[choices.Length];
            decorationScales = new Vector3[choices.Length];
            sparkleScales = new Vector3[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                decorationPositions[i] = choices[i].decoration.localPosition;
                decorationScales[i] = choices[i].decoration.localScale;
                if (choices[i].sparkle != null)
                    sparkleScales[i] = choices[i].sparkle.localScale;
            }
            handleRest = pullHandle.position;
            UpdateCombination();
        }

        private void Update()
        {
            AnimateDecorations();
            if (HasFinished || SceneFadeTransition.InputBlocked)
                return;
            Vector2 screen = Input.mousePosition;
            if (!Application.isFocused || !interactionCamera.pixelRect.Contains(screen))
            {
                EndGesture();
                return;
            }
            Vector2 pointer = girl.ScreenToWorld(screen);
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
            if (handleCollider.OverlapPoint(pointer))
            {
                draggingCord = true;
                cordGrabOffset = pointer.y - pullHandle.position.y;
                return;
            }
            int part = -1;
            for (int hand = 0; hand < held.Length; hand++)
                if (held[hand] >= 0 && choices[held[hand]].collider.OverlapPoint(pointer))
                {
                    part = hand == 0 ? 3 : 5;
                    break;
                }
            if (part < 0)
                part = girl.Pick(pointer);
            if (part >= 0)
            {
                draggingGirl = true;
                girl.BeginControlledDrag(part, pointer);
            }
        }

        private void MoveGesture(Vector2 pointer)
        {
            if (HasFinished)
                return;
            if (draggingCord)
            {
                float pull = Mathf.Clamp(handleRest.y - (pointer.y - cordGrabOffset), 0, pullDistance);
                pullHandle.position = handleRest + Vector3.down * pull;
                pullRope.SetPosition(1, pullRope.transform.InverseTransformPoint(pullHandle.position));
                if (pull >= pullDistance)
                    FinishDecorating();
                return;
            }
            if (!draggingGirl || girl.Model.GrabWorld == pointer)
                return;
            girl.MoveControlledDrag(pointer);
            TryTakeItems();
            TryDecorate();
        }

        private void TryTakeItems()
        {
            for (int hand = 0; hand < held.Length; hand++)
            {
                if (held[hand] >= 0)
                    continue;
                for (int i = 0; i < choices.Length; i++)
                {
                    if (placed[i] || held[0] == i || held[1] == i)
                        continue;
                    ColliderDistance2D contact = palms[hand].Distance(choices[i].collider);
                    if (!contact.isValid || contact.distance > 0f)
                        continue;
                    held[hand] = i;
                    choices[i].item.SetParent(hand == 0 ? leftHandAnchor : rightHandAnchor, true);
                    foreach (SpriteRenderer sprite in choices[i].item.GetComponentsInChildren<SpriteRenderer>())
                        sprite.sortingOrder = 330 + sprite.sortingOrder % 10;
                    foreach (Canvas label in choices[i].item.GetComponentsInChildren<Canvas>())
                        label.enabled = false;
                    feedbackText.text = "Picked up " + choices[i].itemName + "   Bring it to the cake";
                    break;
                }
            }
        }

        private void TryDecorate()
        {
            for (int hand = 0; hand < held.Length; hand++)
            {
                int index = held[hand];
                if (index < 0)
                    continue;
                ColliderDistance2D contact = palms[hand].Distance(cakeArea);
                if (!contact.isValid || contact.distance > 0f)
                    continue;
                Choice choice = choices[index];
                held[hand] = -1;
                placed[index] = true;
                placedAt[index] = Time.unscaledTime;
                decoratedCount++;
                choice.item.SetParent(looseItems, true);
                choice.item.gameObject.SetActive(false);
                choice.decoration.localScale = decorationScales[index] * 0.01f;
                choice.decoration.gameObject.SetActive(true);
                UpdateCombination();
                feedbackText.text = "Add more or pull the cord on the right when you are ready";
            }
        }

        private void AnimateDecorations()
        {
            for (int i = 0; i < choices.Length; i++)
            {
                if (!placed[i])
                    continue;
                float age = Time.unscaledTime - placedAt[i];
                float growth = Mathf.SmoothStep(0.01f, 1, Mathf.Clamp01(age / decorationRevealSeconds));
                choices[i].decoration.localScale = decorationScales[i] * growth;
                if (choices[i].motion == DecorationMotion.Star)
                    choices[i].decoration.localPosition = decorationPositions[i] + Vector3.up * (Mathf.Sin(age * 1.8f) * 0.12f);
                if (choices[i].sparkle != null)
                    choices[i].sparkle.localScale = sparkleScales[i] * (0.7f + 0.3f * Mathf.Sin(age * 5f));
            }
        }

        private void UpdateCombination() => combinationText.text = "Decorations " + EnglishText.Number(decoratedCount) + "   Add more or finish when ready";

        private void FinishDecorating()
        {
            HasFinished = true;
            EndGesture();
            for (int hand = 0; hand < held.Length; hand++)
            {
                if (held[hand] >= 0)
                    choices[held[hand]].item.SetParent(looseItems, true);
                held[hand] = -1;
            }
            finishControl.SetActive(false);
            foreach (GameObject hint in preparationHints)
                hint.SetActive(false);
            feedbackText.text = string.Empty;
            celebration.Begin();
        }

        private void EndGesture()
        {
            draggingGirl = draggingCord = false;
            girl.EndControlledDrag();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && girl != null)
                EndGesture();
        }

        private void OnDisable()
        {
            if (girl != null)
                EndGesture();
        }
    }
}
