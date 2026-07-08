using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ProjectS
{
    // Kept for compatibility with existing checks (QTE/Insanity ask "is it a live threat?").
    //   Static = dormant (before the run starts, or retired in the final section).
    //   Hunter = the active predator. (Watcher is deprecated — the teleporting tier was cut.)
    public enum MonsterTier { Static, Watcher, Hunter }

    /// <summary>
    /// One persistent predator (redesign 2026-07-08 — replaces the key-gated Static/Watcher/Hunter tiers).
    /// It hunts from the moment the run starts, but SOFT: an <b>aggression</b> value (0→1) ramps with time +
    /// keys collected and modulates its speed, senses, and how easily it gives up — soft early (you learn the
    /// map), relentless late.
    ///
    /// Senses (fair, with clear counterplay):
    ///   • <b>Sight</b> — sees you within an aggression-scaled range, walls block line-of-sight.
    ///   • <b>Hearing</b> — while you MOVE you're heard within a radius even without line-of-sight; stand STILL
    ///     and you're silent. Freezing + breaking line-of-sight is the reliable hide (works even mid-panic).
    ///   • <b>Fear = volume knob</b> — insanity (your racing heartbeat) widens BOTH sight and hearing, so panic
    ///     betrays you — but freezing still saves you, so there's no death spiral.
    ///
    /// Fair machinery kept: awareness hysteresis, breathing-room after a won QTE (it searches your last-known),
    /// non-fatal catch recoil. Speed stays only a touch above the player → you win by juking, not out-running.
    /// All tuning is [SerializeField]. Retire() dormants it for the scare-free final section (GDD).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        [Header("Start (dormant until the run begins; set per test)")]
        [SerializeField] private MonsterTier _startTier = MonsterTier.Static;
        [SerializeField] private bool _debugHotkeys = true; // 1 = retire (dormant), 3 = activate, Q = won-QTE

        [Header("References")]
        [SerializeField] private Transform _player;
        [SerializeField] private float _eyeHeight = 1.5f;
        [SerializeField] private float _playerHeadHeight = 1.4f;

        [Header("Aggression ramp (0 soft → 1 relentless)")]
        [SerializeField] private float _startAggression = 0.2f;  // how threatening it is the moment the run starts
        [SerializeField] private float _keyAggression = 0.25f;   // + per key collected beyond the first
        [SerializeField] private float _timeAggression = 0.004f; // + per second active (~+0.24 over 60 s)

        [Header("Speed (lerped by aggression) — a touch above the player, escape by juking")]
        [SerializeField] private float _chaseSpeedMin = 2.2f;
        [SerializeField] private float _chaseSpeedMax = 2.9f;
        [SerializeField] private float _stalkSpeed = 0.9f;       // creep to last-known when it loses you

        [Header("Sight (walls block line-of-sight)")]
        [SerializeField] private float _sightRangeMin = 7f;      // low aggression
        [SerializeField] private float _sightRangeMax = 10f;     // high aggression
        [SerializeField] private float _loseRange = 20f;         // hysteresis: only drops beyond this
        [SerializeField] private float _sightInsanityBonus = 6f; // fear widens sight

        [Header("Hearing (moving = heard w/o line-of-sight; still = silent)")]
        [SerializeField] private float _hearRadiusMin = 4f;      // low aggression
        [SerializeField] private float _hearRadiusMax = 8f;      // high aggression
        [SerializeField] private float _hearInsanityBonus = 4f;  // fear sharpens hearing too
        [SerializeField] private float _playerMoveThreshold = 0.4f; // m/s to count as "making noise"

        [Header("Give-up / breathing room")]
        [SerializeField] private float _searchMin = 2f;          // low aggression gives up fast
        [SerializeField] private float _searchMax = 6f;          // high aggression searches long
        [SerializeField] private float _winSearchSeconds = 4f;   // forced breathing room after a won QTE

        [Header("Fear (InsanitySystem feeds this)")]
        [SerializeField, Range(0f, 1f)] private float _insanity = 0f;

        private NavMeshAgent _agent;
        private MonsterTier _tier;
        private bool _aware;
        private Vector3 _lastKnownPos;
        private float _searchTimer;
        private float _stunTimer;
        private bool _frozen;

        private int _keyCount = 1;
        private float _runTime;         // seconds active — feeds the aggression ramp
        private Vector3 _lastPlayerPos;
        private float _playerSpeed;     // m/s, horizontal — how much noise the player is making

        public MonsterTier Tier => _tier;
        public bool IsAware => _aware;

        /// <summary>Aggression 0→1 (start + keys + time). Drives speed, senses, and give-up.</summary>
        public float Aggression =>
            Mathf.Clamp01(_startAggression + _keyAggression * Mathf.Max(0, _keyCount - 1) + _timeAggression * _runTime);

        /// <summary>Stunned or frozen — the QTE must not (re)trigger while true, so a catch recoil/stun gives a
        /// real escape window even if the shove couldn't move it far in tight space.</summary>
        public bool IsBusy => _frozen || _stunTimer > 0f;

        /// <summary>Hold the monster in place (e.g. while a QTE overlay is open). Clear on resolve.</summary>
        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = frozen;
        }

        /// <summary>Won-QTE stun: stand still for a few seconds (with OnQteWon for the breathing room).</summary>
        public void Stun(float seconds)
        {
            _stunTimer = Mathf.Max(_stunTimer, seconds);
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        /// <summary>Catch recoil: shove the monster back from the player, then stun it — a fresh escape.</summary>
        public void Recoil(float distance, float stunSeconds)
        {
            if (_player != null && _agent != null && _agent.isOnNavMesh)
            {
                Vector3 away = transform.position - _player.position;
                away.y = 0f;
                Vector3 target = transform.position + away.normalized * distance;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, distance, NavMesh.AllAreas))
                    _agent.Warp(hit.position);

                _aware = false;
                _lastKnownPos = _player.position;
            }
            Stun(stunSeconds);
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }
            if (_player != null) _lastPlayerPos = _player.position;

            // The encounter is the QTE, not a body-block. Stop the monster capsule from physically jamming the
            // player's CharacterController (an overlapping solid capsule freezes movement while look still works).
            if (_player != null)
            {
                var monsterCol = GetComponent<Collider>();
                var playerCol = _player.GetComponent<Collider>();
                if (monsterCol != null && playerCol != null)
                    Physics.IgnoreCollision(monsterCol, playerCol, true);
            }
        }

        private void Start() => SetTier(_startTier);

        /// <summary>InsanitySystem feeds current fear here — it widens both sight and hearing.</summary>
        public void SetInsanity(float value) => _insanity = Mathf.Clamp01(value);

        /// <summary>Instantly relocate the agent (used by scripted scares).</summary>
        public void TeleportTo(Vector3 worldPos)
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.Warp(worldPos);
        }

        /// <summary>Snap to face a world point on the horizontal plane (scare lunge).</summary>
        public void FaceInstant(Vector3 target)
        {
            Vector3 dir = target - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        /// <summary>Called at run start (held keys) and on each pickup. Activates the predator (soft) and bumps
        /// aggression per key. Redesign: no more Static→Watcher→Hunter switching — one predator that ramps.</summary>
        public void OnKeyCollected(int keyCount)
        {
            _keyCount = keyCount;
            if (keyCount >= 1 && _tier != MonsterTier.Hunter) Activate();
        }

        private void Activate()
        {
            _runTime = 0f;              // aggression starts ramping from run start
            SetTier(MonsterTier.Hunter);
        }

        /// <summary>Retire the monster for the scare-free final section (GDD): it goes dormant.</summary>
        public void Retire() => SetTier(MonsterTier.Static);

        public void SetTier(MonsterTier tier)
        {
            _tier = tier;
            if (_agent == null || !_agent.isOnNavMesh) return;

            if (tier == MonsterTier.Hunter)
            {
                _agent.isStopped = false;
                _aware = false;
                // Start by creeping toward where the player is now; it locks on once you're within sight/hearing.
                _lastKnownPos = _player != null ? _player.position : transform.position;
            }
            else // Static / dormant
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        /// <summary>Breathing-room hook: after a won QTE, force the monster to lose you and search last-known.</summary>
        public void OnQteWon()
        {
            _aware = false;
            _searchTimer = _winSearchSeconds;
            if (_player != null) _lastKnownPos = _player.position;
        }

        private void Update()
        {
            if (_debugHotkeys) HandleDebugHotkeys();
            if (_player == null || _agent == null || !_agent.isOnNavMesh) return;

            // Track how fast the player is moving (their noise) — kept fresh even while frozen so unfreezing
            // doesn't spike a huge delta.
            Vector3 flat = _player.position - _lastPlayerPos; flat.y = 0f;
            _playerSpeed = flat.magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
            _lastPlayerPos = _player.position;

            if (_frozen) { _agent.isStopped = true; return; }
            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                _agent.isStopped = true;
                return;
            }

            if (_tier == MonsterTier.Hunter)
            {
                _agent.isStopped = false;
                _runTime += Time.deltaTime;
                TickPredator();
            }
        }

        private void TickPredator()
        {
            // DISABLED FOR PLAYTESTING: Prevent monster from chasing
            return;

            float aggro = Aggression;
            float dist = Vector3.Distance(transform.position, _player.position);

            float effSight = Mathf.Lerp(_sightRangeMin, _sightRangeMax, aggro) + _insanity * _sightInsanityBonus;
            float hearR = Mathf.Lerp(_hearRadiusMin, _hearRadiusMax, aggro) + _insanity * _hearInsanityBonus;

            bool los = HasLineOfSight();
            bool sees = dist <= effSight && los;
            bool hears = _playerSpeed > _playerMoveThreshold && dist <= hearR; // still = silent = safe

            // Awareness: gain if it sees OR hears you; drop when it can do NEITHER (freeze + break LOS to hide),
            // or you're simply beyond loseRange.
            if (!_aware)
            {
                if (sees || hears) { _aware = true; _searchTimer = 0f; }
            }
            else if (dist > _loseRange || (!sees && !hears))
            {
                _aware = false;
                _lastKnownPos = _player.position;
                _searchTimer = Mathf.Lerp(_searchMin, _searchMax, aggro); // soft gives up fast
            }

            if (_aware)
            {
                _agent.speed = Mathf.Lerp(_chaseSpeedMin, _chaseSpeedMax, aggro);
                _lastKnownPos = _player.position;
                _agent.SetDestination(_player.position);
            }
            else
            {
                _agent.speed = _stalkSpeed;
                if (_searchTimer > 0f) _searchTimer -= Time.deltaTime;
                _agent.SetDestination(_lastKnownPos);
            }
        }

        // Walls block sight. The monster is on the Ignore Raycast layer so it never intercepts its own ray.
        private bool HasLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * _eyeHeight;
            Vector3 head = _player.position + Vector3.up * _playerHeadHeight;

            if (Physics.Linecast(eye, head, out RaycastHit hit))
                return hit.transform == _player || hit.transform.IsChildOf(_player);
            return true;
        }

        private void HandleDebugHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Retire();               // dormant
            if (kb.digit3Key.wasPressedThisFrame) OnKeyCollected(3);      // activate + high aggression
            if (kb.qKey.wasPressedThisFrame) OnQteWon();                  // feel the breathing room
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _aware ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _sightRangeMax);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f); // hearing
            Gizmos.DrawWireSphere(transform.position, _hearRadiusMax);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);   // lose range
            Gizmos.DrawWireSphere(transform.position, _loseRange);
        }
    }
}
