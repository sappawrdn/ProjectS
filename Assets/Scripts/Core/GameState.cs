using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectS
{
    /// <summary>
    /// Thin run-state owner + game-flow orchestrator (architecture.md: no god-object). Holds the run flags,
    /// key/catch counts, drives the monster tier on pickup, and runs the front-end flow:
    /// MainMenu → Playing → Won/Lost → replay (scene reload). During non-Playing states the player + monster
    /// are frozen. The OnGUI screens are a greybox stand-in for the real front-end (cold-open, menu, options).
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        public enum RunState { MainMenu, Playing, Won, Lost }

        [Header("Rules (architecture.md)")]
        [SerializeField] private int _keysRequired = 3;      // 1 held + 2 found
        [SerializeField] private int _startingHeldKeys = 1;
        [SerializeField] private int _catchesToLose = 3;

        [Header("References (auto-found if empty)")]
        [SerializeField] private MonsterAI _monster;
        [SerializeField] private PlayerController _player;

        public int KeyCount { get; private set; }
        public int CatchCount { get; private set; }
        public RunState State { get; private set; } = RunState.MainMenu;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            KeyCount = _startingHeldKeys;
            if (_monster == null) _monster = FindFirstObjectByType<MonsterAI>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            EnterMainMenu();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (State == RunState.MainMenu && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                BeginRun();
            else if ((State == RunState.Won || State == RunState.Lost) && kb.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // clean replay reset
        }

        private void EnterMainMenu()
        {
            State = RunState.MainMenu;
            Freeze(true);
        }

        private void BeginRun()
        {
            State = RunState.Playing;
            Freeze(false);
            _monster?.OnKeyCollected(KeyCount);                 // set the initial tier for keys held
            FindFirstObjectByType<ScareDirector>()?.OnRunStarted();
        }

        // Freeze/unfreeze the run actors for non-Playing states.
        private void Freeze(bool frozen)
        {
            _player?.SetInputEnabled(!frozen);
            _monster?.SetFrozen(frozen);
        }

        public void CollectKey()
        {
            if (State != RunState.Playing) return;
            KeyCount++;
            _monster?.OnKeyCollected(KeyCount); // pickup escalates the monster
            Debug.Log($"[GameState] Key collected: {KeyCount}/{_keysRequired}");
        }

        /// <summary>Non-fatal strike from a lost QTE. 3 = game over.</summary>
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
            Freeze(true);
            Debug.Log("[GameState] YOU ESCAPED.");
        }

        private void Lose()
        {
            State = RunState.Lost;
            Freeze(true);
            Debug.Log("[GameState] CAUGHT.");
        }

        private void OnGUI()
        {
            float w = Screen.width, h = Screen.height;

            if (State == RunState.MainMenu)
            {
                CenterText("PROJECT S", 64, Color.white, -40f);
                CenterText("Press [Enter] to begin", 24, new Color(0.8f, 0.8f, 0.8f), 40f);
                return;
            }

            // Playing: temporary dev readout (greybox only — near-zero HUD in the ship build).
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(12, 12, 500, 30),
                $"Keys {KeyCount}/{_keysRequired}    Catches {CatchCount}/{_catchesToLose}    {State}", style);

            if (State == RunState.Won || State == RunState.Lost)
            {
                CenterText(State == RunState.Won ? "YOU ESCAPED" : "CAUGHT", 56,
                    State == RunState.Won ? Color.green : Color.red, -40f);
                CenterText("Press [R] to restart", 24, new Color(0.85f, 0.85f, 0.85f), 40f);
            }
        }

        private void CenterText(string text, int size, Color color, float yOffset)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(0, yOffset, Screen.width, Screen.height), text, style);
        }
    }
}
