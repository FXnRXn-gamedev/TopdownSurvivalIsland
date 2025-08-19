using System;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

namespace FXnRXn
{
	public class PorterWeaponManager : MonoBehaviour
	{
		#region Singleton
		public static PorterWeaponManager Instance { get; private set; }

		private void Awake()
		{
			if(Instance == null) Instance = this;
		}

		#endregion
		
		
		#region Properties
		[Title("Weapon Slots")]
		[Space(10)]
		[SerializeField] private Transform							weaponHolder;
		[SerializeField] private List<BaseWeapon>					availableWeapons = new List<BaseWeapon>();
		[SerializeField] private int								maxWeaponSlots = 4;
		
		
		
		private BaseWeapon currentWeapon;
		private int currentWeaponIndex = 0;
		private bool isAiming = false;

		public Action onWeaponFirePressed;
		
		#endregion
		
		#region Unity Callbacks

		private void Start()
		{
			currentWeapon = availableWeapons[currentWeaponIndex];

			onWeaponFirePressed += WeaponFirePressed;
			
			//availableWeapons[0].Fire();
		}

		private void OnDisable()
		{
			onWeaponFirePressed -= WeaponFirePressed;
		}

		#endregion
		
		#region Methods
		
		// Firing
		private void WeaponFirePressed()
		{
			if (currentWeapon != null)
			{
				currentWeapon.ReadyToFire();
			}
		
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion
	}
}

