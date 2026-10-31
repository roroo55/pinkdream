using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PinkDream
{
    public sealed class AdolescencePiercingLevel : MonoBehaviour
    {
        public enum PropKind { Cotton, Marker, Machine, Stud }
        public enum StudStyle { Star, Ring, Spike }
        [System.Serializable] public sealed class EarTarget
        {
            public CircleCollider2D lobe;
            public GameObject earring;
            public GameObject guide, mark;
            public SpriteRenderer cleanFlash;
            public GameObject[] styles;
        }
        [System.Serializable] public sealed class InteractionItem
        {
            public PropKind kind;
            public StudStyle style;
            public Transform root;
            public Collider2D collider, tip;
        }
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll girl;
        [SerializeField] private Transform leftHandAnchor, rightHandAnchor;
        // Retain the authored physical tool and ear references.
        [SerializeField] private Transform piercingTool;
        [SerializeField] private PolygonCollider2D toolCollider;
        [SerializeField] private CircleCollider2D toolTip;
        [SerializeField] private EarTarget[] ears;
        [SerializeField] private Text feedbackText;
        [SerializeField, Min(0)] private float completionHoldSeconds = 2f;
        [SerializeField] private string nextScene = "Level05_ComingOfAge";
        [SerializeField] private InteractionItem[] objects;
        [SerializeField] private PiercingInspectionLens inspectionLens;
        [SerializeField] private Collider2D loadingZone, mirrorCollider;
        [SerializeField] private SpriteRenderer machineLight, mirrorAccent;
        [SerializeField] private SpriteRenderer[] progressDots;
        [SerializeField] private GameObject[] loadedIcons;
        [SerializeField] private Transform completionEffects;
        [SerializeField] private LevelCompletionGlow completionGlow;

        private readonly CircleCollider2D[] palms = new CircleCollider2D[2];
        private readonly int[] held = { -1, -1 };
        private readonly bool[] revealed = new bool[2], cleaned = new bool[2], marked = new bool[2], pierced = new bool[2];
        private readonly int[] earStyles = { -1, -1 };
        private bool[] consumed, returning, mustSeparate;
        private Transform[] restParents;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Renderer[][] renderers;
        private int[][] originalOrders;
        private int loaded = -1, chosenStyle = -1, activeProp = -1;
        private bool dragging;
        private Tween lightTween, mirrorTween;
        public bool IsComplete { get; private set; }
        private static readonly Color Neutral = new Color32(86,93,99,255), Cyan = new Color32(113,221,222,255), Green = new Color32(148,228,185,255), Gold = new Color32(244,205,117,255);

        private void Awake()
        {
            girl.ReadMouseInput = false;
            palms[0] = leftHandAnchor.parent.GetComponent<CircleCollider2D>();
            palms[1] = rightHandAnchor.parent.GetComponent<CircleCollider2D>();
            int count = objects.Length;
            consumed = new bool[count]; returning = new bool[count]; mustSeparate = new bool[count];
            restParents = new Transform[count]; restPositions = new Vector3[count]; restRotations = new Quaternion[count];
            renderers = new Renderer[count][]; originalOrders = new int[count][];
            for (int i = 0; i < count; i++)
            {
                restParents[i] = objects[i].root.parent; restPositions[i] = objects[i].root.position; restRotations[i] = objects[i].root.rotation;
                renderers[i] = objects[i].root.GetComponentsInChildren<Renderer>(true); originalOrders[i] = new int[renderers[i].Length];
                for (int r = 0; r < renderers[i].Length; r++) originalOrders[i][r] = renderers[i][r].sortingOrder;
            }
            RefreshProgress(); ShowNextStep();
        }
        private void Update()
        {
            if (IsComplete || SceneFadeTransition.InputBlocked) return;
            Vector2 screen = Input.mousePosition;
            if (!Application.isFocused || !interactionCamera.pixelRect.Contains(screen)) { EndGesture(false); return; }
            Vector2 pointer = girl.ScreenToWorld(screen);
            if (Input.GetMouseButtonDown(0)) BeginGesture(pointer);
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
            {
                if (inspectionLens.IsDragging) inspectionLens.MoveDrag(pointer);
                else if (dragging && girl.Model.GrabWorld != pointer)
                {
                    girl.MoveControlledDrag(pointer); Physics2D.SyncTransforms(); TryTakeProps(); inspectionLens.RefreshViewAndInspect();
                }
            }
            if (!Input.GetMouseButton(0)) EndGesture(Input.GetMouseButtonUp(0));
        }
        private void BeginGesture(Vector2 pointer)
        {
            EndGesture(false);
            if (inspectionLens.BeginDrag(pointer)) return;
            if (mirrorCollider.OverlapPoint(pointer))
            {
                ShowNextStep();
                if (held[0] >= 0 && held[1] >= 0 && !Both(marked)) feedbackText.text += "\nSet tools down away from the ears";
                mirrorTween?.Kill(); mirrorAccent.color = Cyan;
                mirrorTween = DOTween.To(() => mirrorAccent.color, c => mirrorAccent.color = c, Neutral, .65f).SetUpdate(true).SetLink(gameObject);
                return;
            }
            int part = -1;
            for (int hand = 0; hand < 2; hand++)
                if (held[hand] >= 0 && objects[held[hand]].collider.OverlapPoint(pointer)) { part = hand == 0 ? 3 : 5; activeProp = held[hand]; break; }
            if (part < 0)
                for (int i = 0; i < objects.Length; i++)
                {
                    if (!Eligible(i) || !objects[i].collider.OverlapPoint(pointer)) continue;
                    for (int hand = 0; hand < 2; hand++)
                        if (held[hand] < 0 && Touches(palms[hand], objects[i].collider)) { Take(i, hand); activeProp = i; part = hand == 0 ? 3 : 5; break; }
                    if (part >= 0) break;
                }
            if (part < 0) part = girl.Pick(pointer);
            if (part < 0) foreach (EarTarget ear in ears) if (ear.lobe.OverlapPoint(pointer)) { part = 1; break; }
            if (part < 0) return;
            if (part == 3 || part == 5) activeProp = held[part == 3 ? 0 : 1];
            dragging = true; girl.BeginControlledDrag(part, pointer);
        }
        private static bool Both(bool[] state) => state[0] && state[1];
        private static bool Touches(Collider2D a, Collider2D b)
        {
            if (a == null || b == null || !a.enabled || !b.enabled || !a.gameObject.activeInHierarchy || !b.gameObject.activeInHierarchy) return false;
            ColliderDistance2D d = a.Distance(b); return d.isValid && d.distance <= 0;
        }
        private bool Eligible(int i)
        {
            if (consumed[i] || returning[i] || i == loaded || held[0] == i || held[1] == i || !objects[i].root.gameObject.activeInHierarchy) return false;
            if (objects[i].kind == PropKind.Cotton && Both(cleaned) || objects[i].kind == PropKind.Marker && Both(marked)) return false;
            return objects[i].kind != PropKind.Stud || Both(marked);
        }
        private void TryTakeProps()
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (!Eligible(i)) continue;
                bool contact = Touches(palms[0], objects[i].collider) || Touches(palms[1], objects[i].collider);
                if (!contact) { mustSeparate[i] = false; continue; }
                if (mustSeparate[i]) continue;
                for (int hand = 0; hand < 2; hand++) if (held[hand] < 0 && Touches(palms[hand], objects[i].collider)) { Take(i, hand); break; }
            }
        }
        private void Take(int index, int hand)
        {
            held[hand] = index;
            // Palm contact retains the precise world pose with no attraction or grip snap.
            objects[index].root.SetParent(hand == 0 ? leftHandAnchor : rightHandAnchor, true);
            int lowestOrder = int.MaxValue;
            foreach (int order in originalOrders[index]) lowestOrder = Mathf.Min(lowestOrder, order);
            for (int r = 0; r < renderers[index].Length; r++) renderers[index][r].sortingOrder = 360 + originalOrders[index][r] - lowestOrder;
            ItemContactFeedback.Play(objects[index].root);
            if (dragging) activeProp = index;
        }
        private void Release(int index)
        {
            for (int hand = 0; hand < 2; hand++) if (held[hand] == index) held[hand] = -1;
            objects[index].root.SetParent(restParents[index], true); mustSeparate[index] = true;
            for (int r = 0; r < renderers[index].Length; r++) renderers[index][r].sortingOrder = originalOrders[index][r];
        }
        private void ReturnToTray(int index)
        {
            Release(index); returning[index] = true;
            var feedback = objects[index].root.GetComponent<ItemContactFeedback>(); if (feedback != null) feedback.enabled = false;
            objects[index].collider.enabled = false;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Append(objects[index].root.DOMove(restPositions[index], .3f).SetEase(Ease.OutQuad));
            sequence.Join(objects[index].root.DORotateQuaternion(restRotations[index], .3f).SetEase(Ease.OutQuad));
            sequence.OnComplete(() => { returning[index] = false; objects[index].collider.enabled = true; if (feedback != null) feedback.enabled = true; });
        }
        private void EndGesture(bool confirm)
        {
            bool wasDragging = dragging; dragging = false; girl.EndControlledDrag(); inspectionLens?.EndDrag();
            if (confirm && wasDragging && !IsComplete)
            {
                Physics2D.SyncTransforms();
                bool loadingAttempt = TryLoadStud();
                if (!loadingAttempt)
                    for (int hand = 0; hand < 2; hand++)
                    {
                        int item = held[hand]; if (item < 0 || activeProp >= 0 && item != activeProp) continue;
                        for (int ear = 0; ear < 2; ear++) if (Touches(objects[item].tip, ears[ear].lobe)) { UseAtEar(item, ear); break; }
                    }
                // Set down away from the ears. A second click can regrasp at contact.
                for (int hand = 0; hand < 2; hand++)
                {
                    int item = held[hand]; if (item < 0 || activeProp >= 0 && item != activeProp) continue;
                    bool atEar = Touches(objects[item].tip ?? objects[item].collider, ears[0].lobe) || Touches(objects[item].tip ?? objects[item].collider, ears[1].lobe);
                    if (!atEar && (objects[item].kind != PropKind.Stud || !Touches(objects[item].collider, loadingZone))) Release(item);
                }
            }
            activeProp = -1;
        }
        public void RevealEar(int index)
        {
            if (revealed[index]) return;
            revealed[index] = true; RefreshProgress(); ShowNextStep();
        }
        private bool TryLoadStud()
        {
            for (int hand = 0; hand < 2; hand++)
            {
                int index = held[hand];
                if (index < 0 || objects[index].kind != PropKind.Stud || !Touches(objects[index].collider, loadingZone)) continue;
                LoadStud(index); return true;
            }
            return false;
        }
        private void LoadStud(int index)
        {
            if (!Both(marked)) { Refuse("Clean and mark both ears before loading a stud"); ReturnToTray(index); return; }
            if (loaded >= 0) { Refuse("The machine holds only one stud"); ReturnToTray(index); return; }
            int style = (int)objects[index].style;
            if (chosenStyle >= 0 && chosenStyle != style) { Refuse("Remember your chosen stud style"); ReturnToTray(index); return; }
            chosenStyle = style; loaded = index; Release(index); objects[index].collider.enabled = false; objects[index].root.gameObject.SetActive(false);
            RefreshProgress(); ShowNextStep();
        }
        private void UseAtEar(int item, int ear)
        {
            switch (objects[item].kind)
            {
                case PropKind.Cotton:
                    if (!Both(revealed)) { feedbackText.text = "Use the magnifier to discover both ear spots first"; return; }
                    if (cleaned[ear]) return;
                    cleaned[ear] = true; Flash(ears[ear].cleanFlash, Green, .6f);
                    if (Both(cleaned)) ReturnToTray(item);
                    RefreshProgress(); ShowNextStep(); break;
                case PropKind.Marker:
                    if (!Both(cleaned)) { feedbackText.text = "Clean both ears before making any marks"; return; }
                    if (marked[ear]) return;
                    marked[ear] = true; if (Both(marked)) ReturnToTray(item);
                    RefreshProgress(); ShowNextStep(); break;
                case PropKind.Machine: OperateMachine(ear); break;
                case PropKind.Stud: feedbackText.text = "Load the stud into the machine first"; break;
            }
        }
        private void OperateMachine(int ear)
        {
            if (!Both(revealed) || !Both(cleaned) || !Both(marked)) { Refuse("Discover clean and mark both ears before using the machine"); return; }
            if (pierced[ear]) { Refuse("This ear is finished   Use the other ear"); return; }
            if (loaded < 0) { Refuse("The machine is empty   Load one stud"); return; }
            if ((int)objects[loaded].style != chosenStyle) { Refuse("Reload the same chosen stud style"); return; }
            pierced[ear] = true; earStyles[ear] = chosenStyle; consumed[loaded] = true; loaded = -1;
            RefreshProgress(); FlashLight(Green);
            if (Both(pierced) && earStyles[0] == earStyles[1])
            {
                IsComplete = true; feedbackText.text = "Both ears match   Moving to the next stage";
                Celebrate(); if (Application.isPlaying) StartCoroutine(ContinueToNextLevel());
            }
            else ShowNextStep();
        }
        private void Refuse(string message) { feedbackText.text = message; FlashLight(new Color32(240,85,98,255)); }
        private Color BaseLight => IsComplete ? Green : loaded >= 0 ? Gold : Neutral;
        private void FlashLight(Color color)
        {
            lightTween?.Kill(); machineLight.color = color;
            lightTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).AppendInterval(.22f)
                .Append(DOTween.To(() => machineLight.color, c => machineLight.color = c, BaseLight, .35f));
        }
        private void Flash(SpriteRenderer renderer, Color color, float duration)
        {
            renderer.color = color; Color end = color; end.a = 0;
            DOTween.To(() => renderer.color, c => renderer.color = c, end, duration).SetUpdate(true).SetLink(gameObject).OnUpdate(() => inspectionLens.RefreshView());
        }
        private void RefreshProgress()
        {
            for (int ear = 0; ear < 2; ear++)
            {
                ears[ear].guide.SetActive(revealed[ear] && !marked[ear] && !pierced[ear]); ears[ear].mark.SetActive(marked[ear] && !pierced[ear]);
                for (int style = 0; style < 3; style++) ears[ear].styles[style].SetActive(pierced[ear] && earStyles[ear] == style);
                bool[] state = { revealed[ear], cleaned[ear], marked[ear], pierced[ear] }; Color[] colors = { Cyan, Green, Gold, Color.white };
                for (int step = 0; step < 4; step++) progressDots[ear * 4 + step].color = state[step] ? colors[step] : Neutral;
            }
            for (int i = 0; i < loadedIcons.Length; i++) loadedIcons[i].SetActive(loaded >= 0 && chosenStyle == i);
            lightTween?.Kill(); machineLight.color = BaseLight; inspectionLens.RefreshView();
        }
        private void ShowNextStep()
        {
            feedbackText.text = !Both(revealed) ? "Use the magnifier to find both suggested ear spots" :
                !Both(cleaned) ? "Touch both ears with the cleaning pad and release" :
                !Both(marked) ? "Touch each suggested spot with the marker and release" :
                chosenStyle < 0 ? "Choose a stud and load it into the machine" :
                loaded < 0 ? "Machine empty   Reload the same style" :
                !pierced[0] && !pierced[1] ? "Align the machine with either ear mark and release" :
                !pierced[0] ? "Align the machine with the left mark and release" :
                !pierced[1] ? "Align the machine with the right mark and release" : "Both ears match";
        }
        private void Celebrate()
        {
            completionGlow?.Play();
            completionEffects.gameObject.SetActive(true);
            foreach (SpriteRenderer sparkle in completionEffects.GetComponentsInChildren<SpriteRenderer>())
            {
                Vector3 rest = sparkle.transform.localPosition;
                var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
                sequence.Append(sparkle.transform.DOScale(sparkle.transform.localScale * 1.35f, .22f).SetEase(Ease.OutBack));
                sequence.Join(sparkle.transform.DOLocalMove(rest + Vector3.up * .25f, .7f).SetEase(Ease.OutSine));
                Color end = sparkle.color; end.a = 0;
                sequence.Join(DOTween.To(() => sparkle.color, c => sparkle.color = c, end, .6f).SetDelay(.3f));
            }
        }
        private IEnumerator ContinueToNextLevel()
        {
            if (completionHoldSeconds > 0) yield return new WaitForSecondsRealtime(completionHoldSeconds);
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene)) SceneFadeTransition.Load(nextScene);
        }
        private void OnApplicationFocus(bool focused) { if (!focused && girl != null) EndGesture(false); }
        private void OnDisable() { if (girl != null) EndGesture(false); lightTween?.Kill(); mirrorTween?.Kill(); }
    }
}
