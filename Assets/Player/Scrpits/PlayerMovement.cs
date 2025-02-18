using System;
using System.Collections;
using Cinemachine;
using Cinemachine.Utility;
using Unity.VisualScripting;
using UnityEditor.Experimental.RestService;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerMovement : MonoBehaviour
{
    // Components
    private Rigidbody _rigidbody;

    // Move smooth
    private Vector3 currVelocity;
    private float turnVelocity;

    // Move Factors
    [Header("Movement Speeds")]
    [Space(15)]
    [Range(1, 100)] [SerializeField] private float PlayerBaseSpeed;
    [Range(0, 2)] [SerializeField] private float PlayerSprintMultiplier;
    [Range(0, 2)] [SerializeField] private float PlayerCrouchMultiplier;
    [Range(0, 20)] [SerializeField] private float PlayerJumpForce;
    [Range(0, 4)] [SerializeField] private float PlayerDoubleJumpMultiplier;
    [Range(0, 2)] [SerializeField] private float PlayerAirMoveMultiplier;
    [Range(0, 100)] [SerializeField] private float PlayerFallForce;
    [Range(0, 100)] [SerializeField] private float PlayerClimbSpeed;
    [SerializeField] private bool CanSecondJump;
    [SerializeField] private bool directionalSecondJump;
    
    private Vector2 _moveInput;
    private bool _jumpInput;
    // private bool PlayerSecondJump;
    private bool _sprintInput;
    private bool _crouchInput;
    private Vector3 camDirection;
    
    //Camera Reference
    [Header("Camera Reference")]
    [Space(15)]
    private CinemachineFreeLook thridPersonCamera;
    private Camera mainCamera;
    [SerializeField] private PlayerFollower _playerFollower;
    
    //Checks
    [Header("Player Checks")]
    [Space(15)]
    [SerializeField] private LayerMask WhatIsGround;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask WhatIsClimb;
    [SerializeField] private Transform climbCheck;
    [SerializeField] private float MaxSlopeAngle;
    private float curSlopeAngle;
    private Vector3 playerForward;
    private RaycastHit groundHitInfo;
    private RaycastHit climbHitInfo;
    
    
    [HideInInspector]public bool isGrounded;
    private bool wasGrounded;
    private bool isJumping;
    
    [Header("Player Events")]
    [Space(15)]
    [SerializeField] UnityEvent OnJumpEvent;
    [SerializeField] UnityEvent OnLandEvent;
    [SerializeField] UnityEvent OnSprintEvent;
    [SerializeField] UnityEvent OnCrouchEvent;

    private void Awake()
    {
        // Get rigidbody
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        thridPersonCamera = FindObjectOfType<CinemachineFreeLook>();
        if(!thridPersonCamera.CompareTag("MainCamera"))
            Debug.LogError("Cannot find thirdPersonCamera");
        
        var cameras = FindObjectsOfType<Camera>();
        foreach (var camera in cameras)
        {
            if (camera.CompareTag("MainCamera"))
                mainCamera = camera;
        }
        if(!mainCamera.CompareTag("MainCamera"))
            Debug.LogError("Cannot find mainCamera");
    }

    private void Update()
    {
        // Start by checking if the player is grounded
        PreformChecks();

        if (CheckClimbing())
        {
            Climb();
        }

        if (isGrounded)
            GroundMovement();
        else
            AirMovement();

        InvokeEvents();
    }

    private void LateUpdate()
    {
        _jumpInput = false;
    }

    #region GetInputs

    public void GetJump(InputAction.CallbackContext context)
    {
        _jumpInput = context.action.WasPerformedThisFrame();
    }

    public void GetMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void GetSprint(InputAction.CallbackContext context)
    {
        _sprintInput = context.action.WasPerformedThisFrame();
    }

    public void GetCrouch(InputAction.CallbackContext context)
    {
        _crouchInput = context.action.WasPerformedThisFrame();
    }
    
    public void GetCamera(InputAction.CallbackContext context)
    {
        Vector2 direction = context.ReadValue<Vector2>(); 
        thridPersonCamera.m_XAxis.m_InputAxisValue = -direction.x;
        thridPersonCamera.m_YAxis.m_InputAxisValue = -direction.y;
    }

    #endregion
    
    // Movement is broken up into methods based on the characters current state
    #region Ground Movement

        private void GroundMovement()
        {
            Move();
            Jump();
        }
        
        private void Jump()
        {
            // If the player jumped this update // Dont need to check if the player is grounded because this can only be called if the player is grounded
            if (_jumpInput) 
                _rigidbody.AddForce(Vector3.up * PlayerJumpForce, ForceMode.VelocityChange);
            // _rigidbody.velocity += Vector3.up * PlayerJumpForce; // Should change this to AddForce()
            
            //if falling
        }

        private void Move()
        {
            //Calculate direction to move
            float targetAngle = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, 0.1f);

            //Move]
            camDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            
            if (CheckSlope())
                camDirection = Vector3.ProjectOnPlane(camDirection, groundHitInfo.normal).normalized;

            if (CheckClimbing())
            {
                camDirection = Vector3.Cross(climbHitInfo.normal, camDirection);
            }

            if(_playerFollower.isAiming)
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                //if input then turn and move
            if (_moveInput.magnitude > 0.1f)
            {
                //apply player rotation
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                
                //multiplied by PlayerDirection.magnitude (input vector) to give stick senstitivity
                if(_sprintInput)
                    _rigidbody.velocity = Vector3.SmoothDamp(_rigidbody.velocity, camDirection * (PlayerBaseSpeed * PlayerSprintMultiplier * _moveInput.magnitude), ref currVelocity, 0.2f);
                if(_crouchInput)
                    _rigidbody.velocity = Vector3.SmoothDamp(_rigidbody.velocity, camDirection * (PlayerBaseSpeed * PlayerCrouchMultiplier * _moveInput.magnitude), ref currVelocity, 0.2f);
                else
                    _rigidbody.velocity = Vector3.SmoothDamp(_rigidbody.velocity, camDirection * (PlayerBaseSpeed * _moveInput.magnitude), ref currVelocity, 0.2f);
            }
            //if no input 0 velocity (prevents sliding)
            else
            {
                _rigidbody.velocity = Vector3.SmoothDamp(_rigidbody.velocity, Vector3.zero, ref currVelocity, 0.05f);
            }
        }

        private void Climb()
        {
            // camDirection = Vector3.ProjectOnPlane(camDirection, climbHitInfo.normal).normalized;
            Debug.Log("climb");
            // _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, PlayerClimbSpeed, _rigidbody.velocity.y);
        }

    #endregion

    #region Air movement
    
        private void AirMovement()
        {
            AirControl();
            
            // if not grounded and can second jump on update // Dont need to check if the player is grounded because this can only be called while player is in the air
            if (_jumpInput && CanSecondJump) // removed && PlayerSecondJump
            {
                DoubbleJump();
            }
            if (_rigidbody.velocity.y <= 0 && !isGrounded) //falling
            {
                _rigidbody.velocity += Vector3.down * (PlayerFallForce * Time.deltaTime);
            }
        }

        private void DoubbleJump()
        {
            // Zero out y velocity before second jump
            _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, 0f, _rigidbody.velocity.z);

            if (directionalSecondJump)
            {
                Vector3 jumpDirection = new Vector3(playerForward.x, 1, playerForward.z);
                _rigidbody.AddForce(playerForward * (PlayerJumpForce * PlayerDoubleJumpMultiplier), ForceMode.VelocityChange);
                Debug.Log("Forard: "+playerForward);
            }
            else
                _rigidbody.AddForce(Vector3.up * (PlayerJumpForce * PlayerDoubleJumpMultiplier), ForceMode.VelocityChange);
            
            CanSecondJump = false;
        }

        private void AirControl()
        {
            // TODO: Move this code outside so it is not repeated in this method and the other move method
            
            // Calculate direction to move 
            float targetAngle = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, 0.1f);

            // Move
            Vector3 camDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            //if input then turn and move
            if (_moveInput.magnitude > 0.1f)
            {
                //apply camera rotation
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                
                Vector3 target = camDirection * (PlayerBaseSpeed * PlayerAirMoveMultiplier * _moveInput.magnitude); // why do we multiply by playerdirection.magnitude?
                target.y = _rigidbody.velocity.y;
                _rigidbody.velocity = Vector3.SmoothDamp(_rigidbody.velocity, target, ref currVelocity, 1f); 
                // _rigidbody.AddForce(camDirection * (10000 * PlayerDirection.magnitude * Time.deltaTime));
            }
        }
    
    #endregion

    #region Checks

    private void PreformChecks()
    {
        CheckGrounded();
        CheckSlope();
        CheckClimbing();
        CheckForward();
        CheckGravity();
        CheckSlowTime();
    }
    
    private void CheckGrounded()
    {
        // If detector detects ground
        if (Physics.CheckSphere(groundCheck.position, 0.1f, WhatIsGround))
        {
            // Debug.Log("walkable");
            wasGrounded = isGrounded;
            isGrounded = true;
            CanSecondJump = true;
        }
        else
            isGrounded = false;
    }
    
    private bool CheckSlope()
    {
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out groundHitInfo, Mathf.Infinity,
                WhatIsGround) && isGrounded)
        {
            curSlopeAngle = Vector3.Angle(groundHitInfo.normal, Vector3.up);
            return curSlopeAngle < MaxSlopeAngle && curSlopeAngle != 0;
        }

        return false;
    }

    private bool CheckClimbing()
    {
        if (Physics.CheckSphere(climbCheck.position, 0.2f, WhatIsClimb))
        {
            if (Physics.Raycast(climbCheck.position, transform.TransformDirection(Vector3.forward), out climbHitInfo))
            {
                if (Vector3.Angle(climbHitInfo.normal, Vector3.up) > MaxSlopeAngle)
                {
                    Debug.Log("climbing");
                    Debug.Log(climbHitInfo.normal);
                    return true;
                }
            }
        }
        return false;
    }
    
    private void CheckGravity()
    {
        _rigidbody.useGravity = !CheckSlope();
        _rigidbody.useGravity = !CheckClimbing();
    }

    private void CheckForward()
    {
        if (!isGrounded)
        {
            camDirection = transform.forward;
            return;
        }
        
        if (CheckSlope())
            camDirection = Vector3.ProjectOnPlane(camDirection, groundHitInfo.normal).normalized;

        if (CheckClimbing())
        {
            camDirection = Vector3.Cross(climbHitInfo.normal, camDirection);
        }
        

        camDirection = Vector3.Cross(groundHitInfo.normal, -transform.right);
    }

    private void CheckSlowTime()
    {
        if (_playerFollower.isAiming && !isGrounded)
        {
            Time.timeScale = 0.25f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
    

    #endregion

    private void InvokeEvents()
    {
        if(_jumpInput && isGrounded) 
            OnJumpEvent.Invoke();
        if(CanSecondJump && _jumpInput)
            OnJumpEvent.Invoke();
        if(_sprintInput && isGrounded) 
            OnSprintEvent.Invoke();
        if(_crouchInput && isGrounded)
            OnCrouchEvent.Invoke();
        if(isGrounded && !wasGrounded)
            OnLandEvent.Invoke();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(groundCheck.position, 0.1f);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.TransformDirection(Vector3.down) * 100);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, playerForward * 100);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, camDirection * 100);
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(climbCheck.position, transform.TransformDirection(Vector3.forward) * 0.25f);
    }
}
