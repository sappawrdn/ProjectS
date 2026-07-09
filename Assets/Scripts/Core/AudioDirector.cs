using UnityEngine;
using UnityEngine.AI;

namespace ProjectS
{
    /// <summary>
    /// The one place that owns the game's sound (architecture.md: wrap it in one director). Mostly POLL-based —
    /// it reads InsanitySystem / MonsterAI / GameState / the player each frame and mixes accordingly, so it
    /// needs almost no edits elsewhere. A few one-shots are pushed in (Jumpscare/MonsterAttack/WallBump).
    ///
    /// Mix philosophy (audio is the by-ear MECHANIC, not decoration): 2D beds carry dread (ambient/vhs/heartbeat/
    /// breath), 3D positional sources carry information (monster + beacons) so you navigate + evade by ear. The
    /// monster crossfades calm→hunting so you can HEAR its state; fear (insanity) turns up heartbeat + breath.
    /// Designed to be sufficient with eyes closed (the nightmare/eyes-off mode reuses all of this).
    ///
    /// Clips + sources are assigned/created by ProjectS > Set Up Audio. All levels are [SerializeField].
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [Header("2D beds (stereo)")]
        public AudioClip ambientBed;
        public AudioClip vhs;
        public AudioClip heartbeat;
        public AudioClip breath;

        [Header("Monster (3D, mono)")]
        public AudioClip creatureMoving;   // calm / searching
        public AudioClip creatureChasing;  // aware / hunting
        public AudioClip creatureDetect;   // one-shot: it just noticed you
        public AudioClip creatureAttack;   // one-shot: QTE encounter
        public AudioClip creatureScream;   // one-shot: jumpscare

        [Header("Beacons (3D, mono)")]
        public AudioClip beaconKey;
        public AudioClip beaconDoor;

        [Header("One-shots / player")]
        public AudioClip stinger;      // detection riser
        public AudioClip keyPickup;
        public AudioClip wallBump;
        public AudioClip footstep;     // looped while moving
        public AudioClip qteHit;       // QTE: needle landed in the green
        public AudioClip qteMiss;      // QTE: tap missed

        [Header("Levels")]
        [SerializeField] private float _ambientVol = 0.5f;
        [SerializeField] private float _vhsVol = 0.28f;
        [SerializeField] private float _heartMinVol = 0.12f, _heartMaxVol = 0.9f;
        [SerializeField] private float _breathMinVol = 0.05f, _breathMaxVol = 0.7f;
        [SerializeField] private float _monsterVol = 0.9f;      // 3D max at source
        [SerializeField] private float _beaconVol = 0.55f;
        [SerializeField] private float _footstepVol = 0.4f;
        [SerializeField] private float _oneShotVol = 1f;

        [Header("3D falloff")]
        [SerializeField] private float _monsterMaxDist = 25f;
        [SerializeField] private float _beaconMaxDist = 30f;
        [SerializeField] private float _crossfade = 3f;         // monster moving↔chasing blend speed

        private InsanitySystem _fear;
        private MonsterAI _monster;
        private NavMeshAgent _monsterAgent;
        private Transform _player;
        private CharacterController _playerCc;

        private AudioSource _ambientSrc, _vhsSrc, _heartSrc, _breathSrc, _footSrc, _oneShotSrc;
        private AudioSource _monMoving, _monChasing;

        private bool _wasAware;
        private int _lastKeyCount = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _fear = FindFirstObjectByType<InsanitySystem>();
            _monster = FindFirstObjectByType<MonsterAI>();
            if (_monster != null) _monsterAgent = _monster.GetComponent<NavMeshAgent>();
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { _player = p.transform; _playerCc = p.GetComponent<CharacterController>(); }

            // 2D beds (on this object).
            _ambientSrc = Make2D(ambientBed, _ambientVol, true, true);
            _vhsSrc = Make2D(vhs, _vhsVol, true, true);
            _heartSrc = Make2D(heartbeat, 0f, true, true);
            _breathSrc = Make2D(breath, 0f, true, true);
            _footSrc = Make2D(footstep, _footstepVol, true, false); // looped, toggled by movement
            _oneShotSrc = Make2D(null, 1f, false, false);           // 2D one-shots (pickup/stinger/scream/bump)

            // Monster 3D (two looping sources we crossfade).
            if (_monster != null)
            {
                _monMoving = Make3D(_monster.gameObject, creatureMoving, 0f, _monsterMaxDist, true, true);
                _monChasing = Make3D(_monster.gameObject, creatureChasing, 0f, _monsterMaxDist, true, true);
            }

            // Beacons on every key + the exit (each source lives on that object, so a collected key that
            // deactivates naturally goes silent).
            foreach (var key in FindObjectsByType<Key>(FindObjectsSortMode.None))
                Make3D(key.gameObject, beaconKey, _beaconVol, _beaconMaxDist, true, true);
            var exit = FindFirstObjectByType<ExitDoor>();
            if (exit != null) Make3D(exit.gameObject, beaconDoor, _beaconVol, _beaconMaxDist, true, true);
        }

        private void Update()
        {
            float insanity = _fear != null ? _fear.Insanity : 0f;

            // Fear beds: heartbeat + breath rise with insanity (pitch nudges the heartbeat faster too).
            if (_heartSrc != null)
            {
                _heartSrc.volume = Mathf.Lerp(_heartMinVol, _heartMaxVol, insanity);
                _heartSrc.pitch = Mathf.Lerp(1f, 1.35f, insanity);
            }
            if (_breathSrc != null)
                _breathSrc.volume = Mathf.Lerp(_breathMinVol, _breathMaxVol, insanity);

            // Footsteps: loop only while the player is actually moving.
            if (_footSrc != null && _playerCc != null)
            {
                Vector3 v = _playerCc.velocity; v.y = 0f;
                bool moving = v.magnitude > 0.3f;
                if (moving && !_footSrc.isPlaying) { _footSrc.pitch = Random.Range(0.92f, 1.08f); _footSrc.Play(); }
                else if (!moving && _footSrc.isPlaying) _footSrc.Stop();
            }

            // Monster: crossfade calm↔hunting by awareness; detect sting on the rising edge.
            if (_monster != null && _monMoving != null && _monChasing != null)
            {
                bool active = _monster.Tier != MonsterTier.Static;
                bool aware = active && _monster.IsAware;
                float t = _crossfade * Time.deltaTime;
                _monChasing.volume = Mathf.MoveTowards(_monChasing.volume, aware ? _monsterVol : 0f, t);
                _monMoving.volume = Mathf.MoveTowards(_monMoving.volume, (active && !aware) ? _monsterVol * 0.8f : 0f, t);

                if (aware && !_wasAware)
                {
                    PlayAt(creatureDetect, _monster.transform.position, _monsterMaxDist);
                    Play2DOneShot(stinger, 0.8f);
                }
                _wasAware = aware;
            }

            // Key pickup sting (poll the count so no GameState edit is needed).
            if (GameState.Instance != null)
            {
                int keys = GameState.Instance.KeyCount;
                if (_lastKeyCount >= 0 && keys > _lastKeyCount) Play2DOneShot(keyPickup, _oneShotVol);
                _lastKeyCount = keys;
            }
        }

        // ---- pushed one-shots ----
        /// <summary>Jumpscare scream (ScareDirector Event B/C).</summary>
        public void Jumpscare() => Play2DOneShot(creatureScream, _oneShotVol);

        /// <summary>The monster's attack at a QTE encounter.</summary>
        public void MonsterAttack()
        {
            if (_monster != null) PlayAt(creatureAttack, _monster.transform.position, _monsterMaxDist);
        }

        /// <summary>Soft thud when the player walks into a wall.</summary>
        public void WallBump(float strength) => Play2DOneShot(wallBump, Mathf.Clamp01(strength) * 0.6f);
        public void QteHit() => Play2DOneShot(qteHit, _oneShotVol);
        public void QteMiss() => Play2DOneShot(qteMiss, _oneShotVol);

        // ---- helpers ----
        private AudioSource Make2D(AudioClip clip, float vol, bool loop, bool play)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip; src.spatialBlend = 0f; src.loop = loop; src.volume = vol;
            src.playOnAwake = false;
            if (play && clip != null) src.Play();
            return src;
        }

        private AudioSource Make3D(GameObject host, AudioClip clip, float vol, float maxDist, bool loop, bool play)
        {
            var src = host.AddComponent<AudioSource>();
            src.clip = clip; src.spatialBlend = 1f; src.loop = loop; src.volume = vol;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = maxDist; src.minDistance = 1.5f;
            src.playOnAwake = false;
            if (play && clip != null) src.Play();
            return src;
        }

        private void Play2DOneShot(AudioClip clip, float vol)
        {
            if (clip != null && _oneShotSrc != null) _oneShotSrc.PlayOneShot(clip, Mathf.Clamp01(vol));
        }

        private void PlayAt(AudioClip clip, Vector3 pos, float maxDist)
        {
            if (clip == null) return;
            var go = new GameObject("OneShot3D");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip; src.spatialBlend = 1f; src.volume = _oneShotVol;
            src.rolloffMode = AudioRolloffMode.Linear; src.maxDistance = maxDist; src.minDistance = 1.5f;
            src.Play();
            Destroy(go, clip.length + 0.1f);
        }
    }
}
