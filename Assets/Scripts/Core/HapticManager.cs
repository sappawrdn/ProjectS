using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Apple.CoreHaptics;

namespace ProjectS
{
    /// <summary>
    /// The single place that speaks to Core Haptics (architecture.md: wrap the Apple plugins in one manager).
    /// Plays the game's haptic vocabulary — <b>heartbeat</b> (the fear channel; rate + intensity scale with
    /// insanity), <b>jumpscare</b> (a violent slam for scares), <b>confirm</b> (pickup done), <b>cold-open
    /// pulse</b> (a deep thump at run start). Built on the pattern proven on device in HeartbeatHapticTest.
    ///
    /// The editor + simulator have NO Taptic Engine, so every call no-ops there (guarded by
    /// HardwareSupportsHaptics) — the game still runs; the haptics must be FELT on a real iPhone. All tuning
    /// mirrors architecture.md's [SerializeField] numbers.
    ///
    /// The heartbeat runs on its own player; one-shots (jumpscare/confirm/pulse) use a separate player so a
    /// scare never clobbers the ongoing heartbeat.
    /// </summary>
    public class HapticManager : MonoBehaviour
    {
        public static HapticManager Instance { get; private set; }

        [Header("Heartbeat (rate + intensity scale with insanity) — architecture.md")]
        [SerializeField] private float _baseBpm = 60f;              // BPM = base + perInsanity * insanity
        [SerializeField] private float _bpmPerInsanity = 90f;       // -> calm 60, panic 150
        [SerializeField] private float _lubDubGapSeconds = 0.14f;   // gap between "lub" and "dub"
        [SerializeField, Range(0f, 1f)] private float _minHeartIntensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _maxHeartIntensity = 1.0f;
        [SerializeField, Range(0f, 1f)] private float _dubFalloff = 0.7f;       // the "dub" is softer than the "lub"
        [SerializeField, Range(0f, 1f)] private float _heartSharpness = 0.4f;   // deep/rounded, not a sharp click

        [Header("Haptic-Primary danger boost (×heartbeat intensity; 1.25 when eyes-off, GDD)")]
        [SerializeField] private float _dangerBoost = 1f;

        private CHHapticEngine _engine;
        private CHHapticPatternPlayer _beatPlayer;   // heartbeat loop
        private CHHapticPatternPlayer _oneShotPlayer; // jumpscare / confirm / pulse
        private CHHapticPatternPlayer _cuePlayer;     // compass tick / grab buzz (eyes-off) — own slot so ticks don't clobber a scare
        private bool _supported;
        private bool _beating;
        private float _insanity;
        private InsanitySystem _fear; // single source of truth for BPM (same value the vignette breathes at)

        // Spawn one persistent manager at startup so EVERY scene (Level3, SampleScene, future menus) gets
        // haptics with zero per-scene setup. DontDestroyOnLoad keeps it alive across the replay scene-reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("HapticManager").AddComponent<HapticManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // Guard: sims + non-haptic devices have no Taptic Engine → stay unsupported, every call no-ops.
            if (!CHHapticEngine.HardwareSupportsHaptics()) { _supported = false; return; }
            try
            {
                _engine = new CHHapticEngine();
                _engine.Start();
                _supported = true;
            }
            catch (Exception e)
            {
                _supported = false;
                Debug.LogWarning("[Haptic] engine start failed: " + e.Message);
            }
        }

        private void OnDisable()
        {
            _beating = false;
            StopAllCoroutines();
            try
            {
                _beatPlayer?.Destroy();
                _oneShotPlayer?.Destroy();
                _cuePlayer?.Destroy();
                _engine?.Stop();
                _engine?.Destroy();
            }
            catch (Exception) { /* tearing down — ignore */ }
            _beatPlayer = _oneShotPlayer = _cuePlayer = null;
            _engine = null;
        }

        /// <summary>Fed every frame by InsanitySystem — drives the heartbeat rate + strength.</summary>
        public void SetInsanity(float value) => _insanity = Mathf.Clamp01(value);

        /// <summary>Set 1.25 when Haptic-Primary Mode is on so proximity/threat is felt harder (GDD).</summary>
        public void SetDangerBoost(float boost) => _dangerBoost = Mathf.Max(1f, boost);

        // ===================== Heartbeat (persistent loop) =====================
        public void StartHeartbeat()
        {
            if (!_supported || _beating) return;
            _beating = true;
            StartCoroutine(BeatLoop());
        }

        public void StopHeartbeat()
        {
            _beating = false; // BeatLoop cleans its player and exits
        }

        private IEnumerator BeatLoop()
        {
            while (_beating)
            {
                // Read the fear value + BPM straight from InsanitySystem so the haptic beat and the vignette
                // breathe are guaranteed identical (== null re-finds after a replay scene reload). Falls back to
                // the pushed _insanity + local formula if no InsanitySystem is present.
                if (_fear == null) _fear = FindFirstObjectByType<InsanitySystem>();
                float insanity = _fear != null ? _fear.Insanity : _insanity;
                float bpm = _fear != null ? _fear.HeartbeatBpm : (_baseBpm + _bpmPerInsanity * insanity);
                float intensity = Mathf.Clamp01(Mathf.Lerp(_minHeartIntensity, _maxHeartIntensity, insanity) * _dangerBoost);
                float period = 60f / Mathf.Max(1f, bpm);

                Play(new List<CHHapticEvent>
                {
                    Transient(0f, intensity, _heartSharpness),
                    Transient(_lubDubGapSeconds, intensity * _dubFalloff, _heartSharpness),
                }, ref _beatPlayer);

                yield return new WaitForSeconds(period);
            }
            try { _beatPlayer?.Destroy(); } catch (Exception) { }
            _beatPlayer = null;
        }

        // ===================== One-shots =====================
        /// <summary>Violent sharp slam + a short rumble — the scare hit (ScareDirector Event B / C).</summary>
        public void Jumpscare()
        {
            Play(new List<CHHapticEvent>
            {
                Transient(0f, 1f, 0.9f),                 // the slam
                Continuous(0.02f, 0.30f, 0.9f, 0.6f),    // the rumble
                Transient(0.32f, 0.8f, 0.8f),            // tail
            }, ref _oneShotPlayer);
        }

        /// <summary>Strong double thump — a key pickup landed.</summary>
        public void Confirm()
        {
            Play(new List<CHHapticEvent>
            {
                Transient(0f, 0.8f, 0.5f),
                Transient(0.09f, 0.8f, 0.5f),
            }, ref _oneShotPlayer);
        }

        /// <summary>One deliberate deep thump felt in the dark — run start (stand-in for the real cold-open).</summary>
        public void ColdOpenPulse()
        {
            Play(new List<CHHapticEvent> { Continuous(0f, 0.35f, 0.7f, 0.1f) }, ref _oneShotPlayer);
        }

        /// <summary>Soft short thud when you walk into a wall (strength 0..1). Subtle — must not crowd the
        /// heartbeat/danger channel; used in both the normal and eyes-off modes.</summary>
        public void WallBump(float strength)
        {
            Play(new List<CHHapticEvent> { Transient(0f, Mathf.Clamp01(strength) * 0.55f, 0.3f) }, ref _oneShotPlayer);
        }

        /// <summary>Crisp light tick — the objective compass (Haptic-Primary): fired faster the more you face the
        /// nearest key/exit, silent when facing away. Sharp + light so it's DISTINCT from the deep heartbeat.</summary>
        public void CompassTick()
        {
            Play(new List<CHHapticEvent> { Transient(0f, 0.45f, 0.95f) }, ref _cuePlayer);
        }

        /// <summary>Soft "you can grab it" hum — right next to a key (Haptic-Primary). Rounded + low so it reads
        /// different from the crisp compass tick; auto-pickup + Confirm follow after the dwell.</summary>
        public void GrabBuzz()
        {
            Play(new List<CHHapticEvent> { Continuous(0f, 0.18f, 0.4f, 0.15f) }, ref _cuePlayer);
        }

        // ===================== helpers =====================
        private static CHHapticTransientEvent Transient(float time, float intensity, float sharpness) =>
            new CHHapticTransientEvent
            {
                Time = time,
                EventParameters = new List<CHHapticEventParameter>
                {
                    new CHHapticEventParameter(CHHapticEventParameterID.HapticIntensity, intensity),
                    new CHHapticEventParameter(CHHapticEventParameterID.HapticSharpness, sharpness),
                }
            };

        private static CHHapticContinuousEvent Continuous(float time, float duration, float intensity, float sharpness) =>
            new CHHapticContinuousEvent
            {
                Time = time,
                EventDuration = duration,
                EventParameters = new List<CHHapticEventParameter>
                {
                    new CHHapticEventParameter(CHHapticEventParameterID.HapticIntensity, intensity),
                    new CHHapticEventParameter(CHHapticEventParameterID.HapticSharpness, sharpness),
                }
            };

        // Fresh player with the pattern baked in; destroy the previous one in this slot to avoid leaks.
        private void Play(List<CHHapticEvent> events, ref CHHapticPatternPlayer slot)
        {
            if (!_supported || _engine == null) return;
            try
            {
                slot?.Destroy();
                slot = _engine.MakePlayer(events);
                slot.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Haptic] play failed: " + e.Message);
            }
        }
    }
}
