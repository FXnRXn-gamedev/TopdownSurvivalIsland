using System;
using UnityEngine;
using UnityEngine.UI;

namespace FXnRXn
{
	public class UIGame : MonoBehaviour
	{
		#region Properties
		
		[Header("--- Buttons ---")]
		[SerializeField] private Button							interactButton;
		
		[Header("--- Components ---")]
		[SerializeField] private Joystick joystick;
		public Joystick Joystick => joystick;
		
		protected Canvas canvas;
		public Canvas Canvas => canvas;
		#endregion
		
		
		#region Methods

		private void Awake()
		{
			canvas = GetComponent<Canvas>();
			if(FindFirstObjectByType<Joystick>() != null) joystick = FindFirstObjectByType<Joystick>();
			
		}

		private void Start()
		{
			if(joystick != null) joystick.Initialise(canvas);
			
			if (interactButton != null)
			{
				interactButton.onClick.RemoveAllListeners();
				interactButton.onClick.AddListener(() =>
				{
					InputHandler.Instance.onInteract?.Invoke();
				});
			}
			
		}

		#endregion
		
		
		
		#region Callbacks
		
		#endregion
		#region Unity Callbacks
		
		#endregion
	}
}
