
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    private enum PlayerState
    {
        Grounded,
        Charging,
        Airborne
    }

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Visual References")]
    [SerializeField] private Transform playerVisuals;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float modelHeightOffset = 0f;

    [Header("Grounded Orbit")]
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float minRadius = 1f;
    [SerializeField] private float maxRadius = 2f;
    [SerializeField] private float radiusChangeInterval = 1f;
    [SerializeField] private float radiusChangeSpeed = 2f;

    [Header("Jump")]
    [SerializeField] private float minJumpSpeed = 4f;
    [SerializeField] private float maxJumpSpeed = 12f;
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private float verticalJumpSpeed = 8f;

    [Header("Air Rotation")]
    [SerializeField] private float airRotationSpeed = 180f;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;

    private PlayerState currentState = PlayerState.Grounded;

    private Vector2 moveInput;

    private float orbitAngle;
    private float currentRadius;
    private float targetRadius;
    private float radiusTimer;

    private Vector3 currentOrbitDirection;
    private Vector3 lockedJumpDirection;

    private float chargeTime;
    private bool jumpRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints = RigidbodyConstraints.FreezeRotation;

        orbitAngle = Random.Range(0f, 360f);

        currentRadius = Random.Range(minRadius, maxRadius);
        targetRadius = currentRadius;

        UpdateOrbitDirection();
        UpdateModelPosition();
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.action.actionMap.Enable();
            moveAction.action.performed += OnMove;
            moveAction.action.canceled += OnMove;
        }

        if (jumpAction != null)
        {
            jumpAction.action.actionMap.Enable();
            jumpAction.action.started += OnJumpStarted;
            jumpAction.action.canceled += OnJumpReleased;
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.action.performed -= OnMove;
            moveAction.action.canceled -= OnMove;
        }

        if (jumpAction != null)
        {
            jumpAction.action.started -= OnJumpStarted;
            jumpAction.action.canceled -= OnJumpReleased;
        }
    }

    private void Update()
    {
        HandleGroundedOrbit();
        HandleJumpCharge();
        HandleAirRotation();
    }

    private void FixedUpdate()
    {
        CheckGround();

        if (jumpRequested)
        {
            ExecuteJump();
        }
    }

    // INPUT

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        if (currentState != PlayerState.Grounded)
            return;

        lockedJumpDirection = currentOrbitDirection;

        chargeTime = 0f;
        moveInput = Vector2.zero;

        currentState = PlayerState.Charging;
    }

    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        if (currentState == PlayerState.Charging)
        {
            jumpRequested = true;
        }
    }

    // GROUNDED ORBIT

    private void HandleGroundedOrbit()
    {
        if (currentState != PlayerState.Grounded)
            return;

        orbitAngle += rotationSpeed * Time.deltaTime;

        radiusTimer += Time.deltaTime;

        if (radiusTimer >= radiusChangeInterval)
        {
            radiusTimer = 0f;
            targetRadius = Random.Range(minRadius, maxRadius);
        }

        currentRadius = Mathf.MoveTowards(
            currentRadius,
            targetRadius,
            radiusChangeSpeed * Time.deltaTime
        );

        UpdateOrbitDirection();
        UpdateModelPosition();
    }

    private void UpdateOrbitDirection()
    {
        float angleRadians = orbitAngle * Mathf.Deg2Rad;

        currentOrbitDirection = new Vector3(
            Mathf.Sin(angleRadians),
            0f,
            Mathf.Cos(angleRadians)
        ).normalized;
    }

    private void UpdateModelPosition()
    {
        if (playerVisuals == null)
            return;

        playerVisuals.localPosition =
            currentOrbitDirection * currentRadius +
            Vector3.up * modelHeightOffset;

        playerVisuals.localRotation =
            Quaternion.LookRotation(currentOrbitDirection, Vector3.up);
    }

    // JUMP CHARGING

    private void HandleJumpCharge()
    {
        if (currentState != PlayerState.Charging)
            return;

        chargeTime += Time.deltaTime;

        chargeTime = Mathf.Min(chargeTime, maxChargeTime);
    }

    // JUMP EXECUTION

    private void ExecuteJump()
    {
        jumpRequested = false;

        float chargeRatio = maxChargeTime > 0f
            ? chargeTime / maxChargeTime
            : 1f;

        float horizontalSpeed = Mathf.Lerp(
            minJumpSpeed,
            maxJumpSpeed,
            chargeRatio
        );

        Vector3 jumpVelocity =
            lockedJumpDirection * horizontalSpeed +
            Vector3.up * verticalJumpSpeed;

        rb.linearVelocity = jumpVelocity;

        if (playerVisuals != null)
        {
            playerVisuals.localPosition =
                Vector3.up * modelHeightOffset;
        }

        chargeTime = 0f;
        moveInput = Vector2.zero;

        currentState = PlayerState.Airborne;
    }

    // AIRBORNE ROTATION

    private void HandleAirRotation()
    {
        if (currentState != PlayerState.Airborne)
            return;

        if (playerVisuals == null)
            return;

        playerVisuals.Rotate(
            Vector3.up,
            moveInput.x * airRotationSpeed * Time.deltaTime,
            Space.World
        );

        playerVisuals.Rotate(
            Vector3.right,
            moveInput.y * airRotationSpeed * Time.deltaTime,
            Space.Self
        );
    }

    // GROUND DETECTION

    private void CheckGround()
    {
        if (currentState == PlayerState.Charging)
            return;

        if (groundCheck == null)
            return;

        // Only detect landing while descending or nearly stationary.
        if (rb.linearVelocity.y > 0.1f)
            return;

        bool wasGrounded = currentState == PlayerState.Grounded;

        bool foundGround = Physics.SphereCast(
            groundCheck.position,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        if (foundGround)
        {
            PogoSurface surface =
                hit.collider.GetComponentInParent<PogoSurface>();

            if (surface != null && hit.normal.y > 0.5f)
            {
                currentState = PlayerState.Grounded;

                if (!wasGrounded)
                {
                    moveInput = Vector2.zero;
                }

                if (rb.linearVelocity.y < 0f)
                {
                    rb.linearVelocity = new Vector3(
                        rb.linearVelocity.x,
                        0f,
                        rb.linearVelocity.z
                    );
                }

                return;
            }
        }

        if (currentState == PlayerState.Grounded)
        {
            currentState = PlayerState.Airborne;
        }
    }
}
