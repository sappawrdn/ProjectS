using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectS
{
    /// <summary>
    /// The proximity-solve QTE (architecture.md). When the (non-Static) monster gets within
    /// proximityRadius, the encounter fires: a needle sweeps a dial; tap [Space] when it's in the green.
    /// 3 hits = stun the monster + breathing room (it loses you). 3 fails = +1 catch + recoil (shove it
    /// back + stun) + the catch ladder on the player. Held input is cleared on close (prototype drift bug).
    /// The dial visual is a greybox stand-in for the polished radial UI. All tuning is [SerializeField].
    /// </summary>
    public class QTEController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MonsterAI _monster;
        [SerializeField] private PlayerController _player;

        [Header("Trigger (architecture.md)")]
        [SerializeField] private float _proximityRadius = 1.6f;
        [SerializeField] private float _cooldown = 1.5f;

        [Header("Needle minigame (architecture.md)")]
        [SerializeField] private float _sweepSeconds = 1.7f;   // per full 360° sweep
        [SerializeField] private float _greenZoneWidth = 46f;  // degrees → ~0.22s tap window
        [SerializeField] private int _hitsToWin = 3;
        [SerializeField] private int _failsToLose = 3;

        [Header("Resolution (architecture.md)")]
        [SerializeField] private float _winStunSeconds = 4f;
        [SerializeField] private float _catchRecoilDistance = 6f;
        [SerializeField] private float _catchStunSeconds = 2f;

        private Transform _playerT;
        private bool _active;
        private float _needle;       // 0..360, current needle angle
        private float _greenCenter;  // 0..360, centre of the green zone this round
        private int _hits;
        private int _fails;
        private float _cooldownTimer;

        private void Awake()
        {
            if (_monster == null) _monster = FindFirstObjectByType<MonsterAI>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player != null) _playerT = _player.transform;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (_active) { TickQte(); return; }
            if (_cooldownTimer <= 0f && CanTrigger()) StartQte();
        }

        private bool CanTrigger()
        {
            if (_monster == null || _playerT == null) return false;
            if (GameState.Instance != null && GameState.Instance.State != GameState.RunState.Playing) return false;
            if (_monster.Tier == MonsterTier.Static) return false; // dormant — no encounter
            if (_monster.IsBusy) return false;                     // stunned/frozen → your escape window
            return Vector3.Distance(_monster.transform.position, _playerT.position) <= _proximityRadius;
        }

        private void StartQte()
        {
            _active = true;
            _hits = 0;
            _fails = 0;
            _needle = 0f;
            RandomizeGreen();
            _player?.SetInputEnabled(false); // freeze the player during the overlay
            _monster?.SetFrozen(true);       // hold the monster while the QTE is open
        }

        private void TickQte()
        {
            _needle = (_needle + (360f / _sweepSeconds) * Time.deltaTime) % 360f;

            var kb = Keyboard.current;
            if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;

            if (Mathf.Abs(Mathf.DeltaAngle(_needle, _greenCenter)) <= _greenZoneWidth * 0.5f) _hits++;
            else _fails++;

            RandomizeGreen();

            if (_hits >= _hitsToWin) Resolve(true);
            else if (_fails >= _failsToLose) Resolve(false);
        }

        private void Resolve(bool win)
        {
            _active = false;
            _cooldownTimer = _cooldown;

            _monster?.SetFrozen(false);
            _player?.SetInputEnabled(true); // restore; new-Input-System reads fresh so nothing carries over

            if (win)
            {
                _monster?.Stun(_winStunSeconds);
                _monster?.OnQteWon(); // breathing room: it loses you and searches last-known
            }
            else
            {
                GameState.Instance?.AddCatch();
                _monster?.Recoil(_catchRecoilDistance, _catchStunSeconds);
                int catches = GameState.Instance != null ? GameState.Instance.CatchCount : 0;
                _player?.ApplyCatch(catches);
            }
        }

        private void RandomizeGreen() => _greenCenter = Random.Range(0f, 360f);

        private void OnGUI()
        {
            if (!_active) return;

            float w = Screen.width, h = Screen.height;
            Vector2 center = new Vector2(w / 2f, h / 2f);
            float radius = Mathf.Min(w, h) * 0.18f;
            Texture2D tex = Texture2D.whiteTexture;
            Matrix4x4 prev = GUI.matrix;

            // Green target zone.
            GUI.color = new Color(0.1f, 0.9f, 0.2f, 0.9f);
            GUIUtility.RotateAroundPivot(_greenCenter, center);
            float arcWidth = radius * Mathf.Deg2Rad * _greenZoneWidth;
            GUI.DrawTexture(new Rect(center.x - arcWidth / 2f, center.y - radius - 8f, arcWidth, 14f), tex);
            GUI.matrix = prev;

            // Sweeping needle.
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(_needle, center);
            GUI.DrawTexture(new Rect(center.x - 2f, center.y - radius, 4f, radius), tex);
            GUI.matrix = prev;

            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0f, center.y + radius + 12f, w, 30f),
                $"TAP [Space] in the GREEN!    Hits {_hits}/{_hitsToWin}    Fails {_fails}/{_failsToLose}", style);
        }
    }
}
