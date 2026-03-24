using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Assign the existing InputActionAsset (e.g. InputSystem_Actions)")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Name of the move action in the asset (default: \"Move\")")]
    [SerializeField] private string moveActionName = "Move";
    [Tooltip("Name of the jump action in the asset (default: \"Jump\")")]
    [SerializeField] private string jumpActionName = "Jump";
    [Tooltip("Name of the brake action in the asset (default: \"Brake\")")]
    [SerializeField] private string brakeActionName = "Brake";

    [Header("Movement")]
    [Tooltip("Base movement speed (m/s)")]
    [SerializeField] private float speed = 5f;
    [Tooltip("How fast the character accelerates to target speed")]
    [SerializeField] private float acceleration = 20f;
    [Tooltip("How fast the character decelerates when no input")]
    [SerializeField] private float deceleration = 25f;

    [Header("Brake")]
    [Tooltip("If true, pressing brake will instantly zero horizontal velocity")] 
    [SerializeField] private bool brakeInstantStop = true;
    [Tooltip("If true, brake will be active while holding the key (started/canceled). Otherwise performed triggers instant brake")]
    [SerializeField] private bool brakeHoldMode;
    [Tooltip("Deceleration applied while holding brake (when brakeHoldMode is true)")]
    [SerializeField] private float brakeHoldDeceleration = 40f;

    [Header("Jump & Gravity")]
    [Tooltip("Gravity (negative). Example: -9.81f")] 
    [SerializeField] private float gravity = -9.81f;
    [Tooltip("Jump height in meters")]
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Ground Check")]
    [Tooltip("Optional transform used to check ground (feet). If null, the object's position is used.")]
    [SerializeField] private Transform groundCheckTransform;
    [Tooltip("Radius used for ground overlap check")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [Tooltip("Layers considered ground for the jump check")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("References")]
    [Tooltip("Optional: assign CharacterController. If left empty, one on the same GameObject will be used.")]
    [SerializeField] private CharacterController controller;
    [Tooltip("Optional: assign Animator to drive animations")]
    [SerializeField] private Animator animator;

    // Input actions
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _brakeAction;

    // Animator parameter hashes
    private int _hashSpeed;
    private int _hashIsGrounded;
    private int _hashJump;
    private int _hashBrake;

    // runtime state
    private Vector2 _moveInput;
    private Vector3 _velocity; // world-space velocity; y component is vertical
    private bool _isBraking;

    void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (groundCheckTransform == null)
            groundCheckTransform = this.transform;

        if (animator == null)
            animator = GetComponent<Animator>();

        // Precompute animator hashes for efficiency (safe even if animator is null)
        _hashSpeed = Animator.StringToHash("Speed");
        _hashIsGrounded = Animator.StringToHash("IsGrounded");
        _hashJump = Animator.StringToHash("Jump");
        _hashBrake = Animator.StringToHash("Brake");
    }

    void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("PlayerController: no InputActionAsset assigned. Assign the Input Actions asset in the inspector.");
            return;
        }

        _moveAction = inputActions.FindAction(moveActionName, true);
        if (_moveAction == null)
        {
            Debug.LogWarning($"PlayerController: Move action '{moveActionName}' not found in the InputActionAsset.");
        }
        else
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _moveAction.Enable();
        }

        _jumpAction = inputActions.FindAction(jumpActionName, true);
        if (_jumpAction == null)
        {
            Debug.LogWarning($"PlayerController: Jump action '{jumpActionName}' not found in the InputActionAsset.");
        }
        else
        {
            _jumpAction.performed += OnJumpPerformed;
            _jumpAction.Enable();
        }

        _brakeAction = inputActions.FindAction(brakeActionName, true);
        if (_brakeAction == null)
        {
            Debug.LogWarning($"PlayerController: Brake action '{brakeActionName}' not found in the InputActionAsset.");
        }
        else
        {
            if (brakeHoldMode)
            {
                _brakeAction.started += OnBrakeStarted;
                _brakeAction.canceled += OnBrakeCanceled;
            }
            else
            {
                _brakeAction.performed += OnBrakePerformed;
            }
            _brakeAction.Enable();
        }
    }

    void OnDisable()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
            _moveAction.Disable();
        }

        if (_jumpAction != null)
        {
            _jumpAction.performed -= OnJumpPerformed;
            _jumpAction.Disable();
        }

        if (_brakeAction != null)
        {
            if (brakeHoldMode)
            {
                _brakeAction.started -= OnBrakeStarted;
                _brakeAction.canceled -= OnBrakeCanceled;
            }
            else
            {
                _brakeAction.performed -= OnBrakePerformed;
            }
            _brakeAction.Disable();
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        _moveInput = Vector2.zero;
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (IsGrounded())
        {
            // v = sqrt(2 * g * h) but gravity is negative
            _velocity.y = Mathf.Sqrt(Mathf.Max(0f, jumpHeight * -2f * gravity));
            if (animator != null) animator.SetTrigger(_hashJump);
        }
    }

    private void OnBrakePerformed(InputAction.CallbackContext ctx)
    {
        if (brakeInstantStop)
        {
            // zero horizontal velocity immediately
            _velocity.x = 0f;
            _velocity.z = 0f;
            _isBraking = false;
            if (animator != null) animator.SetTrigger(_hashBrake);
        }
        else
        {
            // if not instant, enable braking briefly
            _isBraking = true;
            if (animator != null) animator.SetTrigger(_hashBrake);
        }
    }

    private void OnBrakeStarted(InputAction.CallbackContext ctx)
    {
        _isBraking = true;
        if (animator != null) animator.SetTrigger(_hashBrake);
    }

    private void OnBrakeCanceled(InputAction.CallbackContext ctx)
    {
        _isBraking = false;
    }

    private bool IsGrounded()
    {
        if (controller != null)
            return controller.isGrounded;

        Vector3 origin = groundCheckTransform != null ? groundCheckTransform.position : transform.position;
        return Physics.CheckSphere(origin + Vector3.down * 0.05f, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        // Horizontal movement (world space relative to character orientation)
        Vector3 desiredMove = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        Vector3 targetVel = desiredMove.normalized * (speed * _moveInput.magnitude);

        // Smoothly accelerate/decelerate horizontal components
        float usedDecel = _isBraking && !brakeInstantStop ? brakeHoldDeceleration : deceleration;
        _velocity.x = Mathf.MoveTowards(_velocity.x, targetVel.x, (_moveInput.magnitude > 0.01f ? acceleration : usedDecel) * Time.deltaTime);
        _velocity.z = Mathf.MoveTowards(_velocity.z, targetVel.z, (_moveInput.magnitude > 0.01f ? acceleration : usedDecel) * Time.deltaTime);

        // Apply gravity
        if (IsGrounded() && _velocity.y < 0f)
        {
            // small negative value to keep the controller grounded
            _velocity.y = -2f;
        }
        _velocity.y += gravity * Time.deltaTime;

        // Move character
        if (controller != null)
        {
            controller.Move(_velocity * Time.deltaTime);
        }
        else
        {
            // fallback: move transform
            transform.position += _velocity * Time.deltaTime;
        }

        // Update animator params
        if (animator != null)
        {
            Vector3 horizontalVel = new Vector3(_velocity.x, 0f, _velocity.z);
            animator.SetFloat(_hashSpeed, horizontalVel.magnitude);
            animator.SetBool(_hashIsGrounded, IsGrounded());
        }

        // If one-off brake (non-hold) was used, stop braking after applying
        if (_isBraking && !brakeHoldMode && !brakeInstantStop)
        {
            // apply a single frame strong deceleration then clear
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, brakeHoldDeceleration * Time.deltaTime);
            _velocity.z = Mathf.MoveTowards(_velocity.z, 0f, brakeHoldDeceleration * Time.deltaTime);
            _isBraking = false;
        }
    }
}
