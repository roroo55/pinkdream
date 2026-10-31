using System.Collections.Generic;
using UnityEngine;

namespace PinkDream
{
    // Optical view only. No character poses, colliders or item transforms are changed.
    public sealed class WorldMagnifier : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera, captureCamera;
        [SerializeField] private Transform lensCenter;
        [SerializeField] private GameObject[] excludedVisuals;
        [SerializeField, Min(.01f)] private float lensRadius = .6f;
        [SerializeField, Min(1)] private float magnification = 2f;
        [SerializeField] private int informationLayer = 2;
        private Renderer[] excludedRenderers;
        private Canvas[] excludedCanvases;
        private bool[] rendererStates, canvasStates;
        private bool rendering;

        private void Awake() { Initialize(); captureCamera.enabled = false; }
        // Refresh the image when an object moves beneath a stationary lens too.
        // This updates only the offscreen camera, never an animation bone.
        private void LateUpdate() { CaptureView(); }
        private void Initialize()
        {
            if (excludedRenderers != null) return;
            var renderers = new HashSet<Renderer>(); var canvases = new HashSet<Canvas>();
            foreach (GameObject visual in excludedVisuals)
            {
                if (visual == null) continue;
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) renderers.Add(renderer);
                foreach (Canvas canvas in visual.GetComponentsInChildren<Canvas>(true)) canvases.Add(canvas);
            }
            excludedRenderers = new Renderer[renderers.Count]; renderers.CopyTo(excludedRenderers);
            excludedCanvases = new Canvas[canvases.Count]; canvases.CopyTo(excludedCanvases);
            rendererStates = new bool[excludedRenderers.Length]; canvasStates = new bool[excludedCanvases.Length];
        }
        public void RenderView(float radius, float zoom)
        { lensRadius = Mathf.Max(.01f, radius); magnification = Mathf.Max(1, zoom); CaptureView(); }
        private void CaptureView()
        {
            if (rendering || !isActiveAndEnabled || worldCamera == null || captureCamera == null || captureCamera.targetTexture == null) return;
            Initialize(); rendering = true;
            try
            {
                // Hide the lens itself and the hint covers in this offscreen pass.
                // Preserve enabled states exactly; no objects or scripts are deactivated.
                for (int i = 0; i < excludedRenderers.Length; i++) if (excludedRenderers[i] != null)
                { rendererStates[i] = excludedRenderers[i].enabled; excludedRenderers[i].enabled = false; }
                for (int i = 0; i < excludedCanvases.Length; i++) if (excludedCanvases[i] != null)
                { canvasStates[i] = excludedCanvases[i].enabled; excludedCanvases[i].enabled = false; }
                captureCamera.enabled = false;
                captureCamera.cullingMask = worldCamera.cullingMask | (1 << informationLayer);
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = worldCamera.backgroundColor;
                captureCamera.transform.SetPositionAndRotation(new Vector3(lensCenter.position.x, lensCenter.position.y, worldCamera.transform.position.z), worldCamera.transform.rotation);
                captureCamera.orthographicSize = lensRadius / magnification;
                if (!captureCamera.targetTexture.IsCreated()) captureCamera.targetTexture.Create();
                captureCamera.Render();
            }
            finally
            {
                for (int i = 0; i < excludedRenderers.Length; i++) if (excludedRenderers[i] != null) excludedRenderers[i].enabled = rendererStates[i];
                for (int i = 0; i < excludedCanvases.Length; i++) if (excludedCanvases[i] != null) excludedCanvases[i].enabled = canvasStates[i];
                rendering = false;
            }
        }
    }
}
