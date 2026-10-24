using UnityEngine;

namespace PinkDream
{
    /// <summary>Coordinates two unchanged frozen ragdolls and the cake reveal.</summary>
    public sealed class BeforeBirthLevel : MonoBehaviour
    {
        [Header("Scene characters")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FrozenRagdoll mother;
        [SerializeField] private FrozenRagdoll father;
        [Header("Cake and knife")]
        [SerializeField] private Transform knife;
        [SerializeField] private Collider2D knifeCollider;
        [SerializeField] private Transform bladeCutPoint;
        [SerializeField] private Collider2D cakeCutZone;
        [SerializeField] private Transform leftCakeHalf;
        [SerializeField] private Transform rightCakeHalf;
        [SerializeField] private GameObject pinkFilling;
        [SerializeField] private GameObject gripGuide;
        [Header("Reveal only")]
        [SerializeField] private ParticleSystem[] pinkConfetti;
        [SerializeField] private Transform[] pinkBalloons;
        [SerializeField] private float balloonRiseSpeed = 0.85f;
        [SerializeField] private BirthCompletionSequence completionSequence;
        [Header("Interaction distances")]
        [SerializeField] private float handJoinDistance = 0.34f;
        [SerializeField] private float knifeGripDistance = 0.42f;
        [SerializeField] private float cakeSeparation = 0.18f;

        private enum Step { JoinHands, GripKnife, CutCake, Revealed }
        private Step step;
        private FrozenRagdoll activeCharacter;
        private bool draggingKnife;
        private Vector2 knifeGrabOffset;
        private int motherHand = -1, fatherHand = -1;
        private bool enteredCake;
        private Vector2 previousBlade;

        private void Awake()
        {
            // One shared pointer chooses exactly one object. Each character's
            // movement still goes through FrozenRagdoll / PoseRagdollModel.
            mother.ReadMouseInput = false;
            father.ReadMouseInput = false;
        }

        private void Update()
        {
            if (step == Step.Revealed)
            {
                if (completionSequence != null)
                    return;
                MoveBalloons();
            }

            Vector2 screen = Input.mousePosition;
            if (!Application.isFocused || !interactionCamera.pixelRect.Contains(screen))
            {
                EndGesture();
                return;
            }
            Vector2 pointer = mother.ScreenToWorld(screen);
            if (Input.GetMouseButtonDown(0))
                BeginGesture(pointer);
            if ((Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)) &&
                (activeCharacter != null || draggingKnife))
                MoveGesture(pointer);
            if (!Input.GetMouseButton(0))
                EndGesture();
        }

        private void BeginGesture(Vector2 pointer)
        {
            EndGesture();
            previousBlade = bladeCutPoint.position;
            if (knifeCollider.OverlapPoint(pointer))
            {
                draggingKnife = true;
                knifeGrabOffset = (Vector2)knife.position - pointer;
                if (step >= Step.CutCake)
                    BeginLinkedHands();
                return;
            }

            int motherPart = mother.Pick(pointer), fatherPart = father.Pick(pointer);
            if (motherPart < 0 && fatherPart < 0)
                return;
            // At an overlap, prefer the visually closer body part's center.
            bool chooseMother = fatherPart < 0 || (motherPart >= 0 &&
                (mother.Model.Parts[motherPart].Position - pointer).sqrMagnitude <=
                (father.Model.Parts[fatherPart].Position - pointer).sqrMagnitude);
            activeCharacter = chooseMother ? mother : father;
            activeCharacter.BeginControlledDrag(chooseMother ? motherPart : fatherPart, pointer);
            if (step >= Step.GripKnife)
                BeginOtherHand();
        }

        private void MoveGesture(Vector2 pointer)
        {
            if (step == Step.Revealed && completionSequence != null)
                return;
            // No solver, coupling or cut progression runs for a stationary
            // pointer. Release and focus loss only end the current gesture.
            if (draggingKnife)
            {
                Vector2 position = pointer + knifeGrabOffset;
                if ((Vector2)knife.position == position)
                    return;
                knife.position = new Vector3(position.x, position.y, knife.position.z);
                if (step >= Step.CutCake)
                {
                    mother.MoveControlledDrag(position);
                    father.MoveControlledDrag(position);
                }
            }
            else
            {
                if (activeCharacter.Model.GrabWorld == pointer)
                    return;
                activeCharacter.MoveControlledDrag(pointer);
                if (step == Step.JoinHands)
                    TryJoinHands();
                if (step >= Step.GripKnife)
                {
                    FrozenRagdoll other = activeCharacter == mother ? father : mother;
                    int ownHand = activeCharacter == mother ? motherHand : fatherHand;
                    other.MoveControlledDrag(activeCharacter.HandPosition(ownHand));
                }
                if (step >= Step.CutCake)
                {
                    Vector2 grip = mother.HandPosition(motherHand);
                    knife.position = new Vector3(grip.x, grip.y, knife.position.z);
                }
            }

            if (step == Step.GripKnife)
                TryGripKnife();
            if (step == Step.CutCake)
                CheckCut();
            previousBlade = bladeCutPoint.position;
            Physics2D.SyncTransforms();
        }

        private void TryJoinHands()
        {
            int dragged = activeCharacter.Model.GrabbedPart;
            if (dragged != 3 && dragged != 5)
                return;
            FrozenRagdoll other = activeCharacter == mother ? father : mother;
            Vector2 hand = activeCharacter.HandPosition(dragged);
            int otherHand = Vector2.Distance(hand, other.HandPosition(3)) <=
                Vector2.Distance(hand, other.HandPosition(5)) ? 3 : 5;
            if (Vector2.Distance(hand, other.HandPosition(otherHand)) > handJoinDistance)
                return;
            motherHand = activeCharacter == mother ? dragged : otherHand;
            fatherHand = activeCharacter == father ? dragged : otherHand;
            step = Step.GripKnife;
            BeginOtherHand();
        }

        private void BeginOtherHand()
        {
            FrozenRagdoll other = activeCharacter == mother ? father : mother;
            int index = other == mother ? motherHand : fatherHand;
            other.BeginControlledDrag(index, other.HandPosition(index));
        }

        private void BeginLinkedHands()
        {
            mother.BeginControlledDrag(motherHand, mother.HandPosition(motherHand));
            father.BeginControlledDrag(fatherHand, father.HandPosition(fatherHand));
        }

        private void TryGripKnife()
        {
            Vector2 grip = mother.HandPosition(motherHand);
            if (Vector2.Distance(grip, knife.position) > knifeGripDistance)
                return;
            step = Step.CutCake;
            enteredCake = false;
            if (draggingKnife)
            {
                BeginLinkedHands();
                mother.MoveControlledDrag(knife.position);
                father.MoveControlledDrag(knife.position);
            }
            else
                knife.position = new Vector3(grip.x, grip.y, knife.position.z);
            gripGuide.SetActive(false);
            previousBlade = bladeCutPoint.position;
        }

        private void CheckCut()
        {
            Bounds cake = cakeCutZone.bounds;
            Vector2 blade = bladeCutPoint.position;
            // Sweep through the top edge, so even a fast downward drag can cut.
            if (!enteredCake && previousBlade.y >= cake.max.y && blade.y < previousBlade.y)
            {
                float t = (previousBlade.y - cake.max.y) / (previousBlade.y - blade.y);
                float x = Mathf.Lerp(previousBlade.x, blade.x, Mathf.Clamp01(t));
                if (t >= 0 && t <= 1 && x >= cake.min.x && x <= cake.max.x)
                    enteredCake = true;
            }
            if (blade.x < cake.min.x || blade.x > cake.max.x)
                enteredCake = false;
            if (enteredCake && blade.y <= cake.min.y + 0.12f)
                Reveal();
        }

        private void Reveal()
        {
            step = Step.Revealed;
            pinkFilling.SetActive(true);
            leftCakeHalf.localPosition += Vector3.left * cakeSeparation;
            rightCakeHalf.localPosition += Vector3.right * cakeSeparation;
            foreach (ParticleSystem confetti in pinkConfetti)
                confetti.Play(true);
            foreach (Transform balloon in pinkBalloons)
                balloon.gameObject.SetActive(true);
            if (completionSequence != null)
            {
                EndGesture();
                completionSequence.Begin();
            }
        }

        private void MoveBalloons()
        {
            foreach (Transform balloon in pinkBalloons)
                if (balloon.position.y < 7f)
                    balloon.position += Vector3.up * (balloonRiseSpeed * Time.deltaTime);
        }

        private void EndGesture()
        {
            activeCharacter = null;
            draggingKnife = false;
            mother.EndControlledDrag();
            father.EndControlledDrag();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                EndGesture();
        }

        private void OnDisable()
        {
            if (mother != null && father != null)
                EndGesture();
        }
    }
}
