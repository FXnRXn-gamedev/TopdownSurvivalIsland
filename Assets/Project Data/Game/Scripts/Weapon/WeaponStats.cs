using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapon/WeaponStats", fileName = "WeaponStats")]
public class WeaponStats : ScriptableObject
{
	public string	weaponName;
	public float	damage;
	public float	range;
	public float	fireRate;
	public int		magazineSize;
	
	public int		reserveAmmo;
	public float	reloadTime;
	public bool		isHematic;
	public bool		isAntiDT;
}
