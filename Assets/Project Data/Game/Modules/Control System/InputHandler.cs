using System;
using UnityEngine;

namespace FXnRXn
{
	public class InputHandler : MonoBehaviour
	{
		public static InputHandler					Instance { get; private set; }
		public static InputType						InputType { get; set; } = InputType.UIJoystick;

		
		public Vector3 MovementInput { get; private set; }
		public bool IsMovementInputNonZero { get; private set;}
		
		
		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
				DontDestroyOnLoad(gameObject);
			}
			else
			{
				Destroy(gameObject);
			}

			
		}

		private void Update()
		{
			switch (InputType)
			{
				case InputType.UIJoystick:
					IsMovementInputNonZero = Joystick.Instance.IsMovementInputNonZero;
					MovementInput = Joystick.Instance.MovementInput;
					break;
				case InputType.Keyboard:
					IsMovementInputNonZero = KeyboardControl.Instance.IsMovementInputNonZero;
					MovementInput = KeyboardControl.Instance.MovementInput;
					break;
				
			}
		}


	}
}
