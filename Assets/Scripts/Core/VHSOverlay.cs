using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ProjectS
{
    /// <summary>
    /// Full-screen VHS video overlay rendered ON TOP of everything (additive: black pixels are invisible, the
    /// grain/scanlines/tracking add over the game). Loops. Toggleable — SetVisible(false) for the "Reduce
    /// Flashing" accessibility option and the eyes-off nightmare mode. Assign the clip via ProjectS > Set Up
    /// VHS + 4:3. Persists across scene loads (DontDestroyOnLoad).
    /// </summary>
    public class VHSOverlay : MonoBehaviour
    {
        public static VHSOverlay Instance { get; private set; }

        [SerializeField] private VideoClip _clip;
        [SerializeField, Range(0f, 1f)] private float _opacity = 1f;
        [SerializeField] private float _targetAspect = 4f / 3f; // match the 4:3 game view (not the full screen)

        private VideoPlayer _player;
        private RawImage _image;
        private RenderTexture _rt;
        private bool _shown = true;
        private bool _forcedOff; // Reduce-Flashing / nightmare mode master switch

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
        }

        private void Update()
        {
            // Only overlay while actually PLAYING — the menu / win-lose screens are clean full-screen.
            bool playing = GameState.Instance != null && GameState.Instance.State == GameState.RunState.Playing;
            bool want = playing && !_forcedOff;
            if (want != _shown) { _shown = want; SetVisible(want); }
            if (_shown) LayoutBox();
        }

        // Size the overlay to a centred 4:3 box (matching the game's pillarbox) instead of the full wide screen.
        private void LayoutBox()
        {
            if (_image == null) return;
            float w = Screen.width, h = Screen.height;
            float boxW, boxH;
            if (w / h > _targetAspect) { boxH = h; boxW = h * _targetAspect; } // wide screen → pillarbox
            else { boxW = w; boxH = w / _targetAspect; }                       // tall screen → letterbox
            _image.rectTransform.sizeDelta = new Vector2(boxW, boxH);
            _image.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void Build()
        {
            if (_clip == null) { Debug.LogWarning("[VHS] No clip assigned — drop the mp4 in Assets/_Project/Video and re-run Set Up VHS."); return; }

            _rt = new RenderTexture(Mathf.Max(2, (int)_clip.width), Mathf.Max(2, (int)_clip.height), 0);

            var canvasGo = new GameObject("VHSCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above all UI

            var imgGo = new GameObject("VHSImage");
            imgGo.transform.SetParent(canvasGo.transform, false);
            _image = imgGo.AddComponent<RawImage>();
            _image.raycastTarget = false;
            // Centred, explicitly sized each frame to a 4:3 box (see LayoutBox) so it aligns with the game view.
            var r = _image.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            _image.texture = _rt;

            // Additive material (black → transparent). Falls back to plain alpha if the shader is missing.
            var shader = Shader.Find("ProjectS/VHSAdditive");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetFloat("_Opacity", _opacity);
                _image.material = mat;
            }
            else
            {
                // Additive shader missing (e.g. stripped from a device build) — DON'T render the black-backed
                // video at full opacity or it covers the whole game. Faint alpha as a safety net. Real fix:
                // add ProjectS/VHSAdditive to Graphics > Always Included Shaders.
                _image.color = new Color(1f, 1f, 1f, 0.2f);
                Debug.LogWarning("[VHS] ProjectS/VHSAdditive shader not found (stripped?). Using a faint fallback — " +
                                 "add it to Graphics > Always Included Shaders so the black background stays transparent.");
            }

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.clip = _clip;
            _player.isLooping = true;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _rt;
            _player.audioOutputMode = VideoAudioOutputMode.None; // silent overlay
            _player.playOnAwake = true;
            _player.Play();
        }

        private void SetVisible(bool on)
        {
            if (_image != null) _image.enabled = on;
            if (_player == null) return;
            if (on) _player.Play(); else _player.Pause();
        }

        /// <summary>Master off-switch for the "Reduce Flashing" option + the eyes-off nightmare mode. When
        /// suppressed, the overlay never shows even while playing.</summary>
        public void SetSuppressed(bool suppressed) => _forcedOff = suppressed;
    }
}
