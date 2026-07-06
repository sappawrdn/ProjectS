using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ProjectS
{
    public enum MonsterTier { Static, Watcher, Hunter }

    /// <summary>
    /// The predator FSM (architecture.md). Key count sets the tier (Static → Watcher → Hunter);
    /// insanity modulates aggression within a tier (wired later by InsanitySystem).
    ///   Static  — dormant.
    ///   Watcher — teleports around the player at intervals, never inside QTE range (min distance).
    ///   Hunter  — NavMeshAgent chase when aware (within sight + line-of-sight); when it loses you it
    ///             creeps to your last-known spot and searches. Awareness has hysteresis (gain within
    ///             sightRange, drop only beyond loseRange) so it feels like hunting, not heat-seeking.
    /// All tuning is [SerializeField] with playtested starting values.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        [Header("Tier (key-gated in game; set here for testing)")]
        [SerializeField] private MonsterTier _startTier = MonsterTier.Hunter;
        [SerializeField] private bool _debugHotkeys = true; // 1/2/3 switch tier, Q simulates a won QTE

        [Header("References")]
        [SerializeField] private Transform _player;
        [SerializeField] private float _eyeHeight = 1.5f;
        [SerializeField] private float _playerHeadHeight = 1.4f;

        [Header("Hunter (architecture.md tuning)")]
        [SerializeField] private float _chaseSpeed = 2.6f;   // a touch above the player
        [SerializeField] private float _stalkSpeed = 0.9f;   // slow creep to last-known
        [SerializeField] private float _sightRange = 10f;    // calm detection radius
        [SerializeField] private float _loseRange = 20f;     // hysteresis: only drops beyond this
        [SerializeField] private float _sightInsanityBonus = 6f; // effective sight = sight + insanity*bonus
        [SerializeField] private float _winSearchSeconds = 4f;   // breathing room after a won QTE

        [Header("Watcher (architecture.md tuning)")]
        [SerializeField] private float _watcherInterval = 4f;
        [SerializeField] private float _watcherMinDistance = 4f; // never inside QTE range
        [SerializeField] private float _watcherMaxDistance = 9f;

        [Header("Fear (InsanitySystem wires this later)")]
        [SerializeField, Range(0f, 1f)] private float _insanity = 0f;

        private NavMeshAgent _agent;
        private MonsterTier _tier;
        private bool _aware;
        private Vector3 _lastKnownPos;
        private float _searchTimer;
        private float _watcherTimer;
        private float _stunTimer;
        private bool _frozen;

        public MonsterTier Tier => _tier;
        public bool IsAware => _aware;

        /// <summary>Stunned or frozen — the encounter QTE must not (re)trigger while true, so a catch
        /// recoil/stun gives a real escape window even if the shove couldn't move it far in tight space.</summary>
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

            // The encounter is the QTE, not a body-block. Stop the monster capsule from physically jamming
            // the player's CharacterController (an overlapping solid capsule freezes movement while look
            // still works). Works regardless of the collider's trigger flag — no scene regen needed.
            if (_player != null)
            {
                var monsterCol = GetComponent<Collider>();
                var playerCol = _player.GetComponent<Collider>();
                if (monsterCol != null && playerCol != null)
                    Physics.IgnoreCollision(monsterCol, playerCol, true);
            }
        }

        private void Start() => SetTier(_startTier);

        /// <summary>Key count drives the tier: 0-1 → Static, 2 → Watcher, 3+ → Hunter.</summary>
        public void OnKeyCollected(int keyCount)
        {
            if (keyCount >= 3) SetTier(MonsterTier.Hunter);
            else if (keyCount >= 2) SetTier(MonsterTier.Watcher);
            else SetTier(MonsterTier.Static);
        }

        public void SetTier(MonsterTier tier)
        {
            _tier = tier;
            if (_agent == null || !_agent.isOnNavMesh) return;

            switch (tier)
            {
                case MonsterTier.Static:
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    break;
                case MonsterTier.Watcher:
                    _agent.isStopped = false;
                    _watcherTimer = 0f; // teleport on the next tick
                    break;
                case MonsterTier.Hunter:
                    _agent.isStopped = false;
                    _aware = false;
                    // Start by hunting toward where the player is now (search), not standing still —
                    // it locks on once you're within sight + line-of-sight.
                    _lastKnownPos = _player != null ? _player.position : transform.position;
                    break;
            }
        }

        /// <summary>Breathing-room hook: after a won QTE, force the monster to lose you and search.</summary>
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

            // Frozen (QTE open) or stunned (post-QTE) → hold still.
            if (_frozen) { _agent.isStopped = true; return; }
            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                _agent.isStopped = true;
                return;
            }
            if (_tier != MonsterTier.Static) _agent.isStopped = false;

            switch (_tier)
            {
                case MonsterTier.Static: break;
                case MonsterTier.Watcher: TickWatcher(); break;
                case MonsterTier.Hunter: TickHunter(); break;
            }
        }

        private void TickHunter()
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            float effectiveSight = _sightRange + _insanity * _sightInsanityBonus;
            bool los = HasLineOfSight();

            // Awareness hysteresis: gain within sight + LOS, drop only beyond loseRange (or LOS lost & far).
            if (!_aware)
            {
                if (dist <= effectiveSight && los)
                {
                    _aware = true;
                    _searchTimer = 0f;
                }
            }
            else if (dist > _loseRange || (!los && dist > effectiveSight))
            {
                _aware = false;
                _lastKnownPos = _player.position;
                _searchTimer = _winSearchSeconds;
            }

            if (_aware)
            {
                _agent.speed = _chaseSpeed;
                _lastKnownPos = _player.position;
                _agent.SetDestination(_player.position);
            }
            else
            {
                // Lost you: creep to your last-known spot and search there.
                _agent.speed = _stalkSpeed;
                if (_searchTimer > 0f) _searchTimer -= Time.deltaTime;
                _agent.SetDestination(_lastKnownPos);
            }
        }

        private void TickWatcher()
        {
            _watcherTimer -= Time.deltaTime;
            if (_watcherTimer > 0f) return;

            // Higher insanity → teleports more often (architecture.md).
            _watcherTimer = _watcherInterval * (1f - 0.5f * _insanity);
            TeleportAroundPlayer();
        }

        private void TeleportAroundPlayer()
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(_watcherMinDistance, _watcherMaxDistance);
                Vector3 candidate = _player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * d;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) &&
                    Vector3.Distance(hit.position, _player.position) >= _watcherMinDistance)
                {
                    _agent.Warp(hit.position);
                    return;
                }
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
            if (kb.digit1Key.wasPressedThisFrame) SetTier(MonsterTier.Static);
            if (kb.digit2Key.wasPressedThisFrame) SetTier(MonsterTier.Watcher);
            if (kb.digit3Key.wasPressedThisFrame) SetTier(MonsterTier.Hunter);
            if (kb.qKey.wasPressedThisFrame) OnQteWon(); // feel the breathing room
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _aware ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _sightRange);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _loseRange);
        }
    }
}
