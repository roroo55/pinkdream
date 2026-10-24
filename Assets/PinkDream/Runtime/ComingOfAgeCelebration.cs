using System.Collections.Generic;
using UnityEngine;

namespace PinkDream
{
    public sealed class ComingOfAgeCelebration : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private FrozenRagdoll girl;
        [SerializeField] private Transform[] happyPose;
        [SerializeField] private GameObject[] neutralFace;
        [SerializeField] private GameObject happyFace;
        [SerializeField] private GameObject birthdayText;
        [SerializeField] private GameObject cakeGlow;
        [SerializeField] private GameObject[] flames;
        [SerializeField] private ParticleSystem[] confetti;
        [SerializeField] private Transform[] balloons;
        [InspectorName("女孩抬手庆祝时间（秒）"), Min(0.01f)]
        [SerializeField] private float poseSeconds = 1f;
        [InspectorName("礼花气球保持时间（秒）"), Min(0)]
        [SerializeField] private float effectHoldSeconds = 2f;
        [InspectorName("礼花气球减淡时间（秒）"), Min(0.01f)]
        [SerializeField] private float effectFadeSeconds = 1.2f;
        [InspectorName("礼花速度"), Min(0)]
        [SerializeField] private float confettiSpeed = 7.5f;
        [InspectorName("礼花每秒数量（每侧）"), Min(0)]
        [SerializeField] private float confettiRate = 18f;
        [InspectorName("气球上升速度"), Min(0)]
        [SerializeField] private float balloonSpeed = 3f;

        private readonly List<SpriteRenderer> balloonSprites = new List<SpriteRenderer>();
        private readonly List<Color> balloonColors = new List<Color>();
        private readonly List<LineRenderer> balloonStrings = new List<LineRenderer>();
        private readonly List<Color> stringColors = new List<Color>();
        private Vector3[] balloonStarts;
        private float[] startAngles;
        private Vector2 startTorso;
        private float age;
        private bool poseFinished, effectsFinished, emissionStopped;
        private MaterialPropertyBlock fadeBlock;
        private static readonly int Tint = Shader.PropertyToID("_Color");
        public bool HasStarted { get; private set; }

        public void Begin()
        {
            if (HasStarted)
                return;
            HasStarted = true;
            girl.EndControlledDrag();
            startAngles = new float[girl.Model.Parts.Count];
            for (int i = 0; i < startAngles.Length; i++)
                startAngles[i] = girl.Model.Parts[i].Angle * Mathf.Rad2Deg;
            startTorso = girl.Model.Parts[0].Position;
            foreach (GameObject face in neutralFace)
                face.SetActive(false);
            happyFace.SetActive(true);
            birthdayText.SetActive(true);
            cakeGlow.SetActive(true);
            foreach (GameObject flame in flames)
                flame.SetActive(true);
            fadeBlock = new MaterialPropertyBlock();
            float depth = Vector3.Dot(Vector3.zero - sceneCamera.transform.position, sceneCamera.transform.forward);
            for (int i = 0; i < confetti.Length; i++)
            {
                ParticleSystem particles = confetti[i];
                bool left = i % 2 == 0;
                particles.transform.position = sceneCamera.ViewportToWorldPoint(new Vector3(left ? 0.025f : 0.975f, 0.10f, depth));
                particles.transform.rotation = Quaternion.LookRotation(new Vector3(left ? 0.8f : -0.8f, 0.6f, 0), Vector3.forward);
                var main = particles.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(confettiSpeed * 0.85f, confettiSpeed * 1.15f);
                main.gravityModifier = 0.55f;
                var emission = particles.emission;
                emission.rateOverTime = confettiRate;
                particles.Play(true);
            }
            balloonStarts = new Vector3[balloons.Length];
            for (int i = 0; i < balloons.Length; i++)
            {
                Transform balloon = balloons[i];
                balloonStarts[i] = sceneCamera.ViewportToWorldPoint(new Vector3(0.09f + i * 0.82f / Mathf.Max(1, balloons.Length - 1), -0.10f - i % 3 * 0.04f, depth));
                balloon.position = balloonStarts[i];
                balloon.gameObject.SetActive(true);
                foreach (SpriteRenderer sprite in balloon.GetComponentsInChildren<SpriteRenderer>())
                {
                    balloonSprites.Add(sprite);
                    balloonColors.Add(sprite.color);
                }
                foreach (LineRenderer line in balloon.GetComponentsInChildren<LineRenderer>())
                {
                    balloonStrings.Add(line);
                    stringColors.Add(line.startColor);
                }
            }
        }

        private void Update()
        {
            if (!HasStarted)
                return;
            age += Time.unscaledDeltaTime;
            if (!poseFinished)
                AnimateHappyPose();
            if (!effectsFinished)
                AnimateEffects();
        }

        private void AnimateHappyPose()
        {
            var model = girl.Model;
            float duration = Mathf.Max(0.01f, poseSeconds);
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / duration));
            float bounce = Mathf.Sin(Mathf.Clamp01((age - duration) / 0.8f) * Mathf.PI) * 0.18f;
            model.Parts[0].Position = Vector2.Lerp(startTorso, happyPose[0].position, blend) + Vector2.up * bounce;
            for (int i = 0; i < model.Parts.Count; i++)
                model.Parts[i].Angle = Mathf.LerpAngle(startAngles[i], happyPose[i].eulerAngles.z, blend) * Mathf.Deg2Rad;
            // Rebuild the same rigid joint tree at each angle, keeping every limb
            // connected throughout the explicit celebration animation.
            foreach (PoseRagdollModel.Joint joint in model.Joints)
            {
                if (joint.B == 1)
                    continue;
                var parent = model.Parts[joint.A];
                var child = model.Parts[joint.B];
                child.Position = parent.WorldPoint(joint.AnchorA) - child.Rotate(joint.AnchorB);
            }
            model.AlignHeadWithTorso();
            girl.RefreshPose();
            if (age >= duration + 0.8f)
                poseFinished = true;
        }

        private void AnimateEffects()
        {
            if (!emissionStopped && age >= effectHoldSeconds)
            {
                emissionStopped = true;
                foreach (ParticleSystem particles in confetti)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - effectHoldSeconds) / Mathf.Max(0.01f, effectFadeSeconds)));
            for (int i = 0; i < balloons.Length; i++)
            {
                float t = Mathf.Max(0, age - i * 0.04f);
                float phase = i * 2.399963f;
                float rise = balloonSpeed * (0.8f + i % 4 * 0.12f) * (t - 0.18f * (1 - Mathf.Exp(-t / 0.18f)));
                float drift = Mathf.Sin(phase) * (t - 0.35f * (1 - Mathf.Exp(-t / 0.35f)));
                float sway = (Mathf.Sin(phase + t * 2) - Mathf.Sin(phase)) * 0.12f;
                balloons[i].position = balloonStarts[i] + new Vector3(drift + sway, rise, 0);
                balloons[i].rotation = Quaternion.Euler(0, 0, Mathf.Sin(phase + t) * 5);
            }
            foreach (ParticleSystem particles in confetti)
            {
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.GetPropertyBlock(fadeBlock);
                fadeBlock.SetColor(Tint, new Color(1, 1, 1, alpha));
                renderer.SetPropertyBlock(fadeBlock);
            }
            for (int i = 0; i < balloonSprites.Count; i++)
            {
                Color color = balloonColors[i];
                color.a *= alpha;
                balloonSprites[i].color = color;
            }
            for (int i = 0; i < balloonStrings.Count; i++)
            {
                Color color = stringColors[i];
                color.a *= alpha;
                balloonStrings[i].startColor = balloonStrings[i].endColor = color;
            }
            if (alpha > 0)
                return;
            effectsFinished = true;
            foreach (ParticleSystem particles in confetti)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (Transform balloon in balloons)
                balloon.gameObject.SetActive(false);
            // The lit cake, chosen combination, birthday text and happy pose remain.
        }
    }
}
