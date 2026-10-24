using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream
{
    /// <summary>Only the completed level's celebration, dialogue and scene exit.</summary>
    public sealed class BirthCompletionSequence : MonoBehaviour
    {
        [Header("场景中预放的礼花与气球")]
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private ParticleSystem[] confetti;
        [SerializeField] private Transform[] balloons;
        [Header("两侧礼花与底部气球的运动")]
        [InspectorName("礼花发射速度"), Min(0)]
        [SerializeField] private float confettiLaunchSpeed = 7.5f;
        [InspectorName("礼花每秒喷出数量（每个喷口）"), Min(0)]
        [SerializeField] private float confettiEmissionRate = 18f;
        [InspectorName("礼花起喷数量（每个喷口）"), Range(0, 100)]
        [SerializeField] private int confettiBurstCount = 12;
        [InspectorName("礼花重力强度"), Min(0)]
        [SerializeField] private float confettiGravity = 0.55f;
        [InspectorName("气球上升速度"), Min(0)]
        [SerializeField] private float balloonUpwardSpeed = 3.8f;
        [InspectorName("气球速度差异"), Range(0, 0.8f)]
        [SerializeField] private float balloonSpeedVariation = 0.3f;
        [InspectorName("气球横向飘散速度"), Min(0)]
        [SerializeField] private float balloonDriftSpeed = 1.4f;
        [InspectorName("气球轻摆幅度"), Min(0)]
        [SerializeField] private float balloonSway = 0.16f;
        [Header("礼花与气球时间")]
        [Tooltip("开始喷洒后保持多久，再开始减淡。默认 1 秒。")]
        [InspectorName("停留时间（秒）"), Min(0)]
        [SerializeField] private float celebrationHoldSeconds = 1f;
        [InspectorName("减淡消失时间（秒）"), Min(0)]
        [SerializeField] private float celebrationFadeSeconds = 0.85f;
        [Header("对话")]
        [SerializeField] private CanvasGroup dialogueGroup;
        [SerializeField] private Text dialogueText;
        [TextArea, SerializeField] private string dialogueLine = "哦，亲爱的，是女孩。";
        [InspectorName("对话出现时间（秒）"), Min(0)]
        [SerializeField] private float dialogueFadeSeconds = 0.25f;
        [InspectorName("对话停留时间（秒）"), Min(0)]
        [SerializeField] private float dialogueHoldSeconds = 2.5f;
        [Header("以游戏背景色转入独立空白场景")]
        [InspectorName("背景色转场遮罩")]
        [SerializeField] private CanvasGroup whiteCover;
        [InspectorName("画面淡出时间（秒）"), Min(0)]
        [SerializeField] private float sceneFadeSeconds = 0.7f;
        [SerializeField] private string transitionScene = "Transition_Blank";

        private readonly List<SpriteRenderer> balloonSprites = new List<SpriteRenderer>();
        private readonly List<Color> spriteColors = new List<Color>();
        private readonly List<LineRenderer> balloonStrings = new List<LineRenderer>();
        private readonly List<Color> stringStarts = new List<Color>();
        private readonly List<Color> stringEnds = new List<Color>();
        private MaterialPropertyBlock fadeProperties;
        private Vector3[] balloonReleasePositions;
        private Quaternion[] balloonReleaseRotations;
        private float balloonFlightTime;
        private bool effectsMoving;
        private static readonly int Tint = Shader.PropertyToID("_Color");
        public bool HasStarted { get; private set; }

        public void Begin()
        {
            if (HasStarted)
                return;
            HasStarted = true;
            fadeProperties = new MaterialPropertyBlock();
            CacheBalloonColors();
            foreach (ParticleSystem particles in confetti)
                if (particles != null)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ArrangeSideJetsAndBottomRelease();
            Image coverImage = whiteCover.GetComponent<Image>();
            if (coverImage != null)
                coverImage.color = sceneCamera.backgroundColor;
            SetEffectAlpha(1);
            foreach (ParticleSystem particles in confetti)
                if (particles != null)
                {
                    particles.Play(true);
                }
            foreach (Transform balloon in balloons)
                if (balloon != null)
                    balloon.gameObject.SetActive(true);
            effectsMoving = true;
            StartCoroutine(CompleteLevel());
        }

        private void Update()
        {
            if (!effectsMoving)
                return;
            balloonFlightTime += Time.unscaledDeltaTime;
            for (int i = 0; i < balloons.Length; i++)
            {
                if (balloons[i] == null)
                    continue;
                // Buoyancy accelerates a released balloon toward terminal speed;
                // each balloon has its own lateral drift, release time and sway.
                float t = Mathf.Max(0, balloonFlightTime - i * 0.018f);
                float phase = i * 2.399963f + 0.4f;
                float variation = 0.5f + 0.5f * Mathf.Sin(i * 1.879f + 0.7f);
                float upSpeed = balloonUpwardSpeed * Mathf.Lerp(1 - balloonSpeedVariation, 1 + balloonSpeedVariation, variation);
                float rise = upSpeed * (t - 0.16f * (1 - Mathf.Exp(-t / 0.16f)));
                float drift = Mathf.Sin(phase) * balloonDriftSpeed * (t - 0.35f * (1 - Mathf.Exp(-t / 0.35f)));
                float sway = (Mathf.Sin(t * 2.1f + phase) - Mathf.Sin(phase)) * balloonSway;
                balloons[i].position = balloonReleasePositions[i] + new Vector3(drift + sway, rise, 0);
                float tilt = (Mathf.Sin(t * 1.7f + phase) - Mathf.Sin(phase)) * 6f;
                balloons[i].rotation = balloonReleaseRotations[i] * Quaternion.Euler(0, 0, tilt);
            }
        }

        private void CacheBalloonColors()
        {
            foreach (Transform balloon in balloons)
            {
                if (balloon == null)
                    continue;
                foreach (SpriteRenderer sprite in balloon.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    balloonSprites.Add(sprite);
                    spriteColors.Add(sprite.color);
                }
                foreach (LineRenderer line in balloon.GetComponentsInChildren<LineRenderer>(true))
                {
                    balloonStrings.Add(line);
                    stringStarts.Add(line.startColor);
                    stringEnds.Add(line.endColor);
                }
            }
        }

        private void ArrangeSideJetsAndBottomRelease()
        {
            // Reposition authored objects only: confetti starts at the side
            // edges, while every balloon starts below the bottom of the picture.
            float depth = Vector3.Dot(Vector3.zero - sceneCamera.transform.position, sceneCamera.transform.forward);
            for (int i = 0; i < confetti.Length; i++)
            {
                if (confetti[i] == null)
                    continue;
                bool left = i % 2 == 0;
                int lane = i / 2;
                confetti[i].transform.position = sceneCamera.ViewportToWorldPoint(
                    new Vector3(left ? 0.025f : 0.975f, 0.10f + lane * 0.018f, depth));
                float angle = (28f + lane % 3 * 9f) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3((left ? 1 : -1) * Mathf.Cos(angle), Mathf.Sin(angle), 0);
                confetti[i].transform.rotation = Quaternion.LookRotation(direction, Vector3.forward);
                var main = confetti[i].main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(confettiLaunchSpeed * 0.85f, confettiLaunchSpeed * 1.15f);
                main.gravityModifier = confettiGravity;
                var emission = confetti[i].emission;
                emission.rateOverTime = confettiEmissionRate;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)confettiBurstCount) });
                var shape = confetti[i].shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 11;
                shape.radius = 0.10f;
                shape.scale = Vector3.one;
                var velocity = confetti[i].velocityOverLifetime;
                velocity.enabled = false;
            }
            balloonReleasePositions = new Vector3[balloons.Length];
            balloonReleaseRotations = new Quaternion[balloons.Length];
            for (int i = 0; i < balloons.Length; i++)
            {
                if (balloons[i] == null)
                    continue;
                float x = 0.12f + i % 9 * 0.095f;
                float y = -0.045f - i / 9 * 0.045f;
                balloons[i].position = sceneCamera.ViewportToWorldPoint(new Vector3(x, y, depth));
                balloonReleasePositions[i] = balloons[i].position;
                balloonReleaseRotations[i] = balloons[i].rotation;
            }
        }

        private IEnumerator CompleteLevel()
        {
            if (celebrationHoldSeconds > 0)
                yield return new WaitForSecondsRealtime(celebrationHoldSeconds);
            foreach (ParticleSystem particles in confetti)
                if (particles != null)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            float elapsed = 0;
            while (elapsed < celebrationFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetEffectAlpha(1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / celebrationFadeSeconds)));
                yield return null;
            }
            SetEffectAlpha(0);
            effectsMoving = false;
            foreach (ParticleSystem particles in confetti)
                if (particles != null)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (Transform balloon in balloons)
                if (balloon != null)
                    balloon.gameObject.SetActive(false);

            dialogueText.text = dialogueLine;
            dialogueGroup.alpha = 0;
            dialogueGroup.gameObject.SetActive(true);
            elapsed = 0;
            while (elapsed < dialogueFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                dialogueGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / dialogueFadeSeconds));
                yield return null;
            }
            dialogueGroup.alpha = 1;
            if (dialogueHoldSeconds > 0)
                yield return new WaitForSecondsRealtime(dialogueHoldSeconds);

            whiteCover.alpha = 0;
            whiteCover.gameObject.SetActive(true);
            elapsed = 0;
            while (elapsed < sceneFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / sceneFadeSeconds));
                whiteCover.alpha = alpha;
                dialogueGroup.alpha = 1 - alpha;
                yield return null;
            }
            whiteCover.alpha = 1;
            if (Application.CanStreamedLevelBeLoaded(transitionScene))
                yield return SceneManager.LoadSceneAsync(transitionScene, LoadSceneMode.Single);
            else
                Debug.LogError("The transition scene is missing from Build Settings: " + transitionScene, this);
        }

        private void SetEffectAlpha(float alpha)
        {
            foreach (ParticleSystem particles in confetti)
            {
                if (particles == null)
                    continue;
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.GetPropertyBlock(fadeProperties);
                fadeProperties.SetColor(Tint, new Color(1, 1, 1, alpha));
                renderer.SetPropertyBlock(fadeProperties);
            }
            for (int i = 0; i < balloonSprites.Count; i++)
            {
                Color color = spriteColors[i];
                color.a *= alpha;
                balloonSprites[i].color = color;
            }
            for (int i = 0; i < balloonStrings.Count; i++)
            {
                Color start = stringStarts[i], end = stringEnds[i];
                start.a *= alpha;
                end.a *= alpha;
                balloonStrings[i].startColor = start;
                balloonStrings[i].endColor = end;
            }
        }
    }
}
