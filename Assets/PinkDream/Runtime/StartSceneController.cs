using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class StartSceneController : MonoBehaviour
    {
        [Header("场景中的父母与放大镜")]
        [SerializeField] private FrozenRagdoll mother;
        [SerializeField] private FrozenRagdoll father;
        [SerializeField] private Transform magnifier;
        [SerializeField] private Transform magnifierGrip;
        [SerializeField] private Collider2D[] magnifierColliders;
        [SerializeField] private FrozenRagdoll initialHolder;
        [SerializeField] private int initialHolderHand = 5;
        [Header("B 超与真实局部放大")]
        [SerializeField] private Camera magnifierCamera;
        [SerializeField] private Transform babyFocus;
        [SerializeField] private Collider2D ultrasoundBounds;
        [InspectorName("放大倍率"), Range(1.2f, 4)]
        [SerializeField] private float magnification = 2.1f;
        [SerializeField] private float lensWorldRadius = 0.88f;
        [InspectorName("对准胎儿的允许距离"), Min(0.01f)]
        [SerializeField] private float focusTolerance = 0.3f;
        [Header("发现 START 后保持")]
        [SerializeField] private CanvasGroup discoveredStart;
        [SerializeField] private Text instructionText;
        [InspectorName("保持时间（至少 1 秒）"), Min(1)]
        [SerializeField] private float revealHoldSeconds = 1;
        [InspectorName("停顿时允许的轻微移动"), Min(0)]
        [SerializeField] private float stillnessTolerance = 0.035f;
        [SerializeField] private string firstLevelScene = "Level01_BeforeBirth";

        private FrozenRagdoll activeCharacter;
        private FrozenRagdoll holder;
        private int holderHand;
        private Vector2 heldHandToLens;
        private Vector2 lensGrabOffset;
        private Vector2 dwellAnchor;
        private float dwellTime;
        private bool draggingLens, alignedLastFrame, initialized, captureDirty = true;
        private bool loading;
        private Vector3 lastCapturedPosition;

        private void Awake()
        {
            mother.ReadMouseInput = false;
            father.ReadMouseInput = false;
        }

        private void Start()
        {
            magnifierCamera.enabled = false;
            if (magnifierCamera.targetTexture != null && !magnifierCamera.targetTexture.IsCreated())
                magnifierCamera.targetTexture.Create();
            if (initialHolder != null)
                SetHolder(initialHolder, initialHolderHand);
            discoveredStart.alpha = 0;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || loading || SceneFadeTransition.InputBlocked)
                return;
            Vector2 screen = Input.mousePosition;
            Camera camera = Camera.main;
            if (!Application.isFocused || !camera.pixelRect.Contains(screen))
            {
                EndGesture();
                ResetDiscovery();
                return;
            }
            Vector2 pointer = mother.ScreenToWorld(screen);
            if (Input.GetMouseButtonDown(0))
                BeginGesture(pointer);
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
                MoveGesture(pointer);
            if (!Input.GetMouseButton(0))
                EndGesture();
            UpdateDiscovery();
        }

        private void LateUpdate()
        {
            if (!initialized || loading || (!captureDirty && magnifier.position == lastCapturedPosition))
                return;
            // The authored camera only sees the scan's visual layer, so neither
            // the parents nor the lens can render recursively into the lens.
            magnifierCamera.transform.position = new Vector3(magnifier.position.x, magnifier.position.y, -10);
            magnifierCamera.orthographicSize = lensWorldRadius / magnification;
            magnifierCamera.Render();
            lastCapturedPosition = magnifier.position;
            captureDirty = false;
        }

        private void BeginGesture(Vector2 pointer)
        {
            EndGesture();
            foreach (Collider2D collider in magnifierColliders)
            {
                if (!collider.OverlapPoint(pointer))
                    continue;
                draggingLens = true;
                lensGrabOffset = (Vector2)magnifier.position - pointer;
                if (holder != null)
                    holder.BeginControlledDrag(holderHand, holder.HandPosition(holderHand));
                return;
            }
            int motherPart = mother.Pick(pointer), fatherPart = father.Pick(pointer);
            if (motherPart < 0 && fatherPart < 0)
                return;
            bool chooseMother = fatherPart < 0 || (motherPart >= 0 &&
                (mother.Model.Parts[motherPart].Position - pointer).sqrMagnitude <=
                (father.Model.Parts[fatherPart].Position - pointer).sqrMagnitude);
            activeCharacter = chooseMother ? mother : father;
            activeCharacter.BeginControlledDrag(chooseMother ? motherPart : fatherPart, pointer);
        }

        private void MoveGesture(Vector2 pointer)
        {
            if (draggingLens)
            {
                Vector2 position = pointer + lensGrabOffset;
                if ((Vector2)magnifier.position == position)
                    return;
                SetLensPosition(position);
                if (holder != null)
                    holder.MoveControlledDrag(position - heldHandToLens);
            }
            else if (activeCharacter != null)
            {
                activeCharacter.MoveControlledDrag(pointer);
                if (activeCharacter == holder)
                    SetLensPosition(holder.HandPosition(holderHand) + heldHandToLens);
                else
                {
                    int part = activeCharacter.Model.GrabbedPart;
                    if ((part == 3 || part == 5) &&
                        Vector2.Distance(activeCharacter.HandPosition(part), magnifierGrip.position) < 0.28f)
                        SetHolder(activeCharacter, part);
                }
            }
        }

        private void SetLensPosition(Vector2 position)
        {
            if ((Vector2)magnifier.position == position)
                return;
            magnifier.position = new Vector3(position.x, position.y, magnifier.position.z);
            captureDirty = true;
            Physics2D.SyncTransforms();
        }

        private void SetHolder(FrozenRagdoll character, int hand)
        {
            holder = character;
            holderHand = hand;
            heldHandToLens = (Vector2)magnifier.position - holder.HandPosition(hand);
            if (initialized) ItemContactFeedback.Play(magnifier);
        }

        private void UpdateDiscovery()
        {
            Vector2 center = magnifier.position;
            bool aligned = ultrasoundBounds.OverlapPoint(center) &&
                Vector2.Distance(center, babyFocus.position) <= focusTolerance;
            if (!aligned)
            {
                ResetDiscovery();
                return;
            }
            discoveredStart.alpha = 1;
            instructionText.text = "START found Hold the magnifier still for one second";
            if (!alignedLastFrame || Vector2.Distance(center, dwellAnchor) > stillnessTolerance)
            {
                dwellAnchor = center;
                dwellTime = 0;
            }
            else
                dwellTime += Time.unscaledDeltaTime;
            alignedLastFrame = true;
            if (dwellTime < Mathf.Max(1, revealHoldSeconds))
                return;
            EndGesture();
            loading = SceneFadeTransition.Load(firstLevelScene);
        }

        private void ResetDiscovery()
        {
            dwellTime = 0;
            alignedLastFrame = false;
            discoveredStart.alpha = 0;
            instructionText.text = "Move the magnifier over the ultrasound You can drag the parents too";
        }

        private void EndGesture()
        {
            activeCharacter = null;
            draggingLens = false;
            mother.EndControlledDrag();
            father.EndControlledDrag();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && initialized)
            {
                EndGesture();
                ResetDiscovery();
            }
        }

        private void OnDisable()
        {
            if (initialized && mother != null && father != null)
                EndGesture();
        }
    }
}
