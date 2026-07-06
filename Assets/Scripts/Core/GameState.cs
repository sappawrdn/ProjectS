using UnityEngine;

namespace ProjectS
{
    /// <summary>
    /// Thin run-state owner (architecture.md: no god-object). Tracks the run flags, key count and catch
    /// count, and drives the monster's tier as keys are collected. Win = reach the exit with enough keys;
    /// lose = 3 catches (catches come from the QTE/catch system in Phase 2, so the lose-path is stubbed
    /// for now). The OnGUI readout is a temporary greybox dev aid — the shipping game has near-zero HUD.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        public enum RunState { Playing, Won, Lost }

        [Header("Rules (architecture.md)")]
        [SerializeField] private int _keysRequired = 3;      // 1 held + 2 found
        [SerializeField] private int _startingHeldKeys = 1;
        [SerializeField] private int _catchesToLose = 3;

        [Header("References")]
        [SerializeField] private MonsterAI _monster;

        public int KeyCount { get; private set; }
        public int CatchCount { get; private set; }
        public RunState State { get; private set; } = RunState.Playing;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            KeyCount = _startingHeldKeys;
            if (_monster == null) _monster = FindFirstObjectByType<MonsterAI>();
            _monster?.OnKeyCollected(KeyCount); // set the initial tier for the keys already held
        }

        public void CollectKey()
        {
            if (State != RunState.Playing) return;
            KeyCount++;
            _monster?.OnKeyCollected(KeyCount); // pickup escalates the monster
            Debug.Log($"[GameState] Key collected: {KeyCount}/{_keysRequired}");
        }

        /// <summary>Non-fatal strike from a lost QTE (Phase 2 wires this). 3 = game over.</summary>
        public void AddCatch()
        {
            if (State != RunState.Playing) return;
            CatchCount++;
            Debug.Log($"[GameState] Catch: {CatchCount}/{_catchesToLose}");
            if (CatchCount >= _catchesToLose) Lose();
        }

        /// <summary>Called by the exit when the player reaches it — wins only if enough keys are held.</summary>
        public void TryExit()
        {
            if (State != RunState.Playing) return;
            if (KeyCount >= _keysRequired) Win();
        }

        private void Win()
        {
            State = RunState.Won;
            Debug.Log("[GameState] YOU ESCAPED.");
        }

        private void Lose()
        {
            State = RunState.Lost;
            Debug.Log("[GameState] CAUGHT.");
        }

        private void OnGUI()
        {
            // Temporary dev readout (greybox only — remove for the near-zero-HUD shipping build).
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(12, 12, 500, 30),
                $"Keys {KeyCount}/{_keysRequired}    Catches {CatchCount}/{_catchesToLose}    {State}", style);

            if (State == RunState.Playing) return;

            var big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = State == RunState.Won ? Color.green : Color.red }
            };
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height),
                State == RunState.Won ? "YOU ESCAPED" : "CAUGHT", big);
        }
    }
}
