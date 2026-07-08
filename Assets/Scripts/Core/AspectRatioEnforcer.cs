using UnityEngine;
using UnityEngine.UI;

namespace ProjectS
{
    /// <summary>
    /// Forces a 4:3 (retro/VHS) view by drawing black UI bars over the sides of a full-screen render — NOT by
    /// touching the camera's viewport rect or adding a second camera. The old camera-rect + letterbox-camera
    /// approach rendered fine in the editor but went BLACK on device (URP render-scale + viewport-rect quirk),
    /// so this UI-bar approach is used instead (pure Screen-Space-Overlay, device-safe). The game camera renders
    /// full-screen; the bars crop it to a centred 4:3 window — same field of view as a native 4:3 camera.
    /// Add to the main camera. Sits below the VHS overlay (which is sized to the same 4:3 box).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class AspectRatioEnforcer : MonoBehaviour
    {
        [SerializeField] private float _targetAspect = 4f / 3f;

        private RectTransform _left, _right, _top, _bottom;

        private void Awake() => BuildBars();
        private void Update() => LayoutBars();

        private void BuildBars()
        {
            var go = new GameObject("LetterboxCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // above the game, below the VHS overlay (32000)

            _left = MakeBar(go.transform);
            _right = MakeBar(go.transform);
            _top = MakeBar(go.transform);
            _bottom = MakeBar(go.transform);
        }

        private static RectTransform MakeBar(Transform parent)
        {
            var img = new GameObject("Bar").AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = Color.black;
            img.raycastTarget = false;
            return img.rectTransform;
        }

        private void LayoutBars()
        {
            if (_left == null) return;
            float w = Screen.width, h = Screen.height, boxW, boxH;
            if (w / h > _targetAspect) { boxH = h; boxW = h * _targetAspect; } // wide → pillarbox
            else { boxW = w; boxH = w / _targetAspect; }                       // tall → letterbox
            float barW = (w - boxW) / 2f;
            float barH = (h - boxH) / 2f;

            SetBar(_left, new Vector2(0f, 0.5f), new Vector2(barW, h));   // left strip, full height
            SetBar(_right, new Vector2(1f, 0.5f), new Vector2(barW, h));  // right strip
            SetBar(_top, new Vector2(0.5f, 1f), new Vector2(w, barH));    // top strip, full width
            SetBar(_bottom, new Vector2(0.5f, 0f), new Vector2(w, barH)); // bottom strip
        }

        private static void SetBar(RectTransform r, Vector2 anchor, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.sizeDelta = size;
            r.anchoredPosition = Vector2.zero;
        }
    }
}
