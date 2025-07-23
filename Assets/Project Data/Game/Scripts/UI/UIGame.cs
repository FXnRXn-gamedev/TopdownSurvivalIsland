using System;
using UnityEngine;

namespace FXnRXn
{
	public class UIGame : MonoBehaviour
	{
		#region Properties
		
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
		}

		#endregion
		
		
		
		#region Callbacks
		
		#endregion
		#region Unity Callbacks
		
		#endregion
	}
}
