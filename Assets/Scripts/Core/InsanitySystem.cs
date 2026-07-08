using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectS
{
    /// <summary>
    /// The fear channel (architecture.md). insanity (0..1) rises near the monster and while moving, and
    /// decays slowly when calm. It drives the screen-edge vignette (breathes with the heartbeat rate) and
    /// modulates the monster per tier (wider sight, quicker Watcher). It is NEVER a meter — the vignette +
    /// (on-device) haptic heartbeat are the only fear cues. Values are playtested [SerializeField]s.
    ///
    /// The heartbeat itself is Core Haptics on device (see HeartbeatHapticTest); here we expose HeartbeatBpm
    /// so that (and an audio heartbeat) can be driven from the same insanity value.
    /// </summary>
    public class InsanitySystem : MonoBehaviour
    {
        [Header("Accumulation / decay (architecture.md, per second)")]
        [SerializeField] private float _nearRadius = 6f;
        [SerializeField] private float _nearGain = 0.22f;   // × closeness (0..1) when within nearRadius
        [SerializeField] private float _moveGain = 0.06f;   // while moving
        [SerializeField] private float _idleDecay = 0.07f;  // while not moving
        [SerializeField] private float _moveThreshold = 0.1f; // m/s to count as "moving"

        [Header("Heartbeat + vignette")]
        [SerializeField] private float _baseBpm = 60f;
        [SerializeField] private float _bpmPerInsanity = 90f;   // BPM = 60 + 90*insanity
        [SerializeField] private float _calmVignette = 0.30f;   // opacity at insanity 0
        [SerializeField] private float _panicVignette = 0.75f;  // opacity at insanity 1

        [Header("Debug")]
        [SerializeField] private bool _showDebug = true;

        public float Insanity { get; private set; }
        public float HeartbeatBpm => _baseBpm + _bpmPerInsanity * Insanity;

        /// <summary>Scares spike fear instantly (never lowers it).</summary>
        public void Spike(float value) => Insanity = Mathf.Max(Insanity, Mathf.Clamp01(value));

        private Transform _player;
        private MonsterAI _monster;
        private Vector3 _lastPlayerPos;
        private float _breathPhase;

        private Volume _volume;
        private Vignette _vignette;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { _player = p.transform; _lastPlayerPos = _player.position; }
            _monster = FindFirstObjectByType<MonsterAI>();

            SetupVignette();
        }

        private void SetupVignette()
        {
            // Enable post-processing on the main camera (a bare Camera has it off by default).
            var cam = Camera.main;
            if (cam != null)
            {
                var camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null) camData.renderPostProcessing = true;
            }

            // Isolated runtime volume so we never dirty a shared profile asset.
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            _vignette = _volume.profile.Add<Vignette>(true);
            _vignette.color.overrideState = true;
            _vignette.color.value = Color.black;
            _vignette.intensity.overrideState = true;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 1f;

            // Bloom so bright emissives (EXIT signs, ceiling lights) glow like the reference.
            var bloom = _volume.profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.85f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 1.6f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.75f;
        }

        private void Update()
        {
            if (_player == null) return;

            UpdateInsanity();
            _monster?.SetInsanity(Insanity);
            HapticManager.Instance?.SetInsanity(Insanity); // same fear value drives the on-device heartbeat
            UpdateVignette();
        }

        private void UpdateInsanity()
        {
            // Movement (horizontal only).
            Vector3 flatDelta = _player.position - _lastPlayerPos;
            flatDelta.y = 0f;
            bool moving = flatDelta.magnitude / Mathf.Max(Time.deltaTime, 1e-4f) > _moveThreshold;
            _lastPlayerPos = _player.position;

            float delta = 0f;

            // Proximity to the monster (only counts a live threat, not a dormant Static one).
            // DISABLED FOR PLAYTESTING: Monster proximity no longer raises insanity
            /*
            if (_monster != null && _monster.Tier != MonsterTier.Static)
            {
                float dist = Vector3.Distance(_player.position, _monster.transform.position);
                if (dist < _nearRadius)
                    delta += ((_nearRadius - dist) / _nearRadius) * _nearGain;
            }
            */
            // DISABLED FOR PLAYTESTING: Walking no longer raises insanity
            // delta += moving ? _moveGain : -_idleDecay;
            delta += -_idleDecay; // Only decay, no gain from moving

            Insanity = Mathf.Clamp01(Insanity + delta * Time.deltaTime);
        }

        private void UpdateVignette()
        {
            if (_vignette == null) return;

            // Breathe at the heartbeat rate; peak opacity scales with insanity.
            _breathPhase += Time.deltaTime * (HeartbeatBpm / 60f) * 2f * Mathf.PI;
            float breathe = Mathf.Sin(_breathPhase) * 0.5f + 0.5f; // 0..1
            float peak = Mathf.Lerp(_calmVignette, _panicVignette, Insanity);
            _vignette.intensity.value = Mathf.Lerp(peak * 0.6f, peak, breathe);
        }

        private void OnGUI()
        {
            if (!_showDebug) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            // Respect the notch/safe area (landscape iPhone) so the text isn't clipped by the screen edge.
            float x = Screen.safeArea.x + 12f;
            float y = (Screen.height - Screen.safeArea.yMax) + 44f; // below the GameState readout
            GUI.Label(new Rect(x, y, 500, 24), $"Insanity {Insanity:0.00}    Heartbeat {HeartbeatBpm:0} BPM", style);
        }
    }
}
