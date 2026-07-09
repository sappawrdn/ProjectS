using UnityEngine;
using UnityEngine.UI;

namespace ProjectS
{
    /// <summary>
    /// The eyes-off "Nightmare Mode" (GDD's Haptic-Primary — the headline accessibility feature). A single
    /// persisted toggle (PlayerPrefs) that the SETTINGS screen flips. When ON and the run is playing it:
    ///   • blacks out the screen (you navigate by EAR + TOUCH),
    ///   • boosts the danger heartbeat (×1.25 so proximity is felt harder),
    ///   • hides the VHS overlay (nothing to see).
    /// The control swap (gyro-aim, hold-to-walk) + compass/auto-pickup arrive in the next chunks — this is the
    /// foundation (toggle + black-out). Self-bootstraps + persists across scenes.
    /// </summary>
    public class HapticPrimaryController : MonoBehaviour
    {
        public static HapticPrimaryController Instance { get; private set; }
        private const string PrefKey = "HapticPrimaryEnabled";

        [SerializeField] private float _dangerBoost = 1.25f;

        [Header("Objective compass — tick toward the nearest key, then the exit (silent when facing away)")]
        [SerializeField] private float _alignThreshold = 0.5f; // dot(facing, toTarget) above this = warm cone
        [SerializeField] private float _tickHot = 0.12f;       // tick period when dead-on
        [SerializeField] private float _tickWarm = 0.6f;       // tick period at the warm edge
        [SerializeField] private float _grabRange = 1.8f;      // within this of a key → grab buzz (a touch > pickup radius)
        [SerializeField] private float _grabBuzzPeriod = 0.2f;

        public bool Enabled { get; private set; }

        private Image _blackout;
        private Camera _cam;
        private Transform _exit;
        private float _tickTimer, _grabTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("HapticPrimary").AddComponent<HapticPrimaryController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Enabled = PlayerPrefs.GetInt(PrefKey, 0) == 1;
            BuildBlackout();
        }

        /// <summary>Called by the SETTINGS toggle. Persists across sessions.</summary>
        public void SetEnabled(bool on)
        {
            Enabled = on;
            PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Toggle() => SetEnabled(!Enabled);

        private void BuildBlackout()
        {
            var canvasGo = new GameObject("NightmareBlackout");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 33000; // above everything (game, letterbox, VHS)

            _blackout = new GameObject("Black").AddComponent<Image>();
            _blackout.transform.SetParent(canvasGo.transform, false);
            _blackout.color = Color.black;
            _blackout.raycastTarget = false; // never eat taps (hold-to-walk needs them, next chunk)
            var r = _blackout.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            _blackout.enabled = false;
        }

        private void Update()
        {
            bool playing = GameState.Instance != null && GameState.Instance.State == GameState.RunState.Playing;
            bool active = Enabled && playing; // black out only while actually playing (menu stays visible)

            if (_blackout != null) _blackout.enabled = active;
            HapticManager.Instance?.SetDangerBoost(active ? _dangerBoost : 1f);
            VHSOverlay.Instance?.SetSuppressed(Enabled); // no VHS in eyes-off mode

            if (active) CompassUpdate(Time.deltaTime);
        }

        // The eyes-off objective compass: crisp ticks toward the nearest uncollected key (then the exit) — faster
        // the more you face it, SILENT when facing away (so "wrong way" is unmistakable). Right on a key → a soft
        // grab buzz instead (it auto-collects by proximity + fires Confirm).
        private void CompassUpdate(float dt)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Transform target = NearestKey(out float keyDist);
            if (target != null && keyDist <= _grabRange)
            {
                _grabTimer -= dt;
                if (_grabTimer <= 0f) { HapticManager.Instance?.GrabBuzz(); _grabTimer = _grabBuzzPeriod; }
                _tickTimer = 0f;
                return;
            }

            if (target == null) target = FindExit(); // all keys found → head for the exit
            if (target == null) return;

            Vector3 to = target.position - _cam.transform.position; to.y = 0f;
            Vector3 fwd = _cam.transform.forward; fwd.y = 0f;
            if (to.sqrMagnitude < 1e-4f || fwd.sqrMagnitude < 1e-4f) return;

            float dot = Vector3.Dot(fwd.normalized, to.normalized);
            if (dot < _alignThreshold) { _tickTimer = 0f; return; } // facing away → silent

            float period = Mathf.Lerp(_tickWarm, _tickHot, Mathf.InverseLerp(_alignThreshold, 1f, dot));
            _tickTimer -= dt;
            if (_tickTimer <= 0f) { HapticManager.Instance?.CompassTick(); _tickTimer = period; }
        }

        private Transform NearestKey(out float dist)
        {
            dist = float.MaxValue;
            Transform best = null;
            Vector3 p = _cam.transform.position;
            foreach (var k in FindObjectsByType<Key>(FindObjectsSortMode.None)) // collected keys are inactive → skipped
            {
                float d = Vector3.Distance(p, k.transform.position);
                if (d < dist) { dist = d; best = k.transform; }
            }
            return best;
        }

        private Transform FindExit()
        {
            if (_exit == null)
            {
                var go = GameObject.Find("ExitDoor") ?? GameObject.Find("Exit");
                if (go != null) _exit = go.transform;
            }
            return _exit;
        }
    }
}
