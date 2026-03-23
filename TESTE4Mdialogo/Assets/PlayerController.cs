using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Assign the existing InputActionAsset (e.g. InputSystem_Actions)")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Name of the move action in the asset (default: \"Move\")")]
    [SerializeField] private string moveActionName = "Move";

    [Header("Physics")]
    [Tooltip("Movement force multiplier")]
    [SerializeField] private float speed = 10f;
    [Tooltip("Optional: assign Rigidbody in inspector. If left empty the component on the same GameObject will be used.")]
    [SerializeField] private Rigidbody rb;

    private InputAction moveAction;
    private Vector2 moveInput;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("PlayerController: no InputActionAsset assigned. Assign the existing InputSystem_Actions asset in the inspector.");
            return;
        }

        moveAction = inputActions.FindAction(moveActionName, true);
        if (moveAction == null)
        {
            Debug.LogWarning($"PlayerController: Move action '{moveActionName}' not found in the provided InputActionAsset.");
            return;
        }

        moveAction.performed += OnMovePerformed;
        moveAction.canceled += OnMoveCanceled;
        moveAction.Enable();
    }

    void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.canceled -= OnMoveCanceled;
            moveAction.Disable();
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        moveInput = Vector2.zero;
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        // Convert 2D input (x,y) to world X/Z movement (fixed plane)
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        // Apply force for rolling behaviour
        rb.AddForce(move * speed, ForceMode.Force);
    }
}

