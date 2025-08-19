using UnityEngine;

namespace FXnRXn
{
	public class PooledObjectInstance : MonoBehaviour
	{
    		
		#region Properties
		
		public string poolTag;
		private float returnDelay = -1f;
        
		
		#endregion
		
		#region Methods

		public void ReturnToPoolAfterDelay(float delay)
		{
			returnDelay = delay;
			StartCoroutine(ReturnAfterDelay());
		}
        
		private System.Collections.IEnumerator ReturnAfterDelay()
		{
			yield return new WaitForSeconds(returnDelay);
			ObjectPoolManager.Instance.ReturnToPool(gameObject);
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion
	}
}

