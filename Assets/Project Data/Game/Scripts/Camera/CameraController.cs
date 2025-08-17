using System;
using UnityEngine;

namespace FXnRXn
{
	public class CameraController : MonoBehaviour
	{
		#region Properties
		public static CameraController Instance { get; private set; }
		
		
		#endregion
		
		#region Unity Callbacks

		private void Awake()
		{
			if(Instance == null) Instance = this;
		}

		#endregion
		
		#region Methods

		

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------
		public Camera GetMainCamera => GetComponent<Camera>();
		public Transform GetMainCameraTransform => GetComponent<Camera>().transform;
		public Vector3 GetMainCameraForward => GetMainCameraTransform.forward;
		public Vector3 GetMainCameraRight => GetMainCameraTransform.right;
		public Vector3 GetMainCameraForwardNormalize => GetMainCameraTransform.forward.normalized;
		public Vector3 GetMainCameraRightNormalize => GetMainCameraTransform.right.normalized;

		#region Helper


		#endregion
	}
}

