using System.Collections;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

namespace FXnRXn
{
	public class AntiBTHandgun : BaseWeapon
	{
    		
		#region Properties
		[Title("Anti-BT Settings")]
		[Space(10)]
		[SerializeField] private GameObject				hematicBulletPrefab;
		[SerializeField] private float					bulletSpeed = 40f;
		[SerializeField] private float					btDamageMultiplier = 2f;
		[SerializeField] private float					bloodConsumptionPerShot = 10f;
        
		[Title("Blood System")]
		[Space(10)]
		[SerializeField] private float					maxBloodLevel = 1000f;
		[SerializeField] private float					currentBloodLevel = 1000f;
		[SerializeField] private float					bloodRegenerationRate = 1f;
        
		[Title("Special Effects")]
		[Space(10)]
		[SerializeField] private GameObject				bloodMuzzleFlashPrefab;
		[SerializeField] private Material				btRevealMaterial;
		[SerializeField] private float					btRevealRadius = 20f;
		[SerializeField] private float					btRevealDuration = 3f;
        
		public System.Action<float, float> OnBloodLevelChanged;
		
		#endregion
		
		#region Unity Callbacks
		
		
		#endregion
		
		#region Methods

		protected override void PerformFire()
		{
			if (currentBloodLevel < bloodConsumptionPerShot)
			{
				// Not enough blood
				PlaySound(emptySound);
				return;
			}
            
			currentBloodLevel -= bloodConsumptionPerShot;
			OnBloodLevelChanged?.Invoke(currentBloodLevel, maxBloodLevel);
            
			GameObject bullet = Instantiate(hematicBulletPrefab, firePoint.position, firePoint.rotation);
			//HematicBullet bulletScript = bullet.GetComponent<HematicBullet>();
            
			// if (bulletScript != null)
			// {
			// 	bulletScript.Initialize(bulletSpeed, stats.damage * btDamageMultiplier, stats.range);
			// }
            
			// Reveal nearby BTs
			RevealNearbyBTs();
		}

		protected override void PlayFireEffects()
		{
			base.PlayFireEffects();
			// Special blood muzzle flash
			if (bloodMuzzleFlashPrefab != null && firePoint != null)
			{
				GameObject bloodFlash = Instantiate(bloodMuzzleFlashPrefab, firePoint.position, firePoint.rotation);
				Destroy(bloodFlash, 0.2f);
			}
		}
		
		
		
		private void RevealNearbyBTs()
		{
			StartCoroutine(BTRevealEffect());
		}
        
		private IEnumerator BTRevealEffect()
		{
			// Find all BTs in range
			Collider[] colliders = Physics.OverlapSphere(transform.position, btRevealRadius);
			// List<IBT> revealedBTs = new List<IBT>();
   //          
			// foreach (Collider col in colliders)
			// {
			// 	IBT bt = col.GetComponent<IBT>();
			// 	if (bt != null)
			// 	{
			// 		bt.Reveal(btRevealDuration);
			// 		revealedBTs.Add(bt);
			// 	}
			// }
            
			// Create reveal pulse effect
			if (btRevealMaterial != null)
			{
				GameObject revealSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				revealSphere.transform.position = transform.position;
				revealSphere.transform.localScale = Vector3.one * 0.1f;
                
				Renderer renderer = revealSphere.GetComponent<Renderer>();
				renderer.material = btRevealMaterial;
                
				Collider sphereCollider = revealSphere.GetComponent<Collider>();
				Destroy(sphereCollider);
                
				float elapsed = 0f;
				while (elapsed < 0.5f)
				{
					elapsed += Time.deltaTime;
					float t = elapsed / 0.5f;
                    
					revealSphere.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, btRevealRadius * 2f, t);
                    
					Color color = renderer.material.color;
					color.a = Mathf.Lerp(0.5f, 0f, t);
					renderer.material.color = color;
                    
					yield return null;
				}
                
				Destroy(revealSphere);
			}
		}
		
		
		
		


		private IEnumerator BloodRegeneration()
		{
			while (true)
			{
				if (currentBloodLevel < maxBloodLevel)
				{
					currentBloodLevel = Mathf.Min(currentBloodLevel + bloodRegenerationRate * Time.deltaTime, maxBloodLevel);
					OnBloodLevelChanged?.Invoke(currentBloodLevel, maxBloodLevel);
				}
				yield return null;
			}
		}

		public override bool CanFire()
		{
			return base.CanFire() && currentBloodLevel >= bloodConsumptionPerShot;
		}


		public void RefillBlood(float amount)
		{
			currentBloodLevel = Mathf.Min(currentBloodLevel + amount, maxBloodLevel);
			OnBloodLevelChanged?.Invoke(currentBloodLevel, maxBloodLevel);
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion

		
	}
}

