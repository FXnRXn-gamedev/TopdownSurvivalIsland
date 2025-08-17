#pragma warning disable 0067
#pragma warning disable 0414

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using TriInspector;

namespace FXnRXn
{ 
	public class PorterSystem : MonoBehaviour
	{
		#region Properties

		[Title("Porter Settings")]
		[Space(10)]
		[SerializeField] private Transform						cargoAnchor;
		[SerializeField] private float							maxCarryWeight = 60f;
		[SerializeField] private float							balanceRecoveryRate = 1.5f;
		[Range(0, 1)][SerializeField] private float				balanceDepletionRate = 0.25f;
		

		[Title("Stacking Settings")]
		[Space(10)]
		[SerializeField] private float							stackSpacing = 0.1f;
		[SerializeField] private float							maxStackHeight = 15f;
		[SerializeField] private bool							autoRotateItems = true;
		
		[Title("Balance System")]
		[Space(10)]
		[SerializeField] private BalanceState					balanceState;
		[SerializeField] private float							maxBalance = 100f;
		[SerializeField] private float							currentBalance;
		[SerializeField] private float							balanceDangerThreshold = 30f;
		[SerializeField] private float							balanceFallThreshold = 10f;
		[SerializeField] private float							balanceSwayIntensity = 0.8f;
		[SerializeField] private float							balanceStabilizationSpeed = 2f;

		[Title("Physics Settings")]
		[Space(10)]
		[SerializeField] private float							weightSpeedModifier = 0.005f;
		[SerializeField] private float							terrainSlopeModifier = 0.5f;
		[SerializeField] private float							waterDragModifier = 0.7f;
		[SerializeField] private float							staminaDrainPerKg = 0.1f;
		
		[Title("Balance Recovery")]
		[Space(10)]
		[SerializeField] private float							stationaryRecoveryBonus = 2f;
		[SerializeField] private float							stationaryRecoveryDelay = 0.5f; // Time before stationary recovery kicks in
		[SerializeField] private float							crouchRecoveryBonus = 1.5f;


		[Title("Events")]
		[Space(10)]
		public UnityEvent<float>								OnBalanceChanged;
		public UnityEvent<float>								OnWeightChanged;
		public UnityEvent<bool>									OnCriticalBalance;
		public UnityEvent										OnCargoDamaged;
		public UnityEvent										OnCargoDropped;

		

		private Vector3 cargoAnchorBaseTransform;
		private PlayerController playerController;
		
		
		private List<CargoItem> carriedCargo = new List<CargoItem>();
		private CharacterController characterController;
		private float currentCarryWeight;
		private Vector3 balanceSwayDirection;
		private float swayTimer;
		private float stationaryTimer = 0f;
		private bool isStationary = false;

		
		// Stacking Variable
		private float currentStackHeight = 0f;
		

		#endregion

		

		#region Unity Callbacks

		private void Awake()
		{
			playerController = GetComponent<PlayerController>();
			if(characterController == null) characterController = GetComponent<CharacterController>();

			currentBalance = maxBalance;
			cargoAnchorBaseTransform = cargoAnchor.localPosition;
			
		}

		private void Update()
		{
			HandleBalance();
			if (PlayerIsMoving())
			{
				ApplyCargoSway();
			}
		}

		#endregion
		
		
		#region Methods
		
			// ---------------------------------------------------------------------------------------------------------
			//-->                                 STACKING SYSTEM
			// ---------------------------------------------------------------------------------------------------------

			#region Stacking System

			/// <summary>
			/// Gets the correct height of a cargo item for stacking purposes, using its local collider/mesh bounds.
			/// This avoids issues with world-space AABB of rotated objects.
			/// </summary>
			private float GetItemStackHeight(Transform item)
			{
				// Try to get height from specific collider types first, as it's most accurate.
				if (item.TryGetComponent<BoxCollider>(out var boxCollider))
				{
					return boxCollider.size.y * item.localScale.y;
				}
				if (item.TryGetComponent<CapsuleCollider>(out var capsuleCollider))
				{
					return capsuleCollider.height * item.localScale.y;
				}
				if (item.TryGetComponent<SphereCollider>(out var sphereCollider))
				{
					// For a sphere, height is diameter.
					return sphereCollider.radius * 2 * item.localScale.y;
				}

				// As a fallback, use the mesh bounds. This is the AABB of the mesh data itself.
				if (item.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
				{
					return meshFilter.sharedMesh.bounds.size.y * item.localScale.y;
				}

				// If all else fails, use the world bounds from a general collider.
				// This might be inaccurate if the object is rotated, but it's a safe fallback.
				if (item.TryGetComponent<Collider>(out var genericCollider))
				{
					Debug.LogWarning($"Could not determine accurate stack height for {item.name} from specific colliders or mesh. Falling back to world bounds, which may be inaccurate if object is rotated.", item);
					return genericCollider.bounds.size.y;
				}

				// Final fallback for items with no colliders at all.
				Debug.LogError($"Cannot determine stack height for {item.name} as it has no Collider.", item);
				return 1f; // Default height
			}
			
			#endregion
			
			// ---------------------------------------------------------------------------------------------------------
			//-->                                 CARGO MANAGEMENT SYSTEM
			// ---------------------------------------------------------------------------------------------------------

			#region Cargo Management
			
				public bool CanPickupItem(CargoItem item)
				{
					return currentCarryWeight + item.weight <= maxCarryWeight;
				}

				public void PickupCargoItem(CargoItem item)
				{
					if (!CanPickupItem(item)) return;
					
					// The position is calculated synchronously here to prevent race conditions.
					// We determine the position BEFORE starting the visual lerp.
					float itemHeight = GetItemStackHeight(item.physicalItem);
					
					// The new item's position is the current top of the stack.
					// We use local position relative to the cargo anchor.
					item.localMountPosition = new Vector3(0, currentStackHeight, 0);

					// IMPORTANT: Update the stack height immediately for the next item.
					currentStackHeight += itemHeight + stackSpacing;

					carriedCargo.Add(item);
					currentCarryWeight += item.weight;
		        
					MountCargoItem(item);
					UpdateBalanceImpact();
					
					OnWeightChanged?.Invoke(GetWeightCarryRatio());
				}

				private void MountCargoItem(CargoItem item) 
				{
					item.isMounted = true;
					// The target position is now pre-calculated in PickupCargoItem.
					// This method now just handles parenting and the visual movement.

					Rigidbody rb = item.physicalItem.GetComponent<Rigidbody>();
					if (rb != null)
					{
						rb.isKinematic = true;
						rb.interpolation = RigidbodyInterpolation.None;
					}
					item.physicalItem.SetParent(cargoAnchor);
					StartCoroutine(LerpToMountPosition(item));
				}

				private IEnumerator LerpToMountPosition(CargoItem item)
				{
					float duration = 0.5f;
					float elapsed = 0f;
					Vector3 startPosition = item.physicalItem.position;
					Quaternion startRotation = item.physicalItem.rotation;
					
					float arcHeight = 2f;
					// The target position is calculated from the pre-assigned local position.
					Vector3 targetPosition = cargoAnchor.TransformPoint(item.localMountPosition);
					Quaternion targetRotation = autoRotateItems ? cargoAnchor.rotation : item.physicalItem.rotation;
					
					while (elapsed < duration)
					{
						float progress = elapsed / duration;
						float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
						
						Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
						
						// Add a small arc to the movement to make it look more natural.
						float arcOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
						currentPos.y += arcOffset;
						
						item.physicalItem.position = currentPos;
						item.physicalItem.rotation = Quaternion.Lerp(startRotation, targetRotation, smoothProgress);
						elapsed += Time.deltaTime;
						yield return null;
					}

					// Snap to final local position and rotation.
					item.physicalItem.localPosition = item.localMountPosition;
					item.physicalItem.localRotation = autoRotateItems ? Quaternion.identity : Quaternion.Inverse(cargoAnchor.rotation) * startRotation;
					
					// The stack height is now updated instantly in PickupCargoItem, so the call here is removed.

					// Disable cargo physics if it has FragileCargo component
					FragileCargo fragileCargo = item.physicalItem.GetComponent<FragileCargo>();
					if (fragileCargo != null)
					{
						fragileCargo.DisableCargo();
					}
				}

				public void RemoveCargoItem(CargoItem item)
				{
					if (carriedCargo.Contains(item))
					{
						carriedCargo.Remove(item);
						currentCarryWeight -= item.weight;
					
						// Re-enable physics
						Rigidbody rb = item.physicalItem.GetComponent<Rigidbody>();
						if (rb != null)
						{
							rb.isKinematic = false;
							rb.interpolation = RigidbodyInterpolation.Interpolate;
						}
					
						item.physicalItem.SetParent(null);
						item.isMounted = false;
						
						// Enable cargo physics if it has FragileCargo component
						FragileCargo fragileCargo = item.physicalItem.GetComponent<FragileCargo>();
						if (fragileCargo != null)
						{
							fragileCargo.ResetFragileCargo();
						}
					
						// Reorganize remaining cargo to fill the gap.
						StartCoroutine(ReorganizeStack());
					}
				}
				
				private IEnumerator ReorganizeStack()
				{
					yield return new WaitForSeconds(0.1f); // Small delay to let physics settle
				
					// Sort remaining items to maintain a consistent order (e.g., heaviest at the bottom).
					carriedCargo.Sort((a, b) => b.weight.CompareTo(a.weight));
					
					// Reset stack height and rebuild the stack from the bottom up.
					currentStackHeight = 0f; 
					
					foreach (var item in carriedCargo)
					{
						if (item.isMounted)
						{
							float itemHeight = GetItemStackHeight(item.physicalItem);

							// Recalculate the local position based on the new, compacted stack height.
							item.localMountPosition = new Vector3(0, currentStackHeight, 0);
							
							// Update stack height for the next item in the loop.
							currentStackHeight += itemHeight + stackSpacing;
							
							// Animate the item to its new, correct position.
							StartCoroutine(LerpToNewPosition(item));
						}
					}
				}

				private IEnumerator LerpToNewPosition(CargoItem item)
				{
					float duration = 0.3f;
					float elapsed = 0f;
					Vector3 startPosition = item.physicalItem.position;
					// Calculate world-space target from the newly assigned local position.
					Vector3 targetPosition = cargoAnchor.TransformPoint(item.localMountPosition);

					while (elapsed < duration)
					{
						float progress = elapsed / duration;
						float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
					
						item.physicalItem.position = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
					
						elapsed += Time.deltaTime;
						yield return null;
					}

					// Snap to final local position.
					item.physicalItem.localPosition = item.localMountPosition;
				}

			#endregion
			
			// ---------------------------------------------------------------------------------------------------------
			//-->                                 BALANCE MANAGEMENT SYSTEM
			// ---------------------------------------------------------------------------------------------------------
			
			#region Balance Management

			private void UpdateBalanceImpact()
			{
				float totalImpact = 0f;
				foreach (var cargo in carriedCargo)
				{
					totalImpact += cargo.balanceImpact;
				}
				balanceSwayIntensity = Mathf.Clamp(totalImpact * 0.2f, 0.5f, 2f);
			}

			private void ApplyCargoSway()
			{
				if (currentCarryWeight > 0f)
				{
					float weightRatio = currentCarryWeight / maxCarryWeight;
					float swayAmount = weightRatio * balanceSwayIntensity * 0.05f;
					
					swayTimer += Time.deltaTime * (1f + weightRatio);
					Vector3 swayOffset = new Vector3(
						Mathf.Sin(swayTimer * 1.2f) * swayAmount, 
						0f, 
						0f);
        
					cargoAnchor.localPosition = Vector3.Lerp(cargoAnchor.localPosition, cargoAnchorBaseTransform + swayOffset, Time.deltaTime * 2f);
				}
				else
				{
					cargoAnchor.localPosition = Vector3.Lerp(cargoAnchor.localPosition, cargoAnchorBaseTransform, Time.deltaTime * 3f);
				}
			}

			private void HandleBalance()
			{
				if (!PlayerIsMoving())
				{
					stationaryTimer += Time.deltaTime;
					isStationary = true;
				}
				else
				{
					stationaryTimer = 0f;
					isStationary = false;
				}
				
				Vector2 balanceInput = new Vector2(InputHandler.Instance.MovementInput.x,
					InputHandler.Instance.MovementInput.y);
				
				float movementModifier = playerController.GetCharacterController().velocity.magnitude * 0.5f;
				
				float weightRatio = (currentCarryWeight / maxCarryWeight) * 0.8f;
				float stackHeightRatio = currentStackHeight / maxStackHeight;
				
				float dynamicDangerThreshold = balanceDangerThreshold * (1f - weightRatio * 0.3f);
				float dynamicFallThreshold = balanceFallThreshold * (1f - weightRatio * 0.2f);
				
				float exponentialWeightImpact = Mathf.Pow(weightRatio, 2.2f) * 30f;
				float exponentialStackImpact = Mathf.Pow(stackHeightRatio, 1.8f) * 25f;

				float centerOfMassImpact = CalculateCenterOfMassImpact() * 15f;
				float weightBalanceImpact = exponentialWeightImpact + exponentialStackImpact + centerOfMassImpact;

				float balanceImpact = 0f;
				float totalNegativeImpact = 0f;
				if (PlayerIsMoving())
				{
					balanceImpact = balanceInput.magnitude * balanceSwayIntensity * 12f;
        
					float directionChangeImpact = CalculateDirectionChangeImpact() * weightRatio * 8f;
					totalNegativeImpact = (balanceImpact + movementModifier + weightBalanceImpact + directionChangeImpact) * balanceDepletionRate;
				}
				else
				{
					totalNegativeImpact = weightBalanceImpact * 0.1f;
				}

				float baseRecoveryRate = balanceRecoveryRate * (1f - weightRatio * 0.9f);
				float finalRecoveryRate = baseRecoveryRate;
    
				if (isStationary)
				{
					if (stationaryTimer > stationaryRecoveryDelay)
					{
						float stationaryBonus = Mathf.Min(stationaryRecoveryBonus * (stationaryTimer / 2f), stationaryRecoveryBonus * 2f);
						finalRecoveryRate += stationaryBonus;
            
						if (stationaryTimer > 2f)
						{
							finalRecoveryRate += 1f;
						}
					}
				}
				
				float balanceChange = (finalRecoveryRate * 8f - totalNegativeImpact) * Time.deltaTime;

				currentBalance = Mathf.Clamp(currentBalance + balanceChange, 0, maxBalance);
				
				UIGame.Instance.OnBlancedSliderChanged?.Invoke(currentBalance);
				OnBalanceChanged?.Invoke(currentBalance / maxBalance);

    
				bool isCritical = currentBalance < dynamicDangerThreshold;
				OnCriticalBalance?.Invoke(isCritical);
    
				if (currentBalance <= dynamicFallThreshold)
				{
					TriggerBalanceFall();
				}
    
				ApplyWeightEffects(weightRatio);
			}

			
			private float CalculateCenterOfMassImpact()
			{
				if (carriedCargo.Count == 0) return 0f;
    
				Vector3 centerOfMass = Vector3.zero;
				float totalWeight = 0f;
    
				foreach (var cargo in carriedCargo)
				{
					if (cargo.isMounted)
					{
						Vector3 cargoWorldPos = cargoAnchor.TransformPoint(cargo.localMountPosition);
						centerOfMass += cargoWorldPos * cargo.weight;
						totalWeight += cargo.weight;
					}
				}
    
				if (totalWeight > 0)
				{
					centerOfMass /= totalWeight;
					Vector3 offsetFromCenter = centerOfMass - cargoAnchor.position;
        
					float horizontalOffset = new Vector2(offsetFromCenter.x, offsetFromCenter.z).magnitude;
        
					float stationaryReduction = isStationary && stationaryTimer > stationaryRecoveryDelay ? 0.5f : 1f;
        
					return horizontalOffset * 2f * stationaryReduction;
				}
    
				return 0f;
			}


			
			private Vector3 lastMovementDirection;
			private float directionChangeTimer;

			private float CalculateDirectionChangeImpact()
			{
				Vector3 currentDirection = InputHandler.Instance.MovementInput.normalized;
    
				if (lastMovementDirection != Vector3.zero && currentDirection != Vector3.zero)
				{
					float dotProduct = Vector3.Dot(lastMovementDirection, currentDirection);
        
					if (dotProduct < 0.5f)
					{
						directionChangeTimer = 0.5f;
					}
				}
    
				lastMovementDirection = currentDirection;
    
				if (directionChangeTimer > 0)
				{
					directionChangeTimer -= Time.deltaTime;
					return Mathf.Clamp01(directionChangeTimer / 0.5f);
				}
    
				return 0f;
			}

			
			private void TriggerBalanceFall()
			{
				StartCoroutine(BalanceFallSequence());
			}

			private IEnumerator BalanceFallSequence()
			{
				stationaryTimer = 0f;
				isStationary = false;

				CalculateToRemoveCargo(0.3f);
    
				currentBalance = maxBalance * 0.6f;
    
				yield return new WaitForSeconds(1f);
			}

			public void CalculateToRemoveCargo(float cargoRemoverPrecentage)
			{
				int cargoToDrop = Mathf.CeilToInt(carriedCargo.Count * cargoRemoverPrecentage);
				for (int i = 0; i < cargoToDrop && carriedCargo.Count > 0; i++)
				{
					CargoItem heaviest = GetHeaviestCargo();
					if (heaviest != null)
					{
						RemoveCargoItem(heaviest);
						OnCargoDropped?.Invoke();
					}
				}
			}

			private CargoItem GetHeaviestCargo()
			{
				return carriedCargo.OrderByDescending(c => c.weight).FirstOrDefault();
			}
			
			private void ApplyWeightEffects(float weightRatio)
			{
				// Placeholder for camera effects or other visual feedback.
			}
			
			
			public float ApplyMovementModifiers()
			{
				float speedModifier = 1 - (currentCarryWeight * weightSpeedModifier);
				float balanceRatio = currentBalance / maxBalance;
				SetBalanceState(balanceRatio);
				float balanceSpeedPenalty = balanceRatio < 0.5f ? (1f - balanceRatio) * 0.3f : 0f;
    
				float terrainModifier = 1f;
				float finalSpeed = (speedModifier - balanceSpeedPenalty) * terrainModifier;
				return Mathf.Clamp(finalSpeed, 0.1f, 1f);

			}
			
			
			#endregion
			
		#endregion
		
		
		
			
		#region Helper

		public Transform GetCargoAnchor() => cargoAnchor;
		public bool IsPlayerStationary() => isStationary;
		public float GetStationaryTime() => stationaryTimer;
		public float GetRecoveryRate() 
		{
			float baseRate = balanceRecoveryRate * (1f - (currentCarryWeight / maxCarryWeight) * 0.9f);
			if (isStationary && stationaryTimer > stationaryRecoveryDelay)
			{
				float stationaryBonus = Mathf.Min(stationaryRecoveryBonus * (stationaryTimer / 2f), stationaryRecoveryBonus * 2f);
				return baseRate + stationaryBonus;
			}
			return baseRate;
		}

		public void SetBalanceState(float balanceRatio)
		{
			if (balanceRatio > 0.7f) balanceState = BalanceState.Stable;
			else if (balanceRatio > 0.4f) balanceState = BalanceState.Unstable;
			else if (balanceRatio > 0.2f) balanceState = BalanceState.Critical;
			else balanceState = BalanceState.Falling;
    
			if (isStationary && stationaryTimer > stationaryRecoveryDelay)
			{
				balanceState = BalanceState.Recovery;
			}
			
		}

		public void ReduceBalance(float amount)
		{
			currentBalance = Mathf.Max(0, currentBalance - amount);
			stationaryTimer = 0f;
		}
		
		public void IncreaseBalance(float amount)
		{
			currentBalance = Mathf.Max(0, currentBalance + amount);
			stationaryTimer = 0f;
		}
			
		public bool PlayerIsMoving() => InputHandler.Instance.IsMovementInputNonZero && 
		                                InputHandler.Instance.MovementInput.magnitude > 0.025f;
		public bool IsPlayerAbleToMountCargo() => currentCarryWeight <= maxCarryWeight;
		
		public float GetWeightCarryRatio() => currentCarryWeight / maxCarryWeight;
		public float GetCurrentCarryWeight() => currentCarryWeight;
		public float GetMaxCarryWeight() => maxCarryWeight;
		public BalanceState GetBalanceState() => balanceState;

		public float GetCurrentBalance() => currentBalance;

		#endregion

	}
	
	public enum BalanceState
	{
		Stable,
		Unstable,
		Critical,
		Recovery,
		Falling
	}

	[Serializable]
	public class CargoItem
	{
		public string ID;
		public string Name;
		public Transform physicalItem;
		public Vector3 localMountPosition;
		public Vector3 mountOffset;
		public float weight;
		public float size;
		public bool fragile;
		public float balanceImpact;
		public bool isMounted;
	}
}
