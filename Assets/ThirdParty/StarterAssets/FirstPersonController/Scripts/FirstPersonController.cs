using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

        [Header("Ladder")]
        [SerializeField] private float m_ladderClimbSpeed = 3.0f;
        private bool m_isClimbing = false;

        [Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

	
#if ENABLE_INPUT_SYSTEM
		private UnityEngine.InputSystem.PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;

		[Header("Camera references")]
		[SerializeField] private PlayerCamera m_playerCameraPrefab;
		private PlayerCamera m_playerCamera;
        [SerializeField] private CinemachineCamera m_playerFollowCameraPrefab;
        private CinemachineCamera m_playerFollowCamera;

		[Header("Player references")]
        private PlayerHealth m_health;
		private PlayerPickup m_playerPickup;
		private PlayerInteract m_playerInteract;

        [Header("Level references")]
        private LevelManager m_manager;

        [Header("Melee")]
        [SerializeField] private float m_weakForce = 5.0f;
        [SerializeField] private float m_strongForce = 15.0f;

        [Header("Dash")]
        [SerializeField] private float m_dashSpeed = 20f;
        [SerializeField] private float m_dashDuration = 0.2f;
        private bool m_isDashing;
        public bool IsDashing() { return m_isDashing; }
        private float m_dashTimer;
        private float m_dashSpeedMultiplier;
        private Vector3 m_dashDirection;

        [Header("Slide")]
        [SerializeField] private float m_slideSpeed = 10f;
        [SerializeField] private float m_slideDuration = 0.6f;
        private bool m_isSliding;
        public bool IsSliding() { return m_isSliding; }
        private float m_slideTimer;
        private Vector3 m_slideDirection;
        private bool m_slideQueued;
        private float m_slideQueueTime;
        [SerializeField] private float m_slideBufferTime = 0.2f;

        private const float _threshold = 0.01f;

        #region - GETTERS -
        public PlayerHealth GetHealth()
		{
			if(m_health == null)
			{
				Debug.LogError("Missing health reference to player.");
				return null;
			}
			return m_health;
		}

        public PlayerCamera GetPlayerCamera()
        {
            if (m_playerCamera == null)
            {
                Debug.LogError("Missing camera reference to player.");
                return null;
            }
            return m_playerCamera;
        }

        public PlayerPickup GetPlayerPickup()
        {
            if (m_playerPickup == null)
            {
                Debug.LogError("Missing reference to player pickup.");
                return null;
            }
            return m_playerPickup;
        }

        public LevelManager GetLevelManager()
		{
            if (m_manager == null)
            {
                Debug.LogError("Missing level manager reference to player.");
                return null;
            }
            return m_manager;
        }

		public float GetSpeed()
		{
			return _speed;
		}

		public Vector3 GetPlayerVelocity()
		{
			return _controller.velocity;
		}

		public UnityEngine.InputSystem.PlayerInput GetPlayerInput()
		{
            if (_playerInput == null)
            {
                Debug.LogError("Missing player input reference to player.");
                return null;
            }
            return _playerInput;
        }
        #endregion

        private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		public void Init(LevelManager manager)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;

            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            _playerInput.actions["Shoot"].performed += OnAttack;
            _playerInput.actions["Dash"].performed  += OnStartDash;
            _playerInput.actions["Slide"].performed += OnStartSlide;

            // get a reference to our main camera
            if (m_playerCamera == null)
			{
				m_playerCamera = Instantiate(m_playerCameraPrefab);
				m_playerCamera.Init(this, CinemachineCameraTarget.transform);
            }

            if (m_playerFollowCamera == null)
            {
                m_playerFollowCamera = Instantiate(m_playerFollowCameraPrefab);
                m_playerFollowCamera.Follow = CinemachineCameraTarget.transform;
            }

            m_health = gameObject.AddComponent<PlayerHealth>();
			m_health.Init();

			m_playerPickup = GetComponent<PlayerPickup>();
			m_playerPickup.Init(manager);

			m_playerInteract = GetComponent<PlayerInteract>();
			m_playerInteract.Init(m_playerCamera);

			m_manager = manager;

            DebugManager.Instance.RegisterFloat(
				new DebugFloat(
					"Player Speed",
					1f,
					100f,
					() => MoveSpeed,
					(v) => MoveSpeed = v
				),
                "Player"
            );

            DebugManager.Instance.RegisterFloat(
				new DebugFloat(
					"Acceleration",
					1f,
					100f,
					() => SpeedChangeRate,
					(v) => SpeedChangeRate = v
				), 
				"Player"
			);

            DebugManager.Instance.RegisterFloat(
				new DebugFloat(
					"Jump Height",
					1f,
					100f,
					() => JumpHeight,
					(v) => JumpHeight = v
				),
                "Player"
            );

            DebugManager.Instance.RegisterFloat(
				new DebugFloat(
					"Gravity",
					-100f,
					-1f,
					() => Gravity,
					(v) => Gravity = v
				),
                "Player"
            );
        }

		private void Start()
		{
			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

        private void OnDisable()
        {
            _playerInput.actions["Shoot"].performed -= OnAttack;
        }

        private void Update()
		{
			if(GameStateManager.Instance.GetFreezeGame())
			{
				return;
			}

			JumpAndGravity();
			GroundedCheck();
			Move();
			AttackUpdate();
            UpdateQueuedActions();
		}

		private void LateUpdate()
		{
            if (GameStateManager.Instance.GetFreezeGame())
            {
                return;
            }

            CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}

            if (m_isSliding)
            {
                Vector3 pos = m_playerFollowCamera.transform.localPosition;
                pos.y = -2.0f; // Desired slide height
                m_playerFollowCamera.transform.localPosition = pos;
            }
            else
            {
                Vector3 pos = m_playerFollowCamera.transform.localPosition;
                pos.y = 0.0f; // Normal height
                m_playerFollowCamera.transform.localPosition = pos;
            }
        }

        private void Move()
        {
            if (UpdateDash())
                return;

            if (UpdateSlide())
                return;

            // Climbing vertically
            if (m_isClimbing)
            {
                if (_input.move.y < -0.1f)
                {
                    StopClimbing();
                    return;
                }

                Vector3 climbMovement = Vector3.up * (_input.move.y * m_ladderClimbSpeed);

                _controller.Move(climbMovement * Time.deltaTime);

                return;
            }

            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            if (_input.move == Vector2.zero)
                targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed,
                    targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 inputDirection = new Vector3(_input.move.x, 0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                inputDirection = transform.right * _input.move.x +
                                 transform.forward * _input.move.y;
            }

            _controller.Move(
                inputDirection.normalized * (_speed * Time.deltaTime) +
                Vector3.up * (_verticalVelocity * Time.deltaTime));
        }

        #region - DASHING -
        private bool UpdateDash()
        {
            if (!m_isDashing)
                return false;

            m_dashTimer -= Time.deltaTime;

            if (m_dashTimer <= 0f)
            {
                m_isDashing = false;
                return false;
            }

            _controller.Move(m_dashDirection * (m_dashSpeed * m_dashSpeedMultiplier * Time.deltaTime) + Vector3.up * (_verticalVelocity * Time.deltaTime));

            return true;
        }

        public void OnStartDash(InputAction.CallbackContext context)
        {
            if (m_isSliding || m_isDashing)
                return;

            Vector3 inputDir = transform.right * _input.move.x +
                               transform.forward * _input.move.y;

            Vector3 velocity = _controller.velocity;
            velocity.y = 0f;

            Vector3 baseDir;

            // prefer current momentum if moving fast enough
            if (velocity.sqrMagnitude > 1f)
                baseDir = velocity.normalized;
            else if (inputDir.sqrMagnitude > 0.01f)
                baseDir = inputDir.normalized;
            else
                baseDir = transform.forward;

            m_dashDirection = baseDir;

            // burst feel tuning
            m_dashTimer = m_dashDuration;
            m_dashSpeedMultiplier = 1f;

            // optional: slightly different behavior air vs ground
            if (!_controller.isGrounded)
            {
                m_dashSpeedMultiplier = 0.8f; // weaker air dash for control
            }

            GetPlayerCamera().GetPlayerAnimator().SetTrigger("Dash");
            m_isDashing = true;
        }
        #endregion

        #region - SLIDING -
        private bool UpdateSlide()
        {
            if (!m_isSliding)
                return false;

            m_slideTimer -= Time.deltaTime;

            if (m_slideTimer <= 0f)
            {
                m_isSliding = false;
                return false;
            }

            _controller.Move(m_slideDirection * (m_slideSpeed * Time.deltaTime) + Vector3.up * (_verticalVelocity * Time.deltaTime));

            return true;
        }

        public void OnStartSlide(InputAction.CallbackContext context)
        {
            if (m_isSliding || m_isDashing)
                return;

            if (_controller.velocity.magnitude == 0)
                return;

            // If grounded, start immediately
            if (_controller.isGrounded)
            {
                StartSlide();
            }
            else
            {
                m_slideQueued = true;
                m_slideQueueTime = m_slideBufferTime;
            }
        }

        private void StartSlide()
        {
            Vector3 velocity = _controller.velocity;

            // remove vertical component
            velocity.y = 0f;

            // if barely moving, fallback to facing direction
            if (velocity.sqrMagnitude < 0.1f)
                velocity = transform.forward;

            m_slideDirection = velocity.normalized;

            m_slideTimer = m_slideDuration;
            m_isSliding = true;
            m_playerCamera.GetPlayerAnimator().SetTrigger("Slide");
        }
        #endregion

        private void UpdateQueuedActions()
        {
            if (m_slideQueued)
            {
                m_slideQueueTime -= Time.deltaTime;

                if (m_slideQueueTime <= 0f)
                {
                    m_slideQueued = false;
                    return;
                }

                if (_controller.isGrounded && !m_isDashing && !m_isSliding)
                {
                    m_slideQueued = false;
                    StartSlide();
                }
            }
        }

        public void SetVerticalVelocity(float val)
		{
			_verticalVelocity += val;
        }

        private void JumpAndGravity()
		{
			// No gravity when climbing
            if (m_isClimbing)
            {
                _verticalVelocity = 0f;
                return;
            }

            if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}

        #region - TRIGGER FUNCS -
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Ladder"))
            {
                StartClimbing();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Ladder"))
            {
                StopClimbing();
            }
        }
        #endregion

        #region - CLIMBING -
        private void StartClimbing()
        {
            m_isClimbing = true;
            _verticalVelocity = 0f;
        }

        private void StopClimbing()
        {
            m_isClimbing = false;
        }
		#endregion

		#region - ATTACKING -
		[Header("Attack")]
        [SerializeField] private float m_attackRange = 2.0f;
        [SerializeField] private LayerMask m_enemyLayer;

        [SerializeField] private float m_attackDuration = 0.6f;
        [SerializeField] private float m_comboWindow = 0.25f;

        private bool m_attacking;
        private bool m_comboQueued;

        private int m_attackIndex;

        private float m_attackTimer;
        private float m_lastAttackTime;

        public bool IsAttacking()
        {
            return m_attacking;
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            if (m_playerPickup == null)
            {
                Debug.LogError("Missing PlayerPickup.");
                return;
            }

            if (m_playerPickup.GetGunCount() != 0)
                return;

            // Already attacking, try to queue combo.
            if (m_attacking)
            {
                if (m_attackIndex == 0 &&
                    !m_comboQueued &&
                    Time.time - m_lastAttackTime <= m_comboWindow)
                {
                    m_comboQueued = true;
                }

                return;
            }

            // Start first attack.
            m_attacking = true;
            m_attackIndex = 0;
            m_attackTimer = m_attackDuration;
            m_lastAttackTime = Time.time;

            PlayAttack(0);
        }

        private void AttackUpdate()
        {
            if (!m_attacking)
                return;

            m_attackTimer -= Time.deltaTime;

            if (m_attackTimer > 0f)
                return;

            // Current attack finished
            if (m_comboQueued)
            {
                m_comboQueued = false;

                m_attackIndex = 1;
                m_attackTimer = m_attackDuration;
                m_lastAttackTime = Time.time;

                PlayAttack(1);
            }
            else
            {
                m_attacking = false;
                m_attackIndex = 0;
            }
        }

        public void PlayAttack(int index)
        {
            Animator anim = GetPlayerCamera().GetPlayerAnimator();

            anim.ResetTrigger("Attack01");
            anim.ResetTrigger("Attack02");

            switch (index)
            {
                case 0:
                    anim.SetTrigger("Attack01");
                    DoMeleeHit(m_weakForce); // Weaker attack
                    break;

                case 1:
                    anim.SetTrigger("Attack02");
                    DoMeleeHit(m_strongForce); // Stronger attack
                    break;
            }
        }

        private void DoMeleeHit(float force)
        {
            Camera cam = GetPlayerCamera().GetCamera();

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, m_attackRange, m_enemyLayer))
            {
                var enemy = hit.collider.GetComponent<Enemy>();
                if (enemy != null)
                {
                    Vector3 dir = transform.forward;
                    dir.y = 0f;
                    dir.Normalize();
                    enemy.ApplyKnockback(dir * force, 0.5f);

                    enemy.GetHealth().SetHealthRelative(-10);
                }
            }
        }
        #endregion
    }
}