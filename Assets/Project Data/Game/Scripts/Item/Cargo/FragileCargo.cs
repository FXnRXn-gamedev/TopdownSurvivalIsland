using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FXnRXn
{
	[RequireComponent(typeof(Rigidbody))]
	public class FragileCargo : MonoBehaviour
	{
		public enum CargoState { Grounded, Mounting, Mounted, Transferring, Damaged }
	
		#region Properties
		
		[Header("--- Cargo Settings ---")] 
		public string id;
		public string item;
		public float weight = 10f;
		public float size = 1f;
		public bool fragile;
		public bool isMounted = false;
		public float durability = 100f;
		public float damageProtection = 0f; // 0-1 protection level
		public float stackHeight;
		public Vector3 targetMountPosition;
		public CargoState currentState = CargoState.Grounded;
		
		[Header("--- Player Detection ---")]
		public LayerMask playerLayer = -1; // Layer mask for player detection
		public float detectionRange = 2f;
		[SerializeField] private float stayDuration = 1f;
		
		[Header("--- UI Components ---")]
		[SerializeField] private Image loaderFillImage;

		
		// Events for player detection
		public Action OnPlayerEnterRange;
		public Action OnPlayerExitRange;
		
		private bool playerInRange = false;
		private Transform playerTransform;
		private FragileCargoOutline outlineController;
		private Coroutine stayTimerCoroutine;
		private float progress;
		private float timeRemaining;



		#endregion
		

		
		#region Unity Callbacks
		
		private void Awake()
		{
			// Generate unique ID if not already set
			if (string.IsNullOrEmpty(id))
			{
				id = Guid.NewGuid().ToString();

			}
			var cargoOutliner = GetComponentInChildren<FragileCargoOutline>();
			cargoOutliner.InitializeComponents(this);
		}
		
		private void Start()
		{
			OnPlayerEnterRange += StartStayTimer;
			OnPlayerExitRange += StopStayTimer;

			
			// Subscribe to the highlight events
			OnPlayerEnterRange += EnableHighlight;
			OnPlayerExitRange += DisableHighlight;
			
			// Get or add outline controller
			if (outlineController == null)
			{
				outlineController = GetComponentInChildren<FragileCargoOutline>();
			}

			if (currentState == CargoState.Grounded)
			{
				ShowLoaderUI();
			}
		}

		private void OnDisable()
		{
			
			OnPlayerEnterRange += StartStayTimer;
			OnPlayerExitRange += StopStayTimer;

			// Unsubscribe from events
			OnPlayerEnterRange -= EnableHighlight;
			OnPlayerExitRange -= DisableHighlight;
			
			// Stop any running coroutines
			if (stayTimerCoroutine != null)
			{
				StopCoroutine(stayTimerCoroutine);
				stayTimerCoroutine = null;
			}


		}

		private void Update()
		{
			DetectPlayerNearBounds();

		}

		#endregion
		
		#region Methods

		private void StartStayTimer()
		{
			AddItemToPlayer();
			if(stayTimerCoroutine != null) StopCoroutine(stayTimerCoroutine);
			
			stayTimerCoroutine = StartCoroutine(StayTimerCoroutine());
			//
		}
		
		private void StopStayTimer()
		{
			if (stayTimerCoroutine != null)
			{
				progress = 0;
				timeRemaining = 0;
				UpdateLoaderUI(0f);
				StopCoroutine(stayTimerCoroutine);
				stayTimerCoroutine = null;
				//
			}
		}
		private IEnumerator StayTimerCoroutine()
		{
			float elapsedTime = 0f;
			
			while (elapsedTime < stayDuration)
			{
				// Check if player is still in range
				if (!playerInRange || playerTransform == null)
				{
					HideLoaderUI();
					yield break;
				}
				
				elapsedTime += Time.deltaTime;
				progress = elapsedTime / stayDuration;
				timeRemaining = stayDuration - elapsedTime;
				
				// Update loader UI
				UpdateLoaderUI(progress);
				
				yield return null;
			}

			// Check if player is still in range after the delay
			if (playerInRange && playerTransform != null)
			{
				MountCargoToPlayer();
			}
			
			stayTimerCoroutine = null;

		}
		
		
		private void AddItemToPlayer()
		{

			if(playerTransform == null && !playerInRange) return;
			
			var porterSystem = playerTransform.GetComponent<PorterSystem>();
			if (porterSystem == null || !porterSystem.IsPlayerAbleToMountCargo()) return;
			
			if (currentState == CargoState.Grounded)
			{
				Debug.Log($"Player entered range of {item} - Item added to player inventory");
			}
		}
		private void MountCargoToPlayer()
		{
			if (playerTransform == null || !playerInRange) return;
			
			var porterSystem = playerTransform.GetComponent<PorterSystem>();
			if (porterSystem == null || !porterSystem.IsPlayerAbleToMountCargo()) return;
			
			if (currentState == CargoState.Grounded)
			{
				var cargoData = new CargoItem();
				cargoData.ID = id;
				cargoData.Name = item;
				cargoData.physicalItem = transform;
				cargoData.localMountPosition = transform.localPosition;
				cargoData.weight = weight;
				cargoData.size = size;
				cargoData.fragile = fragile;
				cargoData.balanceImpact = 2f;
				cargoData.isMounted = true;
				isMounted = true;
				porterSystem.PickupCargoItem(cargoData);
				DisableHighlight();
				HideLoaderUI();
				Debug.Log($"Cargo {item} mounted to player anchor position after {stayDuration}s delay");
			}
		}


		#region Highlight System


		private void EnableHighlight()
		{
			if (outlineController != null)
			{
				outlineController.EnableOutline();
				
				// Optional: Change outline color based on cargo state
				// if (fragile)
				// {
				// 	outlineController.SetOutlineColor(Color.red);
				// }
				// else
				// {
				// 	outlineController.SetOutlineColor(new Color(1f, 0f, 1f, 0.8117647f)); // Orange
				// }
			}

		}
		
		private void DisableHighlight()
		{
			if (outlineController != null)
			{
				outlineController.DisableOutline();
			}

		}


		#endregion
		
		
		
		public Bounds GetBounds()
		{
			Renderer r = GetComponent<Renderer>();
			return r != null ? r.bounds : new Bounds(transform.position, Vector3.one * size);
		}
		private void DetectPlayerNearBounds()
		{
			Bounds bounds = GetBounds();
			
			// Expand bounds by detection range
			Bounds detectionBounds = new Bounds(bounds.center, bounds.size + Vector3.one * detectionRange * 2);
			
			// Find all colliders within the detection bounds
			Collider[] nearbyColliders = Physics.OverlapBox(
				detectionBounds.center,
				detectionBounds.extents,
				transform.rotation,
				playerLayer
			);

			bool playerFound = false;
			Transform foundPlayer = null;

			// Check if any of the found colliders belong to a player
			foreach (Collider col in nearbyColliders)
			{
				if (col.CompareTag("Player")) // Assuming player has "Player" tag
				{
					playerFound = true;
					foundPlayer = col.transform;
					break;
				}
			}

			// Handle player enter/exit events
			if (playerFound && !playerInRange)
			{
				playerInRange = true;
				playerTransform = foundPlayer;
				OnPlayerEnterRange?.Invoke();
				Debug.Log($"Player entered range of {item}");
			}
			else if (!playerFound && playerInRange)
			{
				playerInRange = false;
				playerTransform = null;
				OnPlayerExitRange?.Invoke();
				Debug.Log($"Player exited range of {item}");
			}
		}
		public void DisableCargo()
		{
			// Reset cargo state
			currentState = CargoState.Mounted;
			// Reset player detection
			playerInRange = false;
			playerTransform = null;
			// Disable highlight
			DisableHighlight();
			
			// Reset any physics
			Rigidbody rb = GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.isKinematic = true;
				rb.interpolation = RigidbodyInterpolation.None;
			}
			
			// Reset parent
			isMounted = true;
			Debug.Log($"FragileCargo {item} has been Disable");
		}
		
		public void ResetFragileCargo()
		{
			// Reset cargo state
			currentState = CargoState.Grounded;
			ShowLoaderUI();
			// Reset player detection
			playerInRange = false;
			playerTransform = null;
			
			// Disable highlight
			DisableHighlight();
			
			
			// Reset any physics
			Rigidbody rb = GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.isKinematic = false;
				rb.interpolation = RigidbodyInterpolation.Interpolate;
			}
			
			// Reset parent
			transform.SetParent(null);
			isMounted = false;
			Debug.Log($"FragileCargo {item} has been reset");
		}

		
		#endregion

		#region UI

		public void ShowLoaderUI()
		{
			if (loaderFillImage != null)
			{
				loaderFillImage.fillAmount = 0f;
				loaderFillImage.enabled = true;
			}
		}
		
		private void UpdateLoaderUI(float progress)
		{
			if (loaderFillImage != null && currentState == CargoState.Grounded)
				loaderFillImage.fillAmount = progress;

		}

		public void HideLoaderUI()
		{
			if (loaderFillImage != null)
			{
				loaderFillImage.fillAmount = 0f;
				loaderFillImage.enabled = false;
			}
		}

		#endregion

		#region Helper Methods
		
		public float GetDistanceToPlayer()
		{
			if (playerInRange && playerTransform != null)
			{
				return Vector3.Distance(transform.position, playerTransform.position);
			}
			return float.MaxValue;
		}
		
		public bool IsPlayerNearBounds()
		{
			return playerInRange;
		}
		
		private void DetectPlayerNearBounds_Spherical()
		{
			Bounds bounds = GetBounds();
			float sphereRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) + detectionRange;
			
			Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, sphereRadius, playerLayer);
			
			bool playerFound = false;
			Transform foundPlayer = null;

			foreach (Collider col in nearbyColliders)
			{
				if (col.CompareTag("Player"))
				{
					playerFound = true;
					foundPlayer = col.transform;
					break;
				}
			}

			if (playerFound && !playerInRange)
			{
				playerInRange = true;
				playerTransform = foundPlayer;
				OnPlayerEnterRange?.Invoke();
			}
			else if (!playerFound && playerInRange)
			{
				playerInRange = false;
				playerTransform = null;
				OnPlayerExitRange?.Invoke();
			}
		}

		public string GetItemID() => id;
		
		public CargoState GetCargoState() => currentState;
		
		#endregion
		
		//--------------------------------------------------------------------------------------------------------------
		
		private void OnDrawGizmosSelected()
		{
			Bounds bounds = GetBounds();
			
			// Draw cargo bounds
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(bounds.center, bounds.size);
			
			// Draw detection range
			Gizmos.color = playerInRange ? Color.green : Color.red;
			Bounds detectionBounds = new Bounds(bounds.center, bounds.size + Vector3.one * detectionRange * 2);
			Gizmos.DrawWireCube(detectionBounds.center, detectionBounds.size);
			
			// Draw connection line to player if in range
			if (playerInRange && playerTransform != null)
			{
				Gizmos.color = Color.cyan;
				Gizmos.DrawLine(transform.position, playerTransform.position);
			}

		}
	}
}
