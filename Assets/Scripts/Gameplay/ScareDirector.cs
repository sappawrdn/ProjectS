using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ProjectS
{
    /// <summary>
    /// Scripted one-shot scares (architecture.md).
    ///   Event B — False Catch (early, ~5s, reliable): the monster lunges right in front of you + a fear
    ///     spike (+ a haptic slam on device), held briefly, then it vanishes. It spikes fear WITHOUT
    ///     advancing the catch counter, and must NOT trigger the QTE (it freezes the monster, which the QTE
    ///     treats as busy, so no encounter fires during the fake).
    ///   Event C — The Reveal (mid): a jumpscare on crossing into the next section. Needs section geometry
    ///     (a must-cross trigger), so it's stubbed here until real levels exist — call TriggerReveal() from
    ///     a section-entry trigger.
    /// Debug: press J to fire Event B on demand.
    /// </summary>
    public class ScareDirector : MonoBehaviour
    {
        [Header("Event B — False Catch (architecture.md)")]
        [SerializeField] private float _eventBDelay = 5f;
        [SerializeField] private float _jumpscareHold = 0.45f;
        [SerializeField] private float _jumpscareInsanity = 0.9f;
        [SerializeField] private float _inFrontDistance = 1.2f;

        [Header("Debug")]
        [SerializeField] private bool _debugKey = true; // J = fire Event B

        private Transform _player;
        private Camera _camera;
        private MonsterAI _monster;
        private InsanitySystem _insanity;

        private bool _eventBFired;
        private bool _scareActive;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
            _camera = Camera.main;
            _monster = FindFirstObjectByType<MonsterAI>();
            _insanity = FindFirstObjectByType<InsanitySystem>();
        }

        /// <summary>GameState calls this when the run actually begins (not during the menu).</summary>
        public void OnRunStarted()
        {
            StartCoroutine(EventBTimer());
        }

        private void Update()
        {
            if (_debugKey && Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
                FireEventB();
        }

        private IEnumerator EventBTimer()
        {
            yield return new WaitForSeconds(_eventBDelay);
            FireEventB();
        }

        /// <summary>Event B — timed false catch. Fires once.</summary>
        public void FireEventB()
        {
            if (_eventBFired || _scareActive) return;
            if (GameState.Instance != null && GameState.Instance.State != GameState.RunState.Playing) return;
            _eventBFired = true;
            StartCoroutine(FalseCatchRoutine());
        }

        /// <summary>Event C hook — call from a section-entry trigger once real levels exist.</summary>
        public void TriggerReveal()
        {
            if (_scareActive) return;
            StartCoroutine(FalseCatchRoutine()); // same jolt; no catch, no QTE
        }

        private IEnumerator FalseCatchRoutine()
        {
            if (_monster == null || _camera == null || _player == null) yield break;
            _scareActive = true;

            // Lunge: slam the monster right in front of you, facing you, frozen (QTE sees it as busy → no encounter).
            Vector3 front = _camera.transform.position + FlatForward() * _inFrontDistance;
            if (NavMesh.SamplePosition(front, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                _monster.TeleportTo(hit.position);
            _monster.SetFrozen(true);
            _monster.FaceInstant(_player.position);

            _insanity?.Spike(_jumpscareInsanity);
            HapticManager.Instance?.Jumpscare(); // violent slam on device (no-op in editor)
            // TODO(audio): add the jumpscare audio sting here once AudioDirector exists (Stage 2).

            yield return new WaitForSeconds(_jumpscareHold);

            // Vanish: throw the monster far away, then hand control back to its FSM.
            if (TryFarPoint(out Vector3 far)) _monster.TeleportTo(far);
            _monster.SetFrozen(false);
            _scareActive = false;
        }

        private Vector3 FlatForward()
        {
            Vector3 fwd = _camera.transform.forward; fwd.y = 0f;
            return fwd.sqrMagnitude < 1e-4f ? transform.forward : fwd.normalized;
        }

        private bool TryFarPoint(out Vector3 result)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 candidate = _player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 12f;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas) &&
                    Vector3.Distance(hit.position, _player.position) > 8f)
                {
                    result = hit.position;
                    return true;
                }
            }
            result = default;
            return false;
        }
    }
}
