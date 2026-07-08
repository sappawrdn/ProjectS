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
        [SerializeField] private float _greenZoneMax = 46f;    // green width at the start of a round
        [SerializeField] private float _greenZoneMin = 14f;    // shrinks to this (thread-the-needle) → tap FAST
        [SerializeField] private float _shrinkSeconds = 1.4f;  // time for the green to go max→min
        [SerializeField] private int _hitsToWin = 3;
        [SerializeField] private int _failsToLose = 3;

        [Header("Ring look (thickness, not size — black & white)")]
        [SerializeField] private float _ringRadiusFrac = 0.18f;  // radius as a fraction of the short screen side
        [SerializeField] private float _ringThickness = 8f;      // track line thickness
        [SerializeField] private float _safeThickness = 16f;     // safe-zone line thickness (chunkier)

        [Header("Juice (hit/miss feedback)")]
        [SerializeField] private float _flashSeconds = 0.28f;
        [SerializeField] private float _shakeSeconds = 0.25f;
        [SerializeField] private float _shakePixels = 14f;
        [SerializeField] private float _pulseSeconds = 0.22f;

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
        private float _roundTime;     // time since the last tap — the green shrinks over this
        private float _sweepDir = 1f; // +1 / -1, flips on each successful hit
        private float _flashTimer, _shakeTimer, _pulseTimer;
        private Color _flashColor = Color.red;

        // Green shrinks from max→min the longer you wait in the current round (pressure to tap fast).
        private float CurrentGreenWidth =>
            Mathf.Lerp(_greenZoneMax, _greenZoneMin, _roundTime / Mathf.Max(0.01f, _shrinkSeconds));

        private void Awake()
        {
            if (_monster == null) _monster = FindFirstObjectByType<MonsterAI>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player != null) _playerT = _player.transform;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            if (_flashTimer > 0f) _flashTimer -= Time.deltaTime;
            if (_shakeTimer > 0f) _shakeTimer -= Time.deltaTime;
            if (_pulseTimer > 0f) _pulseTimer -= Time.deltaTime;

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
            _roundTime = 0f;
            _sweepDir = 1f;
            RandomizeGreen();
            AudioDirector.Instance?.MonsterAttack(); // creature attack as the encounter opens
            _player?.SetInputEnabled(false); // freeze the player during the overlay
            _monster?.SetFrozen(true);       // hold the monster while the QTE is open
        }

        private void TickQte()
        {
            _roundTime += Time.deltaTime; // the green shrinks as this grows
            _needle = Mathf.Repeat(_needle + _sweepDir * (360f / _sweepSeconds) * Time.deltaTime, 360f);

            // Tap the screen (device) or press [Space] (editor). Player is frozen here, so a tap only hits the QTE.
            bool tapped = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                          || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
            if (!tapped) return;

            if (Mathf.Abs(Mathf.DeltaAngle(_needle, _greenCenter)) <= CurrentGreenWidth * 0.5f)
            {
                _hits++;
                _sweepDir *= -1f; // reverse the sweep direction after a successful tap
                _pulseTimer = _pulseSeconds;                                   // ring pulses on a hit
                _flashColor = Color.white; _flashTimer = _flashSeconds * 0.4f; // faint white pop
            }
            else
            {
                _fails++;
                _flashColor = Color.white; _flashTimer = _flashSeconds; // harsh white flash (static/camera)
                _shakeTimer = _shakeSeconds;                            // + screen shake
            }

            _roundTime = 0f;   // fresh round → green resets to max
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
            var tex = Texture2D.whiteTexture;
            Matrix4x4 prev = GUI.matrix;

            Vector2 center = new Vector2(w / 2f, h / 2f);
            if (_shakeTimer > 0f) // screen shake on a miss
            {
                float m = _shakeTimer / _shakeSeconds * _shakePixels;
                center += new Vector2(Random.Range(-m, m), Random.Range(-m, m));
            }

            float radius = Mathf.Min(w, h) * _ringRadiusFrac; // fixed size — hits get THICKER, not bigger
            float pulse = _pulseTimer > 0f ? _pulseTimer / _pulseSeconds : 0f; // 0..1 on a hit

            // The ring track — solid grey band (dim). Thickens briefly on a hit.
            DrawArc(center, radius, _ringThickness + pulse * 4f, 0f, 360f, new Color(0.55f, 0.55f, 0.55f, 0.75f), tex, prev);

            // The safe arc — solid WHITE band (chunkier). Its width shrinks each round.
            float gw = CurrentGreenWidth;
            DrawArc(center, radius, _safeThickness + pulse * 8f, _greenCenter - gw / 2f, _greenCenter + gw / 2f, Color.white, tex, prev);

            // The sweeping needle (white, thick).
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(_needle, center);
            GUI.DrawTexture(new Rect(center.x - 3f, center.y - radius - _safeThickness / 2f, 6f, radius + _safeThickness / 2f), tex);
            GUI.matrix = prev;

            // Full-screen white flash (harsh on a miss, faint on a hit).
            if (_flashTimer > 0f)
            {
                var c = _flashColor; c.a = _flashTimer / _flashSeconds * 0.4f;
                GUI.color = c;
                GUI.DrawTexture(new Rect(0f, 0f, w, h), tex);
                GUI.color = Color.white;
            }

            // Big black-&-white counters in the corners (like the reference's HEALTH / CASH).
            DrawBigCounter(40f, h - 160f, "HITS", _hits, TextAnchor.LowerLeft, tex);
            DrawBigCounter(w - 40f, h - 160f, "MISS", _fails, TextAnchor.LowerRight, tex);
        }

        // A solid arc band around the circle (tangential segments overlapped → no gaps). Thickness = radial "tebal".
        private void DrawArc(Vector2 c, float radius, float thickness, float from, float to, Color color, Texture2D tex, Matrix4x4 baseMatrix)
        {
            const float step = 4f;
            float segW = radius * Mathf.Deg2Rad * step * 1.8f; // tangential length, overlapped for a solid band
            GUI.color = color;
            for (float a = from; a <= to; a += step)
            {
                GUIUtility.RotateAroundPivot(a, c);
                GUI.DrawTexture(new Rect(c.x - segW / 2f, c.y - radius - thickness / 2f, segW, thickness), tex);
                GUI.matrix = baseMatrix;
            }
            GUI.color = Color.white;
        }

        // Big bold count (value) with a small label above it, black shadow for readability. Reference-style.
        private void DrawBigCounter(float x, float y, string label, int value, TextAnchor anchor, Texture2D tex)
        {
            const float boxW = 320f;
            bool left = anchor == TextAnchor.LowerLeft;
            var small = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = anchor };
            var big = new GUIStyle(GUI.skin.label) { fontSize = 84, fontStyle = FontStyle.Bold, alignment = anchor };
            Rect labelR = new Rect(left ? x : x - boxW, y, boxW, 34f);
            Rect numR = new Rect(left ? x : x - boxW, y + 30f, boxW, 100f);
            ShadowLabel(labelR, label, small);
            ShadowLabel(numR, value.ToString(), big);
        }

        private void ShadowLabel(Rect r, string txt, GUIStyle style)
        {
            var shadow = new GUIStyle(style); shadow.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), txt, shadow);
            style.normal.textColor = Color.white;
            GUI.Label(r, txt, style);
        }
    }
}
