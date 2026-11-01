using UnityEngine;
using UnityEngine.UI;

namespace PinkDream
{
    // Keeps authored UI in the same world layout in Scene and Game views.
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]
    public sealed class ScenePresentation : MonoBehaviour
    {
        [System.Serializable] public sealed class CanvasLayout
        {
            public Canvas canvas;
            public CanvasScaler scaler;
            public float distance = 1f;
        }
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private CanvasLayout[] canvasLayouts;
        private Color lastColor;
        private bool colorInitialized;
        public Camera SceneCamera => sceneCamera;
        private void OnEnable() { Refresh(); }
        private void OnValidate() { Refresh(); }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (sceneCamera == null || background == null || background.sprite == null) return;
            if (!colorInitialized) { lastColor = background.color; colorInitialized = true; }
            if (background.color != lastColor) sceneCamera.backgroundColor = background.color;
            else if (sceneCamera.backgroundColor != lastColor) background.color = sceneCamera.backgroundColor;
            lastColor = background.color;
            float pixelWidth = Mathf.Max(1, sceneCamera.pixelWidth), pixelHeight = Mathf.Max(1, sceneCamera.pixelHeight);
            float worldHeight = sceneCamera.orthographicSize * 2;
            float aspect = pixelWidth / pixelHeight;
            float depth = Mathf.Min(20, sceneCamera.farClipPlane * .5f);
            background.transform.SetPositionAndRotation(sceneCamera.ViewportToWorldPoint(new Vector3(.5f,.5f,depth)), sceneCamera.transform.rotation);
            background.transform.localScale = new Vector3(worldHeight * aspect / background.sprite.bounds.size.x, worldHeight / background.sprite.bounds.size.y, 1);
            if (canvasLayouts == null) return;
            foreach (CanvasLayout layout in canvasLayouts)
            {
                if (layout.canvas == null) continue;
                Vector2 reference = layout.scaler != null ? layout.scaler.referenceResolution : new Vector2(1600,900);
                float match = layout.scaler != null ? layout.scaler.matchWidthOrHeight : .5f;
                float factor = Mathf.Pow(2, Mathf.Lerp(Mathf.Log(pixelWidth / reference.x,2), Mathf.Log(pixelHeight / reference.y,2), match));
                var rect = (RectTransform)layout.canvas.transform;
                rect.sizeDelta = new Vector2(pixelWidth / factor, pixelHeight / factor);
                rect.SetPositionAndRotation(sceneCamera.ViewportToWorldPoint(new Vector3(.5f,.5f,Mathf.Max(sceneCamera.nearClipPlane+.05f,layout.distance))), sceneCamera.transform.rotation);
                rect.localScale = Vector3.one * (worldHeight / pixelHeight * factor);
            }
        }
    }
}
