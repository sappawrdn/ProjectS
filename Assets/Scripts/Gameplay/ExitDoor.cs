using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectS
{
    /// <summary>
    /// The run's exit. Reaching it WITH all keys ends the run — but HOW depends on the mode:
    ///   • Normal: a "TAP TO ESCAPE" prompt — tap anywhere (or Space/Enter) to escape (a deliberate final beat).
    ///   • Nightmare (eyes-off): NO button-hunting — a short dwell at the door auto-escapes (mirrors the key
    ///     auto-pickup), with a soft haptic buzz + the audio door beacon marking arrival.
    /// Proximity-based to match the Key pickup. Arrive without enough keys → nothing happens (the compass/beacon
    /// leads you back to the remaining keys).
    /// </summary>
    public class ExitDoor : MonoBehaviour
    {
        [SerializeField] private float _radius = 2.5f;
        [SerializeField] private float _cuePeriod = 0.5f;      // "you're at the door" buzz cadence
        [SerializeField] private float _autoEscapeDwell = 0.6f; // eyes-off: hold at the door this long → escape

        private Transform _player;
        private bool _atDoor;
        private bool _eyesOff;
        private float _cueTimer;
        private float _dwell;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        private void Update()
        {
            var gs = GameState.Instance;
            if (_player == null || gs == null) return;

            bool near = Vector3.Distance(transform.position, _player.position) <= _radius;
            _atDoor = near && gs.State == GameState.RunState.Playing && gs.KeyCount >= gs.KeysRequired;
            if (!_atDoor) { _cueTimer = 0f; _dwell = 0f; return; }

            _eyesOff = HapticPrimaryController.Instance != null && HapticPrimaryController.Instance.Enabled;

            // Arrival cue: a soft periodic buzz (esp. for eyes-off — "you're at the door").
            _cueTimer -= Time.deltaTime;
            if (_cueTimer <= 0f) { HapticManager.Instance?.GrabBuzz(); _cueTimer = _cuePeriod; }

            if (_eyesOff)
            {
                // No button-hunting eyes-off: dwell at the door → auto-escape (mirrors the key auto-pickup).
                _dwell += Time.deltaTime;
                if (_dwell >= _autoEscapeDwell) gs.TryExit(); // TryExit re-checks keys, then Win()
            }
            else if (TapPressed())
            {
                gs.TryExit();
            }
        }

        private static bool TapPressed()
        {
            if (Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            return false;
        }

        private void OnGUI()
        {
            if (!_atDoor || _eyesOff) return; // eyes-off is black + auto-escapes — no on-screen prompt
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 60f), "TAP TO ESCAPE", style);
        }
    }
}
