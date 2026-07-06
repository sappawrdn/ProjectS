using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Apple.CoreHaptics;

namespace ProjectS.DeRisk
{
    /// <summary>
    /// Phase 0 de-risk harness: proves Core Haptics can BE the fear channel on a real device.
    /// Plays a looping heartbeat "lub-dub" whose rate (BPM) and strength (intensity) both scale with an
    /// insanity value [0..1] — the exact mechanic the game relies on. All tuning is [SerializeField].
    ///
    /// HOW TO VERIFY (on an iPhone — the simulator/editor cannot reproduce the Taptic Engine):
    ///   1. Build & run to device. Leave "Auto Sweep" on: insanity ramps 0->1->0 automatically.
    ///   2. FEEL for two things: the beat should SPEED UP and get STRONGER as insanity rises, then ease off.
    ///   3. Toggle "Auto Sweep" off and drag the slider to confirm manual control feels right.
    /// If the lub-dub is distinct and intensity is clearly felt to change, Core Haptics is proven.
    /// </summary>
    public class HeartbeatHapticTest : MonoBehaviour
    {
        [Header("Fear input (0 = calm, 1 = panic) — drives BPM + intensity")]
        [SerializeField, Range(0f, 1f)] private float _insanity = 0f;
        [SerializeField] private bool _autoSweep = true;          // hands-free ramp for a device test
        [SerializeField] private float _autoSweepSeconds = 10f;   // time for one 0->1 (or 1->0) leg

        [Header("Heartbeat tuning (starting values from architecture.md)")]
        [SerializeField] private float _baseBpm = 60f;            // BPM = base + perInsanity * insanity
        [SerializeField] private float _bpmPerInsanity = 90f;     // -> calm 60 BPM, panic 150 BPM
        [SerializeField] private float _lubDubGapSeconds = 0.14f; // gap between the "lub" and the "dub"
        [SerializeField, Range(0f, 1f)] private float _minIntensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _maxIntensity = 1.0f;
        [SerializeField, Range(0f, 1f)] private float _dubFalloff = 0.7f; // the "dub" is a bit softer than the "lub"
        [SerializeField, Range(0f, 1f)] private float _sharpness = 0.4f;   // deep/rounded thump, not a sharp click

        private CHHapticEngine _engine;
        private CHHapticPatternPlayer _player;
        private bool _supported;
        private string _status = "starting…";
        private float _sweepTime;

        private void OnEnable()
        {
            // Guard: some devices (and all sims) have no haptic hardware.
            if (!CHHapticEngine.HardwareSupportsHaptics())
            {
                _supported = false;
                _status = "This device does NOT support Core Haptics.";
                return;
            }

            _supported = true;
            try
            {
                _engine = new CHHapticEngine();
                _engine.Start();
                _status = "engine started";
                StartCoroutine(BeatLoop());
            }
            catch (Exception e)
            {
                _status = "engine start FAILED: " + e.Message;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            try
            {
                _player?.Destroy();
                _engine?.Stop();
                _engine?.Destroy();
            }
            catch (Exception) { /* tearing down — ignore */ }
            _player = null;
            _engine = null;
        }

        // A heartbeat = two transient thumps ("lub" then a softer "dub").
        // Intensity is baked into the pattern each beat so the change is unambiguous on device.
        private List<CHHapticEvent> BuildLubDub(float intensity)
        {
            return new List<CHHapticEvent>
            {
                new CHHapticTransientEvent
                {
                    Time = 0f,
                    EventParameters = new List<CHHapticEventParameter>
                    {
                        new CHHapticEventParameter(CHHapticEventParameterID.HapticIntensity, intensity),
                        new CHHapticEventParameter(CHHapticEventParameterID.HapticSharpness, _sharpness),
                    }
                },
                new CHHapticTransientEvent
                {
                    Time = _lubDubGapSeconds,
                    EventParameters = new List<CHHapticEventParameter>
                    {
                        new CHHapticEventParameter(CHHapticEventParameterID.HapticIntensity, intensity * _dubFalloff),
                        new CHHapticEventParameter(CHHapticEventParameterID.HapticSharpness, _sharpness),
                    }
                },
            };
        }

        private IEnumerator BeatLoop()
        {
            while (true)
            {
                if (_autoSweep)
                {
                    _sweepTime += Time.deltaTime / Mathf.Max(0.1f, _autoSweepSeconds);
                    _insanity = Mathf.PingPong(_sweepTime, 1f);
                }

                float intensity = Mathf.Lerp(_minIntensity, _maxIntensity, _insanity);
                float bpm = _baseBpm + _bpmPerInsanity * _insanity;
                float beatPeriod = 60f / Mathf.Max(1f, bpm);

                try
                {
                    // Fresh player per beat with intensity baked in; destroy the previous to avoid leaks.
                    _player?.Destroy();
                    _player = _engine.MakePlayer(BuildLubDub(intensity));
                    _player.Start();
                    _status = $"beating — {bpm:0} BPM · intensity {intensity:0.00}";
                }
                catch (Exception e)
                {
                    _status = "play error: " + e.Message;
                }

                yield return new WaitForSeconds(beatPeriod);
            }
        }

        // Minimal on-screen controls (IMGUI works on-device without a Canvas — fine for a throwaway harness).
        private void OnGUI()
        {
            // Scale UI up for high-DPI phone screens.
            float scale = Mathf.Max(1f, Screen.dpi / 160f);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

            const float pad = 12f;
            GUILayout.BeginArea(new Rect(pad, pad, 360f, 320f), GUI.skin.box);

            GUILayout.Label("<b>Core Haptics — Heartbeat de-risk</b>");
            GUILayout.Space(6f);
            GUILayout.Label(_supported ? _status : "<color=red>" + _status + "</color>");
            GUILayout.Space(10f);

            _autoSweep = GUILayout.Toggle(_autoSweep, "  Auto Sweep (insanity 0 → 1 → 0)");
            GUILayout.Space(6f);

            GUILayout.Label($"Insanity: {_insanity:0.00}");
            GUI.enabled = !_autoSweep;
            _insanity = GUILayout.HorizontalSlider(_insanity, 0f, 1f);
            GUI.enabled = true;

            GUILayout.EndArea();
        }
    }
}
