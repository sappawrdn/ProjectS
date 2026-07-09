using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectS
{
    /// <summary>
    /// The main-menu flow, built at runtime: a cold-open haptic pulse → the ONBOARDING video (warning + use
    /// headphones; auto-advances when it ends) → the MENU video (loops; START/SETTINGS are baked into the video)
    /// with INVISIBLE tap buttons placed over that baked text. START loads the game scene. Videos are fit to
    /// their own aspect (no stretch); the tap-button rects are normalised in the video box so they follow it.
    /// Nudge _startRect / _settingsRect from a screenshot if the taps don't line up with the text.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private VideoClip _onboarding;
        [SerializeField] private VideoClip _menu;
        [SerializeField] private string _gameScene = "PlacedObjects";

        [Header("Invisible tap zones over the baked text (normalised in the video box, y from the TOP)")]
        [SerializeField] private Rect _startRect = new Rect(0.12f, 0.55f, 0.32f, 0.12f);
        [SerializeField] private Rect _settingsRect = new Rect(0.12f, 0.71f, 0.40f, 0.12f);

        private VideoPlayer _player;
        private RawImage _image;
        private RenderTexture _rt;
        private Button _startBtn, _settingsBtn;
        private Action _pendingEnd;
        private GameObject _settingsPanel;
        private Text _nightmareLabel;

        private void Start()
        {
            HapticManager.Instance?.ColdOpenPulse(); // one deep thump in the dark
            BuildUI();
            PlayClip(_onboarding, loop: false, onEnd: ShowMenu);
        }

        private void Update()
        {
            if (_image != null && _rt != null) FitBox();
        }

        private void BuildUI()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>(); // New Input System → touch/click UI
            }

            var canvasGo = new GameObject("MenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Black background (letterbox bars).
            var bg = new GameObject("BG").AddComponent<RawImage>();
            bg.transform.SetParent(canvasGo.transform, false);
            bg.color = Color.black; bg.raycastTarget = false;
            StretchFull(bg.rectTransform);

            // The video, fit to its own aspect, centred.
            _image = new GameObject("Video").AddComponent<RawImage>();
            _image.transform.SetParent(canvasGo.transform, false);
            _image.raycastTarget = false;
            _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = _image.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Invisible tap buttons (children of the video box → they scale/position with the video).
            _startBtn = MakeButton("StartBtn", _startRect, StartGame);
            _settingsBtn = MakeButton("SettingsBtn", _settingsRect, OnSettings);
            _startBtn.gameObject.SetActive(false);
            _settingsBtn.gameObject.SetActive(false);
        }

        private static void StretchFull(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private Button MakeButton(string name, Rect r, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_image.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // invisible, still raycastable
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(r.x, 1f - (r.y + r.height)); // rect y is top-down → flip to UI bottom-up
            rt.anchorMax = new Vector2(r.x + r.width, 1f - r.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            return btn;
        }

        private void PlayClip(VideoClip clip, bool loop, Action onEnd)
        {
            if (clip == null) { onEnd?.Invoke(); return; }

            if (_rt != null) _rt.Release();
            _rt = new RenderTexture(Mathf.Max(2, (int)clip.width), Mathf.Max(2, (int)clip.height), 0);
            // Clear to BLACK first — a fresh RenderTexture is uninitialised grey otherwise (the grey flash).
            var prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prevActive;
            _image.texture = _rt;

            if (_player == null) _player = gameObject.AddComponent<VideoPlayer>();
            _player.Stop();
            _player.clip = clip;
            _player.isLooping = loop;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _rt;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.skipOnDrop = false;       // play EVERY frame from the start (don't skip the intro typing)
            _player.waitForFirstFrame = true;

            _player.loopPointReached -= OnClipEnd;
            _pendingEnd = null;
            if (!loop && onEnd != null) { _pendingEnd = onEnd; _player.loopPointReached += OnClipEnd; }

            // Prepare first, then start from frame 0 — avoids the startup hitch that skips the opening frames.
            _player.prepareCompleted -= OnPrepared;
            _player.prepareCompleted += OnPrepared;
            _player.Prepare();
            FitBox();
        }

        private void OnPrepared(VideoPlayer vp)
        {
            vp.prepareCompleted -= OnPrepared;
            vp.frame = 0;
            vp.Play();
        }

        private void OnClipEnd(VideoPlayer vp)
        {
            vp.loopPointReached -= OnClipEnd;
            var e = _pendingEnd; _pendingEnd = null;
            e?.Invoke();
        }

        private void ShowMenu()
        {
            PlayClip(_menu, loop: true, onEnd: null);
            if (_startBtn) _startBtn.gameObject.SetActive(true);
            if (_settingsBtn) _settingsBtn.gameObject.SetActive(true);
        }

        private void StartGame()
        {
            GameState.AutoBeginNextLoad = true; // drop straight into the run (skip the greybox menu)
            SceneManager.LoadScene(_gameScene);
        }

        private void OnSettings()
        {
            if (_settingsPanel == null) BuildSettingsPanel();
            _settingsPanel.SetActive(true);
            RefreshNightmareLabel();
        }

        private void BuildSettingsPanel()
        {
            var canvas = _image.canvas;
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(canvas.transform, false);
            var bg = _settingsPanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.92f);
            bg.raycastTarget = true; // block taps to the menu behind
            StretchFull(bg.rectTransform);

            MakeLabel("SETTINGS", 54, new Vector2(0.5f, 0.82f), _settingsPanel.transform);

            var toggle = MakeMenuButton(ToggleText(), new Vector2(0.5f, 0.55f), _settingsPanel.transform);
            _nightmareLabel = toggle.GetComponentInChildren<Text>();
            toggle.onClick.AddListener(() => { HapticPrimaryController.Instance?.Toggle(); RefreshNightmareLabel(); });

            var back = MakeMenuButton("BACK", new Vector2(0.5f, 0.28f), _settingsPanel.transform);
            back.onClick.AddListener(() => _settingsPanel.SetActive(false));
        }

        private static string ToggleText()
        {
            bool on = HapticPrimaryController.Instance != null && HapticPrimaryController.Instance.Enabled;
            return "NIGHTMARE (EYES-OFF): " + (on ? "ON" : "OFF");
        }

        private void RefreshNightmareLabel()
        {
            if (_nightmareLabel != null) _nightmareLabel.text = ToggleText();
        }

        private static Text MakeLabel(string text, int size, Vector2 anchor, Transform parent)
        {
            var t = new GameObject("Label").AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.text = text; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.raycastTarget = false;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.sizeDelta = new Vector2(920f, 120f);
            r.anchoredPosition = Vector2.zero;
            return t;
        }

        private static Button MakeMenuButton(string label, Vector2 anchor, Transform parent)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.02f, 0.02f, 0.95f); // dark red
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.sizeDelta = new Vector2(780f, 100f);
            r.anchoredPosition = Vector2.zero;
            var btn = go.AddComponent<Button>();
            var t = MakeLabel(label, 34, new Vector2(0.5f, 0.5f), go.transform);
            t.rectTransform.sizeDelta = new Vector2(760f, 90f);
            return btn;
        }

        // Fit the video box to the clip's own aspect (no stretch), centred, with black bars around it.
        private void FitBox()
        {
            float clipAspect = (float)_rt.width / Mathf.Max(1, _rt.height);
            float w = Screen.width, h = Screen.height, boxW, boxH;
            if (w / h > clipAspect) { boxH = h; boxW = h * clipAspect; }
            else { boxW = w; boxH = w / clipAspect; }
            _image.rectTransform.sizeDelta = new Vector2(boxW, boxH);
            _image.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
