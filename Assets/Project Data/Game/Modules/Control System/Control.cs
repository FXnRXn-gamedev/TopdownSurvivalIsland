using System;
using UnityEngine;
using Object = UnityEngine.Object;


namespace FXnRXn
{
	public static class Control
	{
		#region Variables

		public static InputType InputType { get; private set; } = InputType.UIJoystick;
		public static IControlBehavior				CurrentControl { get; private set; }
		public static GamepadData					GamepadData { get; private set; }
		
		
		
		public delegate void OnInputChangedCallback(InputType input);
		public static event OnInputChangedCallback OnInputChanged;
		
		#endregion
		
		#region Methods

		public static void Initialise(InputType _inputType, GamepadData _gamepadData)
		{
			InputType = _inputType;
			GamepadData = _gamepadData;

			if (GamepadData != null)
			{
				GamepadData.Initialise();
			}
		}

		public static void ChangeInputType(InputType _inputType)
		{
			InputType = _inputType;
			Object.Destroy(CurrentControl as MonoBehaviour);

			switch (_inputType)
			{
				case InputType.Keyboard:
					break;
				case InputType.Gamepad:
					break;
			}
			
			OnInputChanged?.Invoke(_inputType);
		}
		
		public static void SetControl(IControlBehavior _control)
		{
			CurrentControl = _control;
		}

		public static void EnableMovementControl()
		{
#if UNITY_EDITOR
			if(CurrentControl == null)
			{
				Debug.LogError("[Control]: Control behavior isn't set!");

				return;
			}
#endif

			CurrentControl.EnableMovementControl();
		}
		public static void DisableMovementControl()
		{
#if UNITY_EDITOR
			if (CurrentControl == null)
			{
				Debug.LogError("[Control]: Control behavior isn't set!");

				return;
			}
#endif

			CurrentControl.DisableMovementControl();
		}
		
		#endregion
		

	}
}
