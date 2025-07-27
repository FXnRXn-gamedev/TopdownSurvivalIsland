#pragma warning disable 0067
#pragma warning disable 0414


using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

namespace FXnRXn
{ 
	public class PorterSystem : MonoBehaviour
	{
		#region Properties

		[Header("--- Porter Settings ---")] 
		[SerializeField] private Transform						cargoAnchor;
		[SerializeField] private float							maxCarryWeight = 120f;
		[SerializeField] private float							balanceRecoveryRate = 1.5f;
		[Range(0, 1)][SerializeField] private float				balanceDepletionRate = 0.25f;
		

		[Header("--- Stacking Settings ---")] 
		[SerializeField] private float							stackSpacing = 0.1f;
		[SerializeField] private float							maxStackHeight = 15f;
		[SerializeField] private LayerMask						cargoLayerMask = -1;
		[SerializeField] private bool							autoRotateItems = true;
		
		[Header("--- Balance System ---")]
		[SerializeField] private float							maxBalance = 100f;
		[SerializeField] private float							currentBalance;
		[SerializeField] private float							balanceDangerThreshold = 30f;
		[SerializeField] private float							balanceFallThreshold = 10f;
		[SerializeField] private float							balanceSwayIntensity = 0.8f;
		[SerializeField] private float							balanceStabilizationSpeed = 2f;

		[Header("--- Physics Settings ---")]
		[SerializeField] private float							weightSpeedModifier = 0.005f;
		[SerializeField] private float							terrainSlopeModifier = 0.5f;
		[SerializeField] private float							waterDragModifier = 0.7f;
		[SerializeField] private float							staminaDrainPerKg = 0.1f;
		
		[Header("--- Balance Recovery ---")]
		[SerializeField] private float							stationaryRecoveryBonus = 2f;
		[SerializeField] private float							stationaryRecoveryDelay = 0.5f; // Time before stationary recovery kicks in
		[SerializeField] private float							crouchRecoveryBonus = 1.5f;


		[Header("--- Events ---")]
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
		private List<StackLayer> stackLayers = new List<StackLayer>();

		#endregion

		

		#region Unity Callbacks

		private void Awake()
		{
			playerController = GetComponent<PlayerController>();
			if(characterController == null) characterController = GetComponent<CharacterController>();

			currentBalance = maxBalance;
			cargoAnchorBaseTransform = cargoAnchor.localPosition;
			InitializeStackingSystem();
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

			private void InitializeStackingSystem()
			{
				stackLayers.Clear();
				currentStackHeight = 0f;
			}

			private Vector3 CalculateStackPosition(CargoItem item)
			{
				Vector3 stackPosition = cargoAnchor.position;
				//Get item bounds
				Bounds itemBounds = GetItemBounds(item.physicalItem);
				float itemHeight = itemBounds.size.y;
				
				// Find the best position in the current stack
				Vector3 bestPosition = FindBestStackPosition(itemBounds);
				
				return bestPosition;
			}

			private Vector3 FindBestStackPosition(Bounds itemBounds)
			{
				Vector3 basePosition = cargoAnchor.position;
				Vector3 bestPosition = basePosition;
				
				// Start from the base and work our way up
				float testHeight = 0f;

				while (testHeight < maxStackHeight)
				{
					Vector3 testPosition = basePosition + Vector3.up * testHeight;
					
					// Check if this position is clear
					if (IsPositionClear(testPosition, itemBounds))
					{
						bestPosition = testPosition;
						break;
					}
					testHeight += itemBounds.size.y + stackSpacing;
				}
				return bestPosition;
			}

			private bool IsPositionClear(Vector3 position, Bounds itemBounds)
			{
				// Create a slightly smaller bounds for overlap checking
				Vector3 checkSize = itemBounds.size * 0.5f;
				
				// Check for overlapping colliders
				Collider[] overlapping = Physics.OverlapBox(position, checkSize * 0.5f, Quaternion.identity, cargoLayerMask);
				
				// Filter out non-cargo items and check if any cargo items overlap
				foreach (var collider in overlapping)
				{
					if (IsCargoItem(collider.transform))
					{
						return false;
					}
				}
				return true;
			}
			
			private bool IsCargoItem(Transform transform)
			{
				// Check if this transform belongs to any of our carried cargo
				foreach (var cargo in carriedCargo)
				{
					if (cargo.physicalItem == transform)
						return true;
				}
				return false;
			}


			private Bounds GetItemBounds(Transform item)
			{
				Bounds bounds = new Bounds();
				Collider collider = item.GetComponent<Collider>();
				if (collider != null)
				{
					bounds = collider.bounds;
				}
				else
				{
					Renderer renderer = item.GetComponent<Renderer>();
					if (renderer != null)
					{
						bounds = renderer.bounds;
					}
					else
					{
						bounds = new Bounds(item.position, Vector3.one);
					}
				}
				return bounds;
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

					carriedCargo.Add(item);
					currentCarryWeight += item.weight;
		        
					MountCargoItem(item);
					UpdateBalanceImpact();
					
					// OnWeightChanged?.Invoke(GetWeightRatio());
				}

				private void MountCargoItem(CargoItem item)
				{
					item.isMounted = true;
					// Calculate the stack position for this item
					Vector3 targetPosition = CalculateStackPosition(item);
					item.localMountPosition = cargoAnchor.InverseTransformPoint(targetPosition);
					// parent to cargo anchor with physics
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
					Vector3 targetPosition = cargoAnchor.TransformPoint(item.localMountPosition);
					Quaternion targetRotation = autoRotateItems ? cargoAnchor.rotation : item.physicalItem.rotation;
					
					while (elapsed < duration)
					{
						float progress = elapsed / duration;
						// Smooth curve for more natural movement
						float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
						
						Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
						
						float arcOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
						currentPos.y += arcOffset;
						
						item.physicalItem.position = currentPos;
						item.physicalItem.rotation = Quaternion.Lerp(startRotation, targetRotation, smoothProgress);
						elapsed += Time.deltaTime;
						yield return null;
					}
					item.physicalItem.localPosition = item.localMountPosition;
					item.physicalItem.localRotation = autoRotateItems ? Quaternion.identity : Quaternion.Inverse(cargoAnchor.rotation) * startRotation;
					// Update current stack height
					UpdateStackHeight();

					
					// Disable cargo physics if it has FragileCargo component
					FragileCargo fragileCargo = item.physicalItem.GetComponent<FragileCargo>();
					if (fragileCargo != null)
					{
						fragileCargo.DisableCargo();
					}

				}

				private void UpdateStackHeight()
				{
					currentStackHeight = 0f;
					foreach (var cargo in carriedCargo)
					{
						if (cargo.isMounted)
						{
							Bounds bounds = GetItemBounds(cargo.physicalItem);
							float itemTop = bounds.max.y - cargoAnchor.position.y;
							if (itemTop > currentStackHeight)
							{
								currentStackHeight = itemTop;
							}
						}
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
					
						// Reorganize remaining cargo
						StartCoroutine(ReorganizeStack());
					}
				}
				
				private IEnumerator ReorganizeStack()
				{
					yield return new WaitForSeconds(0.1f); // Small delay to let physics settle
				
					// Temporarily store all cargo items
					List<CargoItem> itemsToReorganize = new List<CargoItem>(carriedCargo);
					
					itemsToReorganize.Sort((a, b) => b.weight.CompareTo(a.weight));
    
					// Clear the cargo list to rebuild it
					carriedCargo.Clear();
					currentStackHeight = 0f;
    
					// Re-add items one by one, calculating proper positions
					float accumulatedHeight = 0f; // Track the accumulated height
    
					foreach (var item in itemsToReorganize)
					{
						if (item.isMounted)
						{
							// Calculate position based on accumulated height
							Vector3 newPosition = CalculateStackPositionSequential(item, accumulatedHeight);
							item.localMountPosition = cargoAnchor.InverseTransformPoint(newPosition);
            
							// Add to cargo list AFTER calculating position
							carriedCargo.Add(item);
            
							// Update accumulated height for next item
							Bounds itemBounds = GetItemBounds(item.physicalItem);
							accumulatedHeight += itemBounds.size.y + stackSpacing;
            
							StartCoroutine(LerpToNewPosition(item));
						}
					}



				}
				
				private Vector3 CalculateStackPositionSequential(CargoItem item , float currentHeight)
				{
					Vector3 basePosition = cargoAnchor.position;
					return basePosition + Vector3.up * currentHeight;

				}

				
				private IEnumerator LerpToNewPosition(CargoItem item)
				{
					
					float duration = 0.3f;
					float elapsed = 0f;
					Vector3 startPosition = item.physicalItem.position;
					Vector3 targetPosition = cargoAnchor.TransformPoint(item.localMountPosition);

					while (elapsed < duration)
					{
						float progress = elapsed / duration;
						float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
					
						item.physicalItem.position = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
					
						elapsed += Time.deltaTime;
						yield return null;
					}

					item.physicalItem.localPosition = item.localMountPosition;
					UpdateStackHeight();
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
					
					// Create subtle sway effect on cargo anchor
					swayTimer += Time.deltaTime * (1f + weightRatio);
					Vector3 swayOffset = new Vector3(
						Mathf.Sin(swayTimer * 1.2f) * swayAmount, 
						0f, 
						0f); //Mathf.Cos(swayTimer * 0.8f) * swayAmount * 0.5f
        
					cargoAnchor.localPosition = Vector3.Lerp(cargoAnchor.localPosition, cargoAnchorBaseTransform + swayOffset, Time.deltaTime * 2f);
				}
				else
				{
					// Return to center when no cargo
					cargoAnchor.localPosition = Vector3.Lerp(cargoAnchor.localPosition, cargoAnchorBaseTransform, Time.deltaTime * 3f);
				}
			}

			private void HandleBalance()
			{
				// Update stationary state
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
				
				// Calculate player input for balance
				Vector2 balanceInput = new Vector2(InputHandler.Instance.MovementInput.x,
					InputHandler.Instance.MovementInput.y);
				
				// Apply terrain effects [Add Later]
				//
				// Apply player movement effects
				float movementModifier = playerController.GetCharacterController().velocity.magnitude * 0.5f;
				
				// Calculate weight impact on balance - MORE AGGRESSIVE
				float weightRatio = (currentCarryWeight / maxCarryWeight) * 0.8f;
				float stackHeightRatio = currentStackHeight / maxStackHeight;
				
				// Dynamic balance thresholds based on weight
				float dynamicDangerThreshold = balanceDangerThreshold * (1f - weightRatio * 0.3f);
				float dynamicFallThreshold = balanceFallThreshold * (1f - weightRatio * 0.2f);
				
				// Enhanced weight impact calculation
				float exponentialWeightImpact = Mathf.Pow(weightRatio, 2.2f) * 30f; // More aggressive
				float exponentialStackImpact = Mathf.Pow(stackHeightRatio, 1.8f) * 25f; // Stack instability



				// Add center of mass impact
				float centerOfMassImpact = CalculateCenterOfMassImpact() * 15f;
				float weightBalanceImpact = exponentialWeightImpact + exponentialStackImpact + centerOfMassImpact;

				// Calculate balance change with enhanced weight impact
				// float balanceImpact = balanceInput.magnitude * balanceSwayIntensity * 2f;
				// float totalNegativeImpact = balanceImpact + movementModifier + weightBalanceImpact;
				float balanceImpact = 0f;
				float totalNegativeImpact = 0f;
				if (PlayerIsMoving())
				{
					// Normal balance loss when moving
					balanceImpact = balanceInput.magnitude * balanceSwayIntensity * 12f;
        
					// Add momentum-based balance loss when changing direction quickly
					float directionChangeImpact = CalculateDirectionChangeImpact() * weightRatio * 8f;
					totalNegativeImpact = (balanceImpact + movementModifier + weightBalanceImpact + directionChangeImpact) * balanceDepletionRate;
				}
				else
				{
					// Minimal balance loss when stationary
					totalNegativeImpact = weightBalanceImpact * 0.1f; // Only 10% of weight impact when not moving
				}

				// Calculate recovery rate based on movement state
				float baseRecoveryRate = balanceRecoveryRate * (1f - weightRatio * 0.9f);
				float finalRecoveryRate = baseRecoveryRate;
    
				// Enhanced recovery when stationary
				if (isStationary)
				{
					if (stationaryTimer > stationaryRecoveryDelay)
					{
						// Progressive recovery bonus - the longer you stand still, the better the recovery
						float stationaryBonus = Mathf.Min(stationaryRecoveryBonus * (stationaryTimer / 2f), stationaryRecoveryBonus * 2f);
						finalRecoveryRate += stationaryBonus;
            
						// Additional bonus for being completely still for a while
						if (stationaryTimer > 2f)
						{
							finalRecoveryRate += 1f; // Extra recovery for patience
						}
					}
				}
				
				float balanceChange = (finalRecoveryRate * 8f - totalNegativeImpact) * Time.deltaTime;

				currentBalance = Mathf.Clamp(currentBalance + balanceChange, 0, maxBalance);
    
				OnBalanceChanged?.Invoke(currentBalance / maxBalance);

    
				// Dynamic critical balance state
				bool isCritical = currentBalance < dynamicDangerThreshold;
				OnCriticalBalance?.Invoke(isCritical);
    
				// Handle falling with dynamic threshold
				if (currentBalance <= dynamicFallThreshold)
				{
					Debug.Log("Triggering balance fall");
					TriggerBalanceFall();
				}
    
				// Add weight-based screen shake or camera tilt effect
				ApplyWeightEffects(weightRatio);
				
				// Debug info
				// if (Application.isEditor)
				// {
				// 	string status = isStationary ? "STATIONARY" : "MOVING";
				// 	string recoveryInfo = $"Recovery: {finalRecoveryRate:F1} | Timer: {stationaryTimer:F1}";
				// 	Debug.Log($"{status} | Balance: {currentBalance:F1} | {recoveryInfo}");
				// }


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
        
					// Calculate how far off-center the mass is (horizontally)
					float horizontalOffset = new Vector2(offsetFromCenter.x, offsetFromCenter.z).magnitude;
        
					// Reduce center of mass impact when stationary
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
        
					// If direction changed significantly (dot product < 0.5 means > 60 degree change)
					if (dotProduct < 0.5f)
					{
						directionChangeTimer = 0.5f; // Impact lasts for half a second
					}
				}
    
				lastMovementDirection = currentDirection;
    
				if (directionChangeTimer > 0)
				{
					directionChangeTimer -= Time.deltaTime;
					return Mathf.Clamp01(directionChangeTimer / 0.5f); // Normalize to 0-1
				}
    
				return 0f;
			}

			
			private void TriggerBalanceFall()
			{
				// Enhanced fall behavior
				StartCoroutine(BalanceFallSequence());
			}

			private IEnumerator BalanceFallSequence()
			{
				// Reset stationary timer since falling interrupts recovery
				stationaryTimer = 0f;
				isStationary = false;
    
				// Disable player movement temporarily
				// Note: You'll need to make IsMovementEnabled public in PlayerController
				// playerController.IsMovementEnabled = false;

    
				// Drop some cargo (not all, for gameplay balance)
				int cargoToDrop = Mathf.CeilToInt(carriedCargo.Count * 0.3f); // Drop 30% of cargo
				for (int i = 0; i < cargoToDrop && carriedCargo.Count > 0; i++)
				{
					// Drop heaviest items first
					CargoItem heaviest = GetHeaviestCargo();
					if (heaviest != null)
					{
						RemoveCargoItem(heaviest);
						OnCargoDropped?.Invoke();
					}
				}
    
				// Partial balance recovery
				currentBalance = maxBalance * 0.6f;
    
				yield return new WaitForSeconds(1f); // Fall animation time
    
				// Re-enable movement
				//playerController.IsMovementEnabled = true;
			}

			private CargoItem GetHeaviestCargo()
			{
				CargoItem heaviest = null;
				float maxWeight = 0f;
    
				foreach (var cargo in carriedCargo)
				{
					if (cargo.weight > maxWeight)
					{
						maxWeight = cargo.weight;
						heaviest = cargo;
					}
				}
    
				return heaviest;
			}
			
			private void ApplyWeightEffects(float weightRatio)
			{
				// This can be used to communicate with camera controller for screen effects
				// For example, slight camera tilt based on center of mass
				if (weightRatio > 0.5f)
				{
					// Could trigger screen shake, camera tilt, or other visual feedback
					// Example: Camera.main.transform.localRotation = Quaternion.Euler(0, 0, centerOfMassOffset * 5f);
				}
			}
			
			
			public float ApplyMovementModifiers()
			{
				float speedModifier = 1 - (currentCarryWeight * weightSpeedModifier);
				// Additional balance-based movement penalty
				float balanceRatio = currentBalance / maxBalance;
				float balanceSpeedPenalty = balanceRatio < 0.5f ? (1f - balanceRatio) * 0.3f : 0f;
    
				float terrainModifier = 1f;
				float finalSpeed = (speedModifier - balanceSpeedPenalty) * terrainModifier;
				return Mathf.Clamp(finalSpeed, 0.1f, 1f);

			}
			
			// Helper methods for external access
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

			// Helper method to get current balance state for UI
			public string GetBalanceState()
			{
				float balanceRatio = currentBalance / maxBalance;
				string state = "";
    
				if (balanceRatio > 0.7f) state = "Stable";
				else if (balanceRatio > 0.4f) state = "Unstable";
				else if (balanceRatio > 0.2f) state = "Critical";
				else state = "Falling";
    
				if (isStationary && stationaryTimer > stationaryRecoveryDelay)
				{
					state += " (Recovering)";
				}
    
				return state;
			}

			// Public method to manually trigger balance loss (for external events)
			public void ReduceBalance(float amount)
			{
				currentBalance = Mathf.Max(0, currentBalance - amount);
				// Reset stationary timer when taking external balance damage
				stationaryTimer = 0f;
			}

		
			
			#endregion
			
			
		#endregion
			
			
		
		#region Helper
			
		public bool PlayerIsMoving() => InputHandler.Instance.IsMovementInputNonZero && 
		                                InputHandler.Instance.MovementInput.magnitude > 0.025f;
		public bool IsPlayerAbleToMountCargo() => currentCarryWeight <= maxCarryWeight;
		
		public float GetWeightCarryRatio() => currentCarryWeight / maxCarryWeight;
		public float GetCurrentCarryWeight() => currentCarryWeight;
		public float GetMaxCarryWeight() => maxCarryWeight;

		#endregion

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

	[Serializable]
	public class StackLayer
	{
		public float height;
		public List<CargoItem> items = new List<CargoItem>();
		public float maxItemHeight;
	}
}
