#pragma warning disable 0067
#pragma warning disable 0414


using System;
using UnityEngine;


namespace FXnRXn
{
	[RequireComponent(typeof(CharacterController))]
	public class PlayerController : MonoBehaviour
	{
		public static PlayerController Instance { get; private set; }

		#region Animator Hash

		public static readonly int RUN_HASH										= Animator.StringToHash("Run");
		public static readonly int MOVEMENT_MULTIPLIER_HASH						= Animator.StringToHash("Movement Multiplier");

		#endregion
		
		#region Properties
		[Header("--- Player Settings ---")]
		[SerializeField] private bool							isGrounded;
		[SerializeField] private float							playerMovementSpeed = 4f;
		[SerializeField] private float							playerAcceleration = 7f;
		[SerializeField] private float							gravity = -9.81f;
		
		[Header("--- Ground Detection ---")]
		[SerializeField] private LayerMask						groundLayerMask = 1; // Ground layer
		[SerializeField] private float							groundCheckDistance = 0.2f;
		[SerializeField] private Transform						groundCheckPoint;

		
		
		
		private bool isRunning;
		public static bool IsRunning => Instance.isRunning;
		
		private float speed = 0;
		private bool IsMovementEnabled { get; set; }
		
		private Animator playerAnimator;
		private CharacterController playerController;
		
		private Vector3 playerVelocity;
		private float maxSpeed;
		private float acceleration;

		#endregion
		
		#region Unity Callbacks

		private void Awake()
		{
			if (Instance == null) Instance = this;
			if(playerController == null) playerController = GetComponent<CharacterController>();
			if(playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
		}

		private void Start()
		{
			Initialise();
			
		}
		
		public void Initialise()
		{
			RecalculateSpeed();
			IsMovementEnabled = true;
		}

		private void Update()
		{
			MovementUpdate();
			HandleGravity();
		}

		#endregion
		
		#region Methods
		
			#region Movement

			private void MovementUpdate()
			{
				Vector3 horizontalMovement = Vector3.zero;
	
				if (IsMovementEnabled && InputHandler.Instance.IsMovementInputNonZero && InputHandler.Instance.MovementInput.magnitude > 0.025f)
				{
					
					if (!isRunning)
					{
						isRunning = true;
						playerAnimator.SetBool(RUN_HASH, true);
						speed = 0;
					}

					float maxAllowedSpeed = InputHandler.Instance.MovementInput.magnitude * maxSpeed;
					
					if (speed > maxAllowedSpeed)
					{
						speed -= acceleration * Time.deltaTime;
						if (speed < maxAllowedSpeed)
						{
							speed = maxAllowedSpeed;
						}
					}
					else
					{
						speed += acceleration * Time.deltaTime;
						if (speed > maxAllowedSpeed)
						{
							speed = maxAllowedSpeed;
						}
					}
					
					float multiplier = speed / maxSpeed;
					playerAnimator.SetFloat(MOVEMENT_MULTIPLIER_HASH, multiplier);
					
					horizontalMovement = InputHandler.Instance.MovementInput * speed * Time.deltaTime;
					transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(InputHandler.Instance.MovementInput.normalized), 0.2f);
				}
				else
				{
					var multiplier = playerAnimator.GetFloat(MOVEMENT_MULTIPLIER_HASH) * 0.8f;
					playerAnimator.SetFloat(MOVEMENT_MULTIPLIER_HASH, multiplier);
					
					if (isRunning)
					{
						isRunning = false;
						playerAnimator.SetBool(RUN_HASH, false);
					}
				}
				
				Vector3 totalMovement = horizontalMovement + (playerVelocity * Time.deltaTime);
				playerController.Move(totalMovement);
				

			}
		
			private void RecalculateSpeed()
			{
				maxSpeed = playerMovementSpeed;
				acceleration = playerAcceleration;
			}

			#endregion
			
			#region Gravity
			
			private void HandleGravity()
			{
				isGrounded = IsGroundedCustom();
				if (isGrounded && playerVelocity.y < 0f)
				{
					playerVelocity.y = -2f;
				}

				playerVelocity.y += gravity * Time.deltaTime;
			}

			private bool IsGroundedCustom()
			{
				// Raycast downward from the character's position
				return Physics.Raycast(groundCheckPoint.position, Vector3.down, groundCheckDistance, groundLayerMask);
			}


			#endregion
		
		
		#endregion
		
		
		//--------------------------------------------------------------------------------------------------------------
		
		private void OnDrawGizmosSelected()
		{
			if (groundCheckPoint != null)
			{
				Gizmos.color = isGrounded ? Color.green : Color.red;
				Gizmos.DrawRay(groundCheckPoint.position, Vector3.down * groundCheckDistance);
			}
		}


	}
}
