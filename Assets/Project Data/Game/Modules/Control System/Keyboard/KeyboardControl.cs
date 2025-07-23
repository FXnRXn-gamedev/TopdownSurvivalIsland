#pragma warning disable 0067
#pragma warning disable 0414

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FXnRXn
{
	public class KeyboardControl : MonoBehaviour
	{
		public static KeyboardControl Instance { get; private set; }
		
		#region Properties
		[Header("Refference :")]
		[SerializeField] private InputActionAsset inputActionAsset;
		
		public Vector3		MovementInput { get; private set; }
		public bool			IsMovementInputNonZero { get; private set; }

		private bool		IsMovementControlActive;
		public event SimpleCallback OnMovementInputActivated;
		
		private InputAction moveAction;
		private InputAction lookAction;
		private InputAction jumpAction;
		private InputAction runtAction;
		private InputAction shootAction;
		private InputAction reloadAction;
		private InputAction aimAction;


		public Action onJump;
		public Action onShoot;
		public Action onReload;
		public Action onAimActivated;
		public Action onAimDeactivated;
		

		#endregion
		
		#region Unity Callbacks
		private void Awake()
		{
			Instance = this;
			if (InputHandler.InputType == InputType.Keyboard)
			{
				enabled = true;
			}
			else
			{
				enabled = false;
			}

		}
		
		private void Start()
		{
			moveAction = InputSystem.actions.FindAction("Move");
			moveAction.performed += OnMove;
			moveAction.canceled += OnMove;
			
			// lookAction = InputSystem.actions.FindAction("Look");
			// lookAction.performed += OnLook;
			// lookAction.canceled += OnLook;
			//
			// jumpAction = InputSystem.actions.FindAction("Jump");
			// jumpAction.performed += OnJump;
			// jumpAction.canceled += OnJump;
			//
			// runtAction = InputSystem.actions.FindAction("Sprint");
			// runtAction.performed += OnRun;
			// runtAction.canceled += OnRun;
			//
			// shootAction = InputSystem.actions.FindAction("Shoot");
			// shootAction.performed += OnShoot;
			// shootAction.canceled += OnShoot;
			//
			// aimAction = InputSystem.actions.FindAction("Aim");
			// aimAction.performed += OnAim;
			// aimAction.canceled += OnAim;
			//
			// reloadAction = InputSystem.actions.FindAction("Reload");
			// reloadAction.performed += OnReload;
			// reloadAction.canceled += OnReload;
		}
		
		private void OnEnable()
		{
			inputActionAsset.FindActionMap("Player").Enable();
		}

		private void OnDisable()
		{
			inputActionAsset.FindActionMap("Player").Disable();
		}
		#endregion
		
		#region Methods
		
#if ENABLE_INPUT_SYSTEM

		public void OnMove(InputAction.CallbackContext ctx)
		{
			Vector3 move = new Vector3(ctx.ReadValue<Vector2>().x, 0, ctx.ReadValue<Vector2>().y);
			MovementInput = Vector3.ClampMagnitude(move, 1);
		}

		// public void OnLook(InputAction.CallbackContext ctx)
		// {
		// 	LookInput(ctx.ReadValue<Vector2>());
		//
		// }
		//
		// public void OnJump(InputAction.CallbackContext ctx)
		// {
		// 	if (ctx.control.IsPressed())
		// 	{
		// 		onJump?.Invoke();
		// 	}
		// }
		//
		// public void OnRun(InputAction.CallbackContext ctx)
		// {
		// 	run = ctx.control.IsPressed();
		// }
		//
		// public void OnShoot(InputAction.CallbackContext ctx)
		// {
		// 	
		// 	if (ctx.control.IsPressed())
		// 	{
		// 		onShoot?.Invoke();
		// 	}
		// 	
		// }
		//
		// public void OnReload(InputAction.CallbackContext ctx)
		// {
		// 	if (ctx.control.IsPressed())
		// 	{
		// 		onReload?.Invoke();
		// 	}
		// }
		//
		// public void OnAim(InputAction.CallbackContext ctx)
		// {
		// 	if (ctx.performed)
		// 	{
		// 		onAimActivated?.Invoke();
		// 	}
		//
		// 	if (ctx.canceled)
		// 	{
		// 		onAimDeactivated?.Invoke();
		// 	}
		// }
		
#endif

		private void Update()
		{
			if(!IsMovementInputNonZero && MovementInput.magnitude > 0.1f)
			{
				IsMovementInputNonZero = true;

				OnMovementInputActivated?.Invoke();
			}

			IsMovementInputNonZero = MovementInput.magnitude > 0.1f;
		}

		#endregion

	}

}