using UnityEngine;

namespace ProjectS
{
    /// <summary>
    /// A collectible key. Auto-collects when the player is within pickup range (proximity, not a physics
    /// trigger — reliable with a CharacterController and a natural base for the Haptic-Primary auto-pickup
    /// dwell later). Optionally reveals the next section on pickup (the maze-switch conceit) via
    /// _revealOnPickup. Keys are the only pickup in the game.
    /// </summary>
    public class Key : MonoBehaviour
    {
        [SerializeField] private float _pickupRadius = 1.5f;
        [SerializeField] private GameObject _revealOnPickup; // optional: next section / next key to enable
        [SerializeField] private float _spinSpeed = 60f;     // gentle idle spin so it reads as a pickup

        private Transform _player;
        private bool _collected;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);

            if (_collected || _player == null) return;
            if (Vector3.Distance(transform.position, _player.position) <= _pickupRadius)
                Collect();
        }

        private void Collect()
        {
            _collected = true;
            GameState.Instance?.CollectKey();
            if (_revealOnPickup != null) _revealOnPickup.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
