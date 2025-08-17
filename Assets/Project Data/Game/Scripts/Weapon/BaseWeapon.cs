using UnityEngine;
using System;
using System.Collections;
using TriInspector;


namespace FXnRXn
{
	public abstract class BaseWeapon : MonoBehaviour, IWeapon
	{
		#region Animator Hash

		public static readonly int FIRE_HASH										= Animator.StringToHash(fireAnimationTrigger);
		public static readonly int Reload_HASH										= Animator.StringToHash(reloadAnimationTrigger);

		#endregion

		#region Properties
		
		[Title("Weapon Stats")]
		[Space(10)]
		[SerializeField] protected WeaponStats			stats;
		
		[Title("Weapon Configuration")]
		[Space(10)]
		[SerializeField] protected Transform			firePoint;
		[SerializeField] protected GameObject			muzzleFlashPrefab;
		[SerializeField] protected AudioSource			audioSource;
		[SerializeField] protected AudioClip			fireSound;
		[SerializeField] protected AudioClip			reloadSound;
		[SerializeField] protected AudioClip			emptySound;
        
		[Title("Animation")]
		[Space(10)]
		[SerializeField] protected Animator weaponAnimator;
		[SerializeField] protected static string fireAnimationTrigger = "Fire";
		[SerializeField] protected static string reloadAnimationTrigger = "Reload";
        
		protected float nextFireTime;
		protected bool isReloading;
		protected bool isAiming;
        
		public System.Action<int, int> OnAmmoChanged;
		public System.Action OnWeaponFired;
		#endregion
		
		#region Unity Callbacks

		private void Start()
		{
			if (GetComponent<AudioSource>() != null)
			{
				audioSource = GetComponent<AudioSource>();
			}
			else
			{
				audioSource = GetComponentInChildren<AudioSource>();
			}
		}

		#endregion
		
		#region Methods

		public void Fire()
		{
			if (!CanFire())
			{
				if (stats.currentAmmo <= 0 && !isReloading)
				{
					PlaySound(emptySound);
					StartCoroutine(AutoReload());
				}
				return;
			}

			nextFireTime = Time.time + (1f / stats.fireRate);
			stats.currentAmmo--;
			
			PerformFire();
			PlayFireEffects();
			OnAmmoChanged?.Invoke(stats.currentAmmo, stats.reserveAmmo);
			OnWeaponFired?.Invoke();
		}
		
		protected abstract void PerformFire();

		protected virtual void PlayFireEffects()
		{
			if(weaponAnimator != null) weaponAnimator.SetTrigger(FIRE_HASH);
			if (muzzleFlashPrefab != null && firePoint != null)
			{
				GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
				Destroy(flash, 0.1f);
			}
			PlaySound(fireSound);
		}

		public virtual void Reload()
		{
			if(isReloading || stats.currentAmmo == stats.magazineSize || stats.reserveAmmo <= 0) return;

			StartCoroutine(ReloadCoroutine());
		}

		protected virtual IEnumerator ReloadCoroutine()
		{
			isReloading = true;
			if(weaponAnimator != null) weaponAnimator.SetTrigger(Reload_HASH);
			
			PlaySound(reloadSound);

			yield return new WaitForSeconds(stats.reloadTime);
			
			int ammoNeeded = stats.magazineSize - stats.currentAmmo;
			int ammoToReload = Mathf.Min(ammoNeeded, stats.reserveAmmo);
            
			stats.currentAmmo += ammoToReload;
			stats.reserveAmmo -= ammoToReload;
            
			OnAmmoChanged?.Invoke(stats.currentAmmo, stats.reserveAmmo);
			isReloading = false;
		}

		public virtual void AimDownSights(bool isAiming)
		{
			this.isAiming = isAiming;
		}

		public virtual bool CanFire()
		{
			return Time.time >= nextFireTime && stats.currentAmmo > 0 && !isReloading;
		}

		public WeaponStats GetStats() => stats;
		
		
		
		
		
		
		
		protected IEnumerator AutoReload()
		{
			yield return new WaitForSeconds(0.5f);
			Reload();
		}
		protected void PlaySound(AudioClip clip)
		{
			if (audioSource != null && clip != null)
				audioSource.PlayOneShot(clip);
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion

		
	}
	
	
	public interface IWeapon
	{
		void Fire();
		void Reload();
		void AimDownSights(bool isAiming);
		bool CanFire();
		WeaponStats GetStats();
	}
	
}

