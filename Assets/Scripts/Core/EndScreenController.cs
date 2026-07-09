using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

namespace ProjectS
{
    /// <summary>
    /// The end-of-run video: on WON it plays "Escaped", on LOST it plays "Game Over" (loaded from
    /// Resources/EndVideos so no scene wiring is needed — never touches the game scene), then returns to the main
    /// menu when the clip finishes. Bootstrapped + DontDestroyOnLoad; fires once per run and re-arms when the next
    /// run starts. Renders above everything (incl. the Nightmare black-out).
    /// </summary>
    public class EndScreenController : MonoBehaviour
    {
        [SerializeField] private string _menuScene = "MainMenu";
        [SerializeField] private string _escapedClip = "EndVideos/Escaped";
        [SerializeField] private string _gameOverClip = "EndVideos/GameOver";

        public static EndScreenController Instance { get; private set; }

        private VideoPlayer _player;
        private RawImage _image;
        private RenderTexture _rt;
        private Canvas _canvas;
        private bool _played; // one end video per run

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("EndScreen").AddComponent<EndScreenController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_player != null) { FitBox(); return; } // the end video is playing — just keep it fit

            var gs = GameState.Instance;
            if (gs == null) return;

            if (gs.State == GameState.RunState.Playing) { _played = false; return; } // a fresh run → re-arm
            if (_played) return;

            if (gs.State == GameState.RunState.Won) Play(_escapedClip);
            else if (gs.State == GameState.RunState.Lost) Play(_gameOverClip);
        }

        private void Play(string resourcePath)
        {
            _played = true;
            var clip = Resources.Load<VideoClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"[EndScreen] '{resourcePath}' not found in Resources — going straight to the menu.");
                SceneManager.LoadScene(_menuScene);
                return;
            }
            BuildOverlay();

            _rt = new RenderTexture(Mathf.Max(2, (int)clip.width), Mathf.Max(2, (int)clip.height), 0);
            var prev = RenderTexture.active;
            RenderTexture.active = _rt; GL.Clear(true, true, Color.black); RenderTexture.active = prev;
            _image.texture = _rt;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.clip = clip;
            _player.isLooping = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _rt;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.skipOnDrop = false;
            _player.waitForFirstFrame = true;
            _player.loopPointReached += OnEnd;
            _player.prepareCompleted += vp => { vp.frame = 0; vp.Play(); };
            _player.Prepare();
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("EndCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 34000; // above the game, letterbox, VHS, and the Nightmare black-out

            var bg = new GameObject("BG").AddComponent<RawImage>();
            bg.transform.SetParent(canvasGo.transform, false);
            bg.color = Color.black; bg.raycastTarget = false;
            var br = bg.rectTransform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;

            _image = new GameObject("Video").AddComponent<RawImage>();
            _image.transform.SetParent(canvasGo.transform, false);
            _image.raycastTarget = false;
            _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = _image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        // Fit the video to its own aspect (no stretch), centred, black bars around it.
        private void FitBox()
        {
            if (_image == null || _rt == null) return;
            float clipAspect = (float)_rt.width / Mathf.Max(1, _rt.height);
            float w = Screen.width, h = Screen.height, boxW, boxH;
            if (w / h > clipAspect) { boxH = h; boxW = h * clipAspect; }
            else { boxW = w; boxH = w / clipAspect; }
            _image.rectTransform.sizeDelta = new Vector2(boxW, boxH);
            _image.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void OnEnd(VideoPlayer vp)
        {
            Cleanup();
            MainMenuController.SkipOnboarding = true; // run-end → straight to the menu, no onboarding replay
            SceneManager.LoadScene(_menuScene);
        }

        private void Cleanup()
        {
            if (_player != null) { _player.loopPointReached -= OnEnd; Destroy(_player); _player = null; }
            if (_rt != null) { _rt.Release(); _rt = null; }
            if (_canvas != null) { Destroy(_canvas.gameObject); _canvas = null; }
            // _played stays true; it re-arms when the next run reaches Playing.
        }
    }
}
