using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

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

        [Header("Touch (device) — left half = move joystick, right half = look drag")]
        [SerializeField] private float _joystickRadius = 140f;        // px drag for full-speed move
        [SerializeField] private float _touchLookSensitivity = 0.12f; // degrees per px dragged

        [Header("Haptic-Primary / Nightmare (eyes-off): gyro-aim + hold-to-walk")]
        [SerializeField] private float _gyroSensitivity = 55f;  // degrees per (rad/s) of phone turn
        [SerializeField] private float _gyroYawSign = -1f;      // FLIP if turning aims the wrong way (device-dependent, GDD)
        [SerializeField] private bool _gyroPitch = false;       // also tilt-to-pitch? off = yaw-only (steadier)
        [SerializeField] private float _gyroPitchSign = 1f;

        [Header("Wall-bump feedback (both modes)")]
        [SerializeField] private float _wallBumpThreshold = 0.6f;     // fraction of intended move blocked = a real bonk
        [SerializeField] private float _wallBumpCooldown = 0.35f;     // debounce so sliding along a wall doesn't buzz

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
        private Vector2 _touchMove; // from the left-half joystick (0..1 magnitude)
        private Vector2 _touchLook; // this frame's right-half drag delta (px)
        private float _bumpTimer;   // wall-bump debounce

        // How much control the player currently has (drops with catches): 0->1, 1->0.6, 2+->0.45.
        public float ControlFactor => _controlFactor;

        // Eyes-off mode: gyro-aim + hold-to-walk replace the on-screen controls.
        private static bool NightmareOn => HapticPrimaryController.Instance != null && HapticPrimaryController.Instance.Enabled;

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
            EnhancedTouchSupport.Enable(); // touch reading on device (harmless/idle on desktop)
            var gyro = UnityEngine.InputSystem.Gyroscope.current; // eyes-off gyro-aim (device only)
            if (gyro != null) UnityEngine.InputSystem.InputSystem.EnableDevice(gyro);
            if (_lockCursor) Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _lookAction.Disable();
            EnhancedTouchSupport.Disable();
            if (_lockCursor) Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            if (_inputEnabled)
            {
                ReadTouch(); // fills _touchMove + _touchLook for this frame
                Look();
                Move();
            }
            else { _touchMove = Vector2.zero; _touchLook = Vector2.zero; }
            UpdateFov();
        }

        // Left half of the screen = a floating move joystick; right half = a look drag. Multi-touch, so one
        // finger can walk while another looks. Desktop has no active touches → both stay zero (WASD/mouse win).
        private void ReadTouch()
        {
            _touchMove = Vector2.zero;
            _touchLook = Vector2.zero;
            if (!EnhancedTouchSupport.enabled) return;

            if (NightmareOn)
            {
                // Hold-to-walk: ANY held touch = walk forward in the facing direction (a single Taptic engine can't
                // do left/right, so you aim by turning and walk by holding). No joystick, no look-drag.
                if (ETouch.activeTouches.Count > 0) _touchMove = new Vector2(0f, 1f);
                return;
            }

            float half = Screen.width * 0.5f;
            foreach (var t in ETouch.activeTouches)
            {
                if (t.startScreenPosition.x < half) // left → move
                    _touchMove = Vector2.ClampMagnitude((t.screenPosition - t.startScreenPosition) / _joystickRadius, 1f);
                else                                 // right → look (accumulate this frame's drag)
                    _touchLook += t.delta;
            }
        }

        private void Look()
        {
            // Mouse always works (editor convenience). In Nightmare mode the phone's GYRO aims (look-drag is off —
            // a held touch walks instead); in normal mode the right-half look-drag applies.
            Vector2 mouse = _lookAction.ReadValue<Vector2>();
            float yawAdd = mouse.x * _lookSensitivity;
            float pitchAdd = -mouse.y * _lookSensitivity;

            if (NightmareOn)
            {
                var gyro = UnityEngine.InputSystem.Gyroscope.current;
                if (gyro != null)
                {
                    Vector3 av = gyro.angularVelocity.ReadValue(); // rad/s, device-local
                    yawAdd += _gyroYawSign * av.y * _gyroSensitivity * Time.deltaTime;
                    if (_gyroPitch) pitchAdd += _gyroPitchSign * av.x * _gyroSensitivity * Time.deltaTime;
                }
            }
            else
            {
                yawAdd += _touchLook.x * _touchLookSensitivity;
                pitchAdd -= _touchLook.y * _touchLookSensitivity;
            }

            _yaw += yawAdd;
            _pitch = Mathf.Clamp(_pitch + pitchAdd, _pitchMin, _pitchMax);

            // Yaw turns the body; pitch tilts only the camera.
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_camera != null)
                _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move()
        {
            Vector2 input = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>() + _touchMove, 1f);
            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            wish = Vector3.ClampMagnitude(wish, 1f) * (_moveSpeed * _controlFactor);

            // Gravity (keep a small downward push while grounded so the CC stays snapped).
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += _gravity * Time.deltaTime;

            Vector3 before = transform.position;
            float desired = wish.magnitude * Time.deltaTime; // intended horizontal distance this frame
            Vector3 velocity = wish + Vector3.up * _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);

            // Wall bump: you tried to move but a wall ate most of it → a real bonk (impact-only, debounced, so
            // sliding along a wall doesn't buzz). Strength scales with how much was blocked.
            if (_bumpTimer > 0f) _bumpTimer -= Time.deltaTime;
            if (desired > 0.01f && _bumpTimer <= 0f)
            {
                Vector3 moved = transform.position - before; moved.y = 0f;
                float blocked = desired - moved.magnitude;
                if (blocked > _wallBumpThreshold * desired)
                {
                    float strength = Mathf.Clamp01(blocked / Mathf.Max(desired, 1e-4f));
                    HapticManager.Instance?.WallBump(strength);
                    AudioDirector.Instance?.WallBump(strength);
                    _bumpTimer = _wallBumpCooldown;
                }
            }
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
