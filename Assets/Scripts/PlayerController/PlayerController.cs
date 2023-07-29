using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[SelectionBase, RequireComponent(typeof(PlayerInput), typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement related:")]
    [SerializeField] float movementSpeed = 5f;
    [SerializeField] float maxXVelocity = 12f;
    private Vector2 movement = Vector2.zero;
    [Header("Jump related:")]
    [SerializeField] float jumpForceUpMultiplier = 10f;
    [SerializeField] AnimationCurve jumpCurve;
    [SerializeField] int maxNumberOfJumps = 1;
    [SerializeField] float fallSpeedMultiplier = 5f;
    [SerializeField] float jumpTime = .5f;
    [SerializeField] GameObject dustLandPrefab;
    bool fallFaster;
    int currentNumberOfJumps;
    bool canJump = true;

    [Header("Dash related:")]
    [SerializeField] float dashDuration = 1f;
    [SerializeField] float dashForce = 10f;
    [SerializeField] AnimationCurve dashCurve;
    [SerializeField] float dashCooldown = 1f;
    bool canDash = true;
    Vector2 dashDirection = Vector2.zero;

    // Make it an event.
    public Action InteractionHandler;

    InputActionAsset inputAsset;
    InputActionMap actionMap;
    InputAction moveAction;
    InputAction jumpAction;
    InputAction swapAction;
    InputAction interactAction;
    InputAction dashAction;

    public event Action<bool> OnSwappingCall;
    public bool CanSwap { get; set; } = true;
    public Rigidbody Rb { get => rb; set => rb = value; }

    bool wantsToSwap;

    [SerializeField] PhysicMaterial physicMaterial;
    Rigidbody rb;  
    Collider col;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Get the input action.
        //controls = new PlayerControls();
        //actionMap = controls.Player;
        inputAsset = GetComponent<PlayerInput>().actions;
        actionMap = inputAsset.FindActionMap("Player");
        actionMap.Enable();
    }  

    private void OnEnable()
    {
        moveAction = actionMap.FindAction("Move");
        moveAction.performed += (context) => { 
            movement.x = context.ReadValue<Vector2>().x;
            dashDirection = movement;
            // Check if rotation really works...
            if (movement.x > 0) transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            else if (movement.x < 0)
                transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        };
        moveAction.canceled += (context) => {
            // Check if rotation really works...
            if (movement.x > 0) transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            else if (movement.x < 0)
                transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            movement = Vector2.zero; };
        moveAction.Enable();

        jumpAction = actionMap.FindAction("Jump");
        jumpAction.performed += OnJump;
        jumpAction.Enable();

        swapAction = actionMap.FindAction("Swap");
        swapAction.performed += (context) => {
            wantsToSwap = true;
            OnSwappingCall?.Invoke(wantsToSwap);
            Debug.Log("context.duration");
        };
        swapAction.canceled += (context) => {
            wantsToSwap = false;
            OnSwappingCall?.Invoke(wantsToSwap);
        };
        swapAction.Enable();

        interactAction = actionMap.FindAction("Interact");
        interactAction.performed += OnInteract;
        interactAction.Enable();

        dashAction = actionMap.FindAction("Dash");
        dashAction.performed += OnDash;
        dashAction.Enable();

    }

    private void Update()
    {
        
    }

    private void FixedUpdate()
    {
        // Move the player.
        if(Mathf.Abs(rb.velocity.x) < maxXVelocity)
            Rb.AddForce(movement * movementSpeed);
        if (fallFaster)
        {
            Rb.AddForce(Vector2.down * fallSpeedMultiplier);
            //rb.velocity += fallSpeedMultiplier * Time.deltaTime * Physics.gravity.y * Vector3.up;
        }
        // Make better clamp!
        //if (rb.velocity.magnitude > maxVelocity)
        //    rb.velocity = rb.velocity.normalized * maxVelocity;

    }

    #region Interaction
    private void OnInteract(InputAction.CallbackContext context)
    {
        InteractionHandler?.Invoke();
    }
    #endregion

    #region Dash
    private void OnDash(InputAction.CallbackContext context)
    {
        if (canDash)
        {
            canDash = false;
            Rb.useGravity = false;
            bool storeJump = canJump;
            canJump = false;
            //rb.AddForce(dashDirection * dashForce, ForceMode.Impulse);
            // Zero out y velocity.
            //Vector2 velo = rb.velocity;
            //velo.y = 0f;
            //rb.velocity = velo;
            StartCoroutine(StartDashTimer(dashDuration, storeJump));
        }
    }

    IEnumerator StartDashTimer(float dashDuration, bool jumpStore)
    {
        float dashTimer = 0f;
        while (dashTimer < dashDuration)
        {
            dashTimer += Time.deltaTime;
            Rb.velocity = new Vector2(dashCurve.Evaluate(dashTimer/dashDuration)*dashForce*dashDirection.x, 0f);
            //dashDuration -= Time.deltaTime;
            yield return null;
        }
        Rb.useGravity = true;
        canJump = jumpStore;
        StartCoroutine(StartDashCooldown(dashCooldown));
    }

    IEnumerator StartDashCooldown(float dashCooldown)
    {
        while (dashCooldown > 0)
        {
            dashCooldown -= Time.deltaTime;
            yield return null;
        }
        canDash = true;
    }
    #endregion

    #region Jump
    private void OnJump(InputAction.CallbackContext context)
    {
        if (canJump && currentNumberOfJumps > 0)
        {
            StartCoroutine(StartJumpSpeedUp(jumpTime));
            currentNumberOfJumps--;
            //canJump = currentNumberOfJumps <= 0 ? false : true;
        }
    }

    IEnumerator StartJumpSpeedUp(float jumpTime)
    {
        float timer = 0f;
        while (timer < jumpTime)
        {
            timer += Time.deltaTime;
            rb.velocity += new Vector3(0f, jumpCurve.Evaluate(timer / jumpTime) * jumpForceUpMultiplier * Time.deltaTime,0f);
            //rb.AddForce(jumpCurve.Evaluate(timer / jumpTime) * jumpForceUpMultiplier * Vector2.up);
            yield return null;
        }
        fallFaster = true;
    }

    public void ResetJumps()
    {
        canJump = true;
        currentNumberOfJumps = maxNumberOfJumps;
        col.material = null;
        fallFaster = false;
        Instantiate(dustLandPrefab, transform.position, Quaternion.identity);
    }

    public void LeftGround()
    {
        col.material = physicMaterial;
    }
    #endregion

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.transform.TryGetComponent(out Diamond controller))
        {
            controller.Explode(10, collision.GetContact(0).point, 0.6f, 2f);
            CameraShaker.Instance.ShakeCamera(2f, 0.5f);
        }
    }
}
