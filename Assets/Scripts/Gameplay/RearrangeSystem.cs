using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ProjectS
{
    /// <summary>
    /// The perception layer (architecture.md) — "the structure changes when your attention drifts". Gated by
    /// UNOBSERVED + high insanity, so it only happens where you aren't looking and only when you're scared:
    ///   - Perception phantom: a dark figure appears behind you and vanishes as you turn to it (Weeping-Angel).
    ///     No attack, no progress impact — accessibility-safe.
    ///   - Light death: a ceiling (point) light behind you goes out.
    /// Hysteresis on the view dot: it SPAWNS clearly behind you (dot &lt; spawnDot) and only VANISHES once you've
    /// nearly turned to face it (dot &gt; vanishDot, inside the ~60° FOV) — so you actually glimpse it first.
    /// Debug: press P to force a phantom for testing without grinding insanity.
    /// </summary>
    public class RearrangeSystem : MonoBehaviour
    {
        [Header("Gate (architecture.md)")]
        [SerializeField] private float _rearrangeInsanity = 0.5f; // min fear before anything rearranges
        [SerializeField] private float _unobservedDot = 0.3f;     // ~72° cone; below this = unobserved (lights)

        [Header("Perception phantom")]
        [SerializeField] private float _phantomInterval = 5f;
        [SerializeField] private float _phantomDistance = 7f;
        [SerializeField] private float _phantomLifetime = 6f;
        [SerializeField] private float _phantomSpawnDot = 0f;     // must appear behind/beside you (dot < this)
        [SerializeField] private float _phantomVanishDot = 0.93f; // vanishes once you turn to nearly face it

        [Header("Light death")]
        [SerializeField] private float _lightFlickerInterval = 8f;

        [Header("Debug")]
        [SerializeField] private bool _debugForceKey = true; // P = force-spawn a phantom

        private Transform _player;
        private Camera _camera;
        private InsanitySystem _insanity;

        private GameObject _phantom;
        private bool _phantomActive;
        private float _phantomTimer;
        private float _phantomAge;
        private float _lightTimer;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
            _camera = Camera.main;
            _insanity = FindFirstObjectByType<InsanitySystem>();

            _phantom = CreatePhantom();
            _phantom.SetActive(false);
            _phantomTimer = _phantomInterval;
            _lightTimer = _lightFlickerInterval;
        }

        private GameObject CreatePhantom()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Phantom";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // a ghost — no physics

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader) { color = new Color(0.02f, 0.02f, 0.03f) };
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return go;
        }

        private void Update()
        {
            if (_player == null || _camera == null) return;
            float fear = _insanity != null ? _insanity.Insanity : 0f;

            if (_debugForceKey && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                TrySpawnPhantom();

            TickPhantom(fear);
            TickLightDeath(fear);
        }

        private void TickPhantom(float fear)
        {
            if (_phantomActive)
            {
                _phantomAge += Time.deltaTime;
                // Vanish once you've turned to nearly face it, or when it times out.
                if (_phantomAge >= _phantomLifetime || ForwardDot(_phantom.transform.position) > _phantomVanishDot)
                {
                    _phantom.SetActive(false);
                    _phantomActive = false;
                    _phantomTimer = _phantomInterval;
                }
                return;
            }

            _phantomTimer -= Time.deltaTime;
            if (_phantomTimer <= 0f && fear >= _rearrangeInsanity)
            {
                if (!TrySpawnPhantom()) _phantomTimer = 1f; // nowhere behind you right now; retry soon
            }
        }

        private bool TrySpawnPhantom()
        {
            if (!TryFindSpawnSpot(_phantomDistance, out Vector3 spot)) return false;
            _phantom.transform.position = spot + Vector3.up * 1f; // capsule base on the floor
            _phantom.transform.rotation = Quaternion.LookRotation(FlatToPlayer(spot));
            _phantom.SetActive(true);
            _phantomActive = true;
            _phantomAge = 0f;
            return true;
        }

        private void TickLightDeath(float fear)
        {
            _lightTimer -= Time.deltaTime;
            if (_lightTimer > 0f || fear < _rearrangeInsanity) return;
            _lightTimer = _lightFlickerInterval;

            // Kill one still-lit ceiling (point) light that's behind/beside you.
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Point || !light.enabled) continue;
                if (ForwardDot(light.transform.position) < _unobservedDot)
                {
                    light.enabled = false;
                    return;
                }
            }
        }

        private bool TryFindSpawnSpot(float distance, out Vector3 result)
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 candidate = _player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (ForwardDot(candidate) >= _phantomSpawnDot) continue; // must appear behind/beside you
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
            result = default;
            return false;
        }

        // dot(camForwardXZ, dirToPosXZ): 1 = dead ahead, 0 = 90° to the side, -1 = directly behind.
        private float ForwardDot(Vector3 worldPos)
        {
            Vector3 fwd = _camera.transform.forward; fwd.y = 0f;
            Vector3 to = worldPos - _camera.transform.position; to.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f || to.sqrMagnitude < 1e-4f) return 1f;
            return Vector3.Dot(fwd.normalized, to.normalized);
        }

        private Vector3 FlatToPlayer(Vector3 from)
        {
            Vector3 dir = _player.position - from; dir.y = 0f;
            return dir.sqrMagnitude < 1e-4f ? Vector3.forward : dir.normalized;
        }
    }
}
