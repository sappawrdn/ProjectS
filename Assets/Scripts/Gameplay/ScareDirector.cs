using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectS
{
    /// <summary>
    /// Scripted one-shot scares (architecture.md), reworked 2026-07-08 for the always-active predator:
    ///   Event B — Phantom Scare: on the FIRST key pickup, a PHANTOM (a fake copy of the monster) flashes right
    ///     in front of you + a fear spike + haptic slam + scream, held briefly, then vanishes. The REAL monster
    ///     is NOT touched — it keeps hunting and stays audible (the old version teleported the real monster in
    ///     and out, which felt like it spawned from nowhere and broke the by-ear tracking).
    ///   Event C — The Reveal (mid): same phantom jolt, call TriggerReveal() from a section-entry trigger once
    ///     real sections exist.
    /// Debug: press J to fire Event B on demand.
    /// </summary>
    public class ScareDirector : MonoBehaviour
    {
        [Header("Phantom scare")]
        [SerializeField] private float _jumpscareHold = 0.45f;
        [SerializeField] private float _jumpscareInsanity = 0.9f;
        [SerializeField] private float _inFrontDistance = 1.2f;

        [Header("Debug")]
        [SerializeField] private bool _debugKey = true; // J = fire Event B

        private Transform _player;
        private Camera _camera;
        private MonsterAI _monster;
        private InsanitySystem _insanity;

        private int _keysAtStart = -1;
        private bool _eventBFired;
        private bool _scareActive;

        private void Start()
        {
            // --- TEMPORARILY DISABLED FOR SCENE TESTING ---
            // Teman Sappa: Kalo mau nge-merge dan butuh Jumpscare Phantom aktif lagi,
            // hapus baris gameObject.SetActive(false) di bawah ini ya!
            gameObject.SetActive(false);
            return;
            // ----------------------------------------------

            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
            _camera = Camera.main;
            _monster = FindFirstObjectByType<MonsterAI>();
            _insanity = FindFirstObjectByType<InsanitySystem>();
        }

        /// <summary>GameState calls this when the run begins — remember the held-key count so we can fire on the
        /// first FOUND key.</summary>
        public void OnRunStarted()
        {
            _keysAtStart = GameState.Instance != null ? GameState.Instance.KeyCount : 1;
        }

        private void Update()
        {
            if (_debugKey && Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
                FireEventB();

            // Fire on the first key pickup (a designed beat, not an arbitrary timer).
            if (!_eventBFired && _keysAtStart >= 0 && GameState.Instance != null
                && GameState.Instance.State == GameState.RunState.Playing
                && GameState.Instance.KeyCount > _keysAtStart)
                FireEventB();
        }

        /// <summary>Event B — phantom false catch. Fires once.</summary>
        public void FireEventB()
        {
            if (_eventBFired || _scareActive) return;
            if (GameState.Instance != null && GameState.Instance.State != GameState.RunState.Playing) return;
            _eventBFired = true;
            StartCoroutine(PhantomScareRoutine());
        }

        /// <summary>Event C hook — call from a section-entry trigger once real levels exist.</summary>
        public void TriggerReveal()
        {
            if (_scareActive) return;
            StartCoroutine(PhantomScareRoutine());
        }

        private IEnumerator PhantomScareRoutine()
        {
            if (_camera == null || _player == null) yield break;
            _scareActive = true;

            // Phantom right in front — the REAL monster is left alone (keeps hunting + audible).
            Vector3 front = _camera.transform.position + FlatForward() * _inFrontDistance;
            front.y = _player.position.y;
            GameObject phantom = SpawnPhantom(front);

            _insanity?.Spike(_jumpscareInsanity);
            HapticManager.Instance?.Jumpscare();
            AudioDirector.Instance?.Jumpscare();

            yield return new WaitForSeconds(_jumpscareHold);

            if (phantom != null) Destroy(phantom);
            _scareActive = false;
        }

        // A throwaway copy of the monster's visual (or a capsule fallback), facing you, playing the lunge.
        private GameObject SpawnPhantom(Vector3 pos)
        {
            GameObject phantom;
            var visual = _monster != null ? _monster.GetComponentInChildren<MonsterVisual>() : null;
            if (visual != null)
            {
                phantom = Instantiate(visual.gameObject);
                phantom.name = "ScarePhantom";
                phantom.transform.position = pos;
                FaceThePlayer(phantom.transform);
                phantom.GetComponent<MonsterVisual>()?.PlayAttack();
            }
            else
            {
                phantom = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                phantom.name = "ScarePhantom";
                var col = phantom.GetComponent<Collider>();
                if (col != null) Destroy(col);
                phantom.transform.position = pos + Vector3.up * 1f;
                FaceThePlayer(phantom.transform);
            }
            return phantom;
        }

        private void FaceThePlayer(Transform t)
        {
            Vector3 dir = _player.position - t.position; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private Vector3 FlatForward()
        {
            Vector3 fwd = _camera.transform.forward; fwd.y = 0f;
            return fwd.sqrMagnitude < 1e-4f ? transform.forward : fwd.normalized;
        }
    }
}
