using System.Collections;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

namespace FXnRXn
{
	public class AntiBTHandgun : BaseWeapon
	{
    		
		#region Properties
		[Title("Advanced Features")]
		[Space(10)]
		[SerializeField] private WeaponAttachment[]		attachments;
		[SerializeField] private bool					useHapticFeedback = true;
		[SerializeField] private bool					useAdaptiveTriggers = true;
		
		[Title("Weapon Condition")]
		[Space(10)]
		[SerializeField] private float					maxDurability = 100f;
		[SerializeField] private float					currentDurability = 100f;
		[SerializeField] private float					durabilityLossPerShot = 0.1f;
		[SerializeField] private AnimationCurve			damageByDurability;
		
		
		[Title("Ammunition Types")]
		[Space(10)]
		[SerializeField] private List<AmmoType> availableAmmoTypes;
		[SerializeField] private int currentAmmoTypeIndex = 0;
		
		
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

		protected override void Start()
		{
			base.Start();
		}

		protected override void Update()
		{
			base.Update();
		}

		#endregion
		
		#region Methods
		
		public override void Fire()
		{
			
			// Apply durability loss
			
			// Generate heat
			
			// Apply haptic feedback
			
			base.Fire();
		}

		public override void Reload()
		{
			base.Reload();
		}



		protected override void PerformFire()
		{
			
			AmmoType currentAmmo = availableAmmoTypes[currentAmmoTypeIndex];
			string projectileTag = currentAmmo.projectilePoolTag;
			
			// Calculate accuracy with all modifiers
			float accuracy = CalculateAccuracy();
			
			// Spawn projectile from pool
			Vector3 spread = Random.insideUnitSphere * (1f - accuracy) * 0.1f;
			Quaternion rotation = firePoint.rotation * Quaternion.Euler(spread * 100f);

			
			GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(
				projectileTag, 
				firePoint.position, 
				rotation
			);
			
			if (projectile != null)
			{
				AdvancedProjectile proj = projectile.GetComponent<AdvancedProjectile>();
				if (proj != null)
				{
					float damageModifier = damageByDurability.Evaluate(currentDurability / maxDurability);
					proj.Initialize(
						currentAmmo.projectileSpeed, 
						stats.damage * currentAmmo.damageMultiplier * damageModifier, 
						stats.range
					);
				}
			}
			
			if (currentBloodLevel < bloodConsumptionPerShot)
			{
				// Not enough blood
				if(emptySound != null) PlaySound(emptySound);
				return;
			}
            
			currentBloodLevel -= bloodConsumptionPerShot;
			OnBloodLevelChanged?.Invoke(currentBloodLevel, maxBloodLevel);
			
			
			
			
			
            
			//GameObject bullet = Instantiate(hematicBulletPrefab, firePoint.position, firePoint.rotation);
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

		private float CalculateAccuracy()
		{
			float accuracy = 1f;

			return Mathf.Clamp01(accuracy);

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

