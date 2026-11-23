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

    private const float TrackJumpBufferTime = 0.19f;
    private const float TrackAirTurnRate = 4.8f;
    [SerializeField] private float trackSpinSpeed = 135f;
    [SerializeField] private float trackJumpSpeed = 3.55f;
    [SerializeField] private float trackMinJumpHeightSpeed = 4.35f;
    [SerializeField] private float trackMaxJumpHeightSpeed = 6f;

    [SerializeField] private float trackChargeTime = 0.52f;
    [SerializeField] private float trackWobble = 8f;

    private float JumpChargeDuration => useTrackSteering ? trackChargeTime : maxChargeTime;

    [SerializeField] private bool useTrackSteering;
    [SerializeField] private BarnJumpEffects jumpEffects;

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

    [Header("Grounded Orbit Pitch")]
    [SerializeField] private float minOrbitPitch = -35f;
    [SerializeField] private float maxOrbitPitch = 0f;
    [SerializeField] private float pitchChangeSpeed = 90f;

    [Header("Jump")]
    [SerializeField] private float minJumpSpeed = 4f;
    [SerializeField] private float maxJumpSpeed = 12f;
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private float verticalJumpSpeed = 8f;

    [Header("Air Rotation")]
    [SerializeField] private float airRotationSpeed = 360f;

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

    private float currentOrbitPitch;
    private float targetOrbitPitch;

    private float radiusTimer;

    private Vector3 currentOrbitDirection;
    private Vector3 lockedJumpDirection;

    private float chargeTime;

    private bool jumpRequested;
    private float bufferedJumpUntil = float.NegativeInfinity;
    private bool bufferedJumpReleased;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints = RigidbodyConstraints.FreezeRotation;

        orbitAngle = useTrackSteering ? 0f : Random.Range(0f, 360f);

        currentRadius = Random.Range(minRadius, maxRadius);
        targetRadius = currentRadius;

        currentOrbitPitch = useTrackSteering ? 0f : Random.Range(minOrbitPitch, maxOrbitPitch);

        targetOrbitPitch = currentOrbitPitch;

        UpdateOrbitDirection();
        if (useTrackSteering)
            UpdateTrackWobble();
        else
            UpdateModelPosition();
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.action.actionMap.Enable();

            moveAction.action.performed += OnMove;
            moveAction.action.canceled += OnMove;

            moveInput = moveAction.action.ReadValue<Vector2>();
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
        if (useTrackSteering)
            UpdateTrackHeading();

        HandleGroundedOrbit();
        HandleJumpCharge();
        HandleAirRotation();
    }

    private void FixedUpdate()
    {
        CheckGround();

        if (useTrackSteering && currentState == PlayerState.Grounded && Time.time <= bufferedJumpUntil)
        {
            BeginJumpCharge();
            jumpRequested = bufferedJumpReleased;
            bufferedJumpUntil = float.NegativeInfinity;
        }

        if (jumpRequested)
        {
            ExecuteJump();
        }
        else if (useTrackSteering && currentState == PlayerState.Airborne)
        {
            ApplyTrackAirSteering();
        }
        else if (useTrackSteering)
        {
            Vector3 velocity = rb.linearVelocity;
            float grip = Mathf.Exp(-6f * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(velocity.x * grip, velocity.y, velocity.z * grip);
        }
    }

    // INPUT

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        if (currentState == PlayerState.Grounded)
        {
            BeginJumpCharge();
        }
        else if (useTrackSteering && currentState == PlayerState.Airborne)
        {
            bufferedJumpUntil = Time.time + TrackJumpBufferTime;
            bufferedJumpReleased = false;
        }
    }

    private void BeginJumpCharge()
    {
        // Preserve the horizontal launch direction.
        // The visual pitch does not affect jump direction.

        lockedJumpDirection = new Vector3(
            currentOrbitDirection.x,
            0f,
            currentOrbitDirection.z
        ).normalized;

        if (useTrackSteering)
            UpdateTrackJumpAim();

        chargeTime = 0f;
        currentState = PlayerState.Charging;
    }

    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        if (currentState == PlayerState.Charging)
        {
            jumpRequested = true;
        }
        else if (useTrackSteering && Time.time <= bufferedJumpUntil)
        {
            bufferedJumpReleased = true;
        }
    }

    private void UpdateTrackJumpAim()
    {
        lockedJumpDirection = currentOrbitDirection;
        if (playerVisuals != null)
            playerVisuals.localRotation = Quaternion.LookRotation(lockedJumpDirection);
    }

    private void UpdateTrackHeading()
    {
        if (currentState != PlayerState.Grounded)
            return;

        orbitAngle = Mathf.Repeat(orbitAngle + trackSpinSpeed * Time.deltaTime, 360f);
        UpdateOrbitDirection();
    }

    private void ApplyTrackAirSteering()
    {
        if (moveInput.sqrMagnitude <= 0.01f)
            return;

        Vector3 velocity = rb.linearVelocity;
        var horizontal = new Vector2(velocity.x, velocity.z);
        float speed = Mathf.Max(horizontal.magnitude, minJumpSpeed);
        Vector2 desired = moveInput.normalized * speed;
        Vector2 steered = Vector2.MoveTowards(horizontal, desired, TrackAirTurnRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(steered.x, velocity.y, steered.y);
    }

    // GROUNDED ORBIT

    private void HandleGroundedOrbit()
    {
        if (currentState != PlayerState.Grounded)
            return;

        if (useTrackSteering)
        {
            UpdateTrackWobble();
            return;
        }

        // Continue rotating horizontally.

        orbitAngle += rotationSpeed * Time.deltaTime;

        // Periodically choose new orbit parameters.

        radiusTimer += Time.deltaTime;

        if (radiusTimer >= radiusChangeInterval)
        {
            radiusTimer = 0f;

            targetRadius = Random.Range(
                minRadius,
                maxRadius
            );

            targetOrbitPitch = Random.Range(
                minOrbitPitch,
                maxOrbitPitch
            );
        }

        // Smoothly transition radius.

        currentRadius = Mathf.MoveTowards(
            currentRadius,
            targetRadius,
            radiusChangeSpeed * Time.deltaTime
        );

        // Smoothly transition downward pitch.

        currentOrbitPitch = Mathf.MoveTowards(
            currentOrbitPitch,
            targetOrbitPitch,
            pitchChangeSpeed * Time.deltaTime
        );

        UpdateOrbitDirection();
        UpdateModelPosition();
    }

    private void UpdateOrbitDirection()
    {
        float yawRadians = orbitAngle * Mathf.Deg2Rad;
        float pitchRadians = currentOrbitPitch * Mathf.Deg2Rad;

        float horizontalScale = Mathf.Cos(pitchRadians);

        currentOrbitDirection = new Vector3(
            Mathf.Sin(yawRadians) * horizontalScale,
            Mathf.Sin(pitchRadians),
            Mathf.Cos(yawRadians) * horizontalScale
        ).normalized;
    }

    private void UpdateTrackWobble()
    {
        if (playerVisuals == null)
            return;

        float sway = Time.time * 10f;
        playerVisuals.localPosition = Vector3.up * modelHeightOffset;
        playerVisuals.localRotation = Quaternion.Euler(0f, orbitAngle, 0f)
            * Quaternion.Euler(Mathf.Sin(sway) * trackWobble, 0f, Mathf.Cos(sway) * trackWobble * 0.7f);
    }

    private void UpdateModelPosition()
    {
        if (playerVisuals == null)
            return;

        // Move the entire visual assembly around PlayerRoot.

        playerVisuals.localPosition =
            currentOrbitDirection * currentRadius +
            Vector3.up * modelHeightOffset;

        // Face in the direction of the orbit,
        // including its downward pitch.

        playerVisuals.localRotation =
            Quaternion.LookRotation(
                currentOrbitDirection,
                Vector3.up
            );
    }

    // JUMP CHARGING

    private void HandleJumpCharge()
    {
        if (currentState != PlayerState.Charging)
            return;

        if (jumpRequested)
            return;

        chargeTime += Time.deltaTime;

        chargeTime = Mathf.Min(
            chargeTime,
            JumpChargeDuration
        );
    }

    // JUMP EXECUTION

    private void ExecuteJump()
    {
        jumpRequested = false;

        float duration = JumpChargeDuration;
        float chargeRatio = duration > 0f
            ? chargeTime / duration
            : 1f;

        float horizontalSpeed = useTrackSteering ? trackJumpSpeed : Mathf.Lerp(minJumpSpeed, maxJumpSpeed, chargeRatio);
        float verticalSpeed = useTrackSteering
            ? Mathf.Lerp(trackMinJumpHeightSpeed, trackMaxJumpHeightSpeed, chargeRatio)
            : verticalJumpSpeed;

        Vector3 jumpVelocity = lockedJumpDirection * horizontalSpeed + Vector3.up * verticalSpeed;

        rb.linearVelocity = jumpVelocity;
        if (jumpEffects != null && groundCheck != null)
            jumpEffects.Emit(groundCheck.position + Vector3.down * 0.20f, lockedJumpDirection);

        if (playerVisuals != null)
        {
            playerVisuals.localPosition =
                Vector3.up * modelHeightOffset;
        }

        chargeTime = 0f;

        currentState = PlayerState.Airborne;
    }

    // AIRBORNE ROTATION

    private void HandleAirRotation()
    {
        if (currentState != PlayerState.Airborne)
            return;

        if (playerVisuals == null)
            return;

        if (useTrackSteering)
        {
            Quaternion lean = Quaternion.Euler(moveInput.y * 4f, orbitAngle, -moveInput.x * 6f);
            playerVisuals.localRotation = Quaternion.Slerp(playerVisuals.localRotation, lean, 12f * Time.deltaTime);
            return;
        }

        // Horizontal input controls yaw.

        playerVisuals.Rotate(
            Vector3.up,
            moveInput.x * airRotationSpeed * Time.deltaTime,
            Space.World
        );

        // Vertical input controls pitch.

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

        // Only check for landing while descending
        // or nearly stationary.

        if (rb.linearVelocity.y > 0.1f)
            return;

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
