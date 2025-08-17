using System;
using TriInspector;
using UnityEngine;

namespace FXnRXn
{
	public class ScanBehaviour : MonoBehaviour
	{
		#region Properties
		
		
		#endregion
		
		#region Unity Callbacks

		private void Start()
		{
			InputHandler.Instance.onScan += ActiveScan;
		}

		private void OnDisable()
		{
			InputHandler.Instance.onScan -= ActiveScan;
		}

		#endregion
		
		#region Methods
		
		[Button]
		public void ActiveScan()
		{
			if (Camera.main != null)
			{
				ScanFeatureURP.ExecuteScan(transform);
			}
			
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion
	}
}

