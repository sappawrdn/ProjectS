using UnityEngine;

namespace ProjectS
{
    /// <summary>
    /// The run's exit. When the player reaches it, asks GameState to win — which only succeeds if the
    /// required keys are held. Proximity-based to match the Key pickup (and the eyes-off design).
    /// </summary>
    public class ExitDoor : MonoBehaviour
    {
        [SerializeField] private float _radius = 2f;

        private Transform _player;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        private void Update()
        {
            if (_player == null) return;
            if (Vector3.Distance(transform.position, _player.position) <= _radius)
                GameState.Instance?.TryExit();
        }
    }
}
