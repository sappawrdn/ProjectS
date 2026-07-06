using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectS
{
    /// <summary>
    /// First-person movement + look on a CharacterController capsule. Phase 1 uses desktop input
    /// (WASD + mouse) so we can iterate in the editor; on-screen touch controls come when we move to
    /// device. All tuning is [SerializeField] with playtested starting values from architecture.md.
    ///
    /// controlFactor + the FOV ladder are exposed now but driven later by the catch/recoil system
    /// (heavier controls + walls-closing-in FOV as catches accrue).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement (architecture.md tuning)")]
        [SerializeField] private float _moveSpeed = 2.5f;   // base metres/second
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _eyeHeight = 1.6f;   // camera height above the capsule base

        [Header("Look")]
        [SerializeField] private float _lookSensitivity = 0.08f; // degrees per mouse count
        [SerializeField] private float _pitchMin = -80f;
        [SerializeField] private float _pitchMax = 80f;
        [SerializeField] private bool _lockCursor = true;

        [Header("Camera + FOV ladder (driven later by the catch system)")]
        [SerializeField] private Camera _camera;
        [SerializeField] private float _baseFov = 60f;
        [SerializeField] private float _minFov = 38f;
        [SerializeField] private float _fovLerpSpeed = 4f;

        [Header("Control penalty (1 = full control; catch system lowers it)")]
        [SerializeField, Range(0f, 1f)] private float _controlFactor = 1f;
        // Design (architecture.md) wants this ON — crippled speed is balanced by the breathing-room loop.
        // Off for now while greyboxing so movement stays snappy to test with.
        [SerializeField] private bool _applyControlPenalty = false;

        [Header("Flashlight")]
        [SerializeField] private Light _flashlight;

        private CharacterController _cc;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;
        private int _catchCount;
        private bool _inputEnabled = true;

        // How much control the player currently has (drops with catches): 0->1, 1->0.6, 2+->0.45.
        public float ControlFactor => _controlFactor;

        /// <summary>Freeze/unfreeze look+move (QTE overlays). Re-enabling reads fresh input so nothing
        /// stale carries through — the prototype's post-QTE drift bug.</summary>
        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;

        /// <summary>Catch ladder (architecture.md): heavier controls + constricting FOV as catches accrue.</summary>
        public void ApplyCatch(int catchCount)
        {
            _catchCount = catchCount; // FOV ladder always applies (fear cue)
            _controlFactor = _applyControlPenalty
                ? (catchCount <= 0 ? 1f : (catchCount == 1 ? 0.6f : 0.45f))
                : 1f; // penalty off → full speed
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            if (_camera != null)
            {
                // Keep the camera at eye height on the capsule.
                var local = _camera.transform.localPosition;
                _camera.transform.localPosition = new Vector3(local.x, _eyeHeight, local.z);
                _camera.fieldOfView = _baseFov;
            }

            // Move: WASD as a 2D vector.
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Look: raw mouse delta.
            _lookAction = new InputAction("Look", InputActionType.Value, binding: "<Mouse>/delta");

            _yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _lookAction.Enable();
            if (_lockCursor) Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _lookAction.Disable();
            if (_lockCursor) Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            if (_inputEnabled)
            {
                Look();
                Move();
            }
            UpdateFov();
        }

        private void Look()
        {
            Vector2 delta = _lookAction.ReadValue<Vector2>();
            _yaw += delta.x * _lookSensitivity;
            _pitch -= delta.y * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

            // Yaw turns the body; pitch tilts only the camera.
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_camera != null)
                _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move()
        {
            Vector2 input = _moveAction.ReadValue<Vector2>();
            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            wish = Vector3.ClampMagnitude(wish, 1f) * (_moveSpeed * _controlFactor);

            // Gravity (keep a small downward push while grounded so the CC stays snapped).
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += _gravity * Time.deltaTime;

            Vector3 velocity = wish + Vector3.up * _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);
        }

        private void UpdateFov()
        {
            if (_camera == null) return;
            // FOV constricts as catches accrue (walls-closing-in). Catch system will set _catchCount later.
            float targetFov = Mathf.Max(_minFov, _baseFov - 9f * _catchCount);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, _fovLerpSpeed * Time.deltaTime);
        }
    }
}
