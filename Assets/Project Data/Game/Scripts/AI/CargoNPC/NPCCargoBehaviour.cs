#pragma warning disable 0067
#pragma warning disable 0414

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TriInspector;
using UnityEngine;
using UnityEngine.UI;

namespace FXnRXn
{
	[RequireComponent(typeof(CharacterController))]
	public class NPCCargoBehaviour : MonoBehaviour
	{
		#region Properties
		
		[Title("NPC Settings")]
		[Space(10)]
		[SerializeField] private string									npcName = "Merchant-Fedrick";
		[SerializeField] private NPCType								npcType = NPCType.Both;
		[SerializeField] private OrderType								orderType = OrderType.Cargo;
		[SerializeField] private float									interactionRange = 3f;
		[SerializeField] private LayerMask								playerLayer = -1;
		
		[Title("Order Generation")]
		[Space(10)]
		[ShowIf("orderType", OrderType.Cargo)]
		[SerializeField] private int									maxActiveOrders = 3;
		[ShowIf("orderType", OrderType.Cargo)]
		[SerializeField] private float									orderGenerationInterval = 30f;
		[ShowIf("orderType", OrderType.Cargo)]
		[SerializeField] private List<OrderTemplate>					availableOrderTemplates = new List<OrderTemplate>();
		
		[Title("Rewards")]
		[Space(10)]
		[SerializeField] private int									baseReward = 100;
		[SerializeField] private float									fragileRewardMultiplier = 1.5f;
		[SerializeField] private float									urgentRewardMultiplier = 2f;
		
		
		[Title("UI References")]
		[Space(10)]
		[SerializeField] private GameObject orderUIPanel;
		[SerializeField] private Transform orderListParent;
		[SerializeField] private GameObject orderItemPrefab;
		[SerializeField] private Button acceptOrderButton;
		[SerializeField] private Button deliverCargoButton;
		[SerializeField] private TMP_Text								npcNameText;
		[SerializeField] private Text interactionPromptText;
		
		// Private fields
		private List<CargoOrder> activeOrders = new List<CargoOrder>();
		private List<CargoOrder> completedOrders = new List<CargoOrder>();
		private CargoOrder selectedOrder;
		private bool playerInRange = false;
		private Transform playerTransform;
		private PorterSystem playerPorterSystem;
		private Coroutine orderGenerationCoroutine;
		//private NPCOutline npcOutline;
        
		// Events
		public Action<CargoOrder> OnOrderGenerated;
		public Action<CargoOrder> OnOrderAccepted;
		public Action<CargoOrder> OnOrderCompleted;
		public Action<CargoOrder> OnOrderExpired;
		
		#endregion

		#region Unity Methods

		private void Awake()
		{
			if (npcNameText != null) npcNameText.text = npcName;
			
			//SetupUI();
		}

		private void Start()
		{
			if (npcType == NPCType.OrderGiver || npcType == NPCType.Both)
			{
				orderGenerationCoroutine = StartCoroutine(GenerateOrdersRoutine());
			}

			InputHandler.Instance.onInteract += AcceptAndInteractOrder;
			
		}

		private void OnDisable()
		{
			InputHandler.Instance.onInteract -= AcceptAndInteractOrder;
		}


		private void Update()
		{
			DetectPlayer();
		}

		private void OnDestroy()
		{
			if (orderGenerationCoroutine != null)
				StopCoroutine(orderGenerationCoroutine);
		}

		#endregion

		#region Methods

		// -------------------------------------------------------------------------------------------------------------
		//-->                                 ORDER MANAGEMENT SYSTEM												 <--
		// -------------------------------------------------------------------------------------------------------------
		
		#region Order Management
		
		private void AcceptAndInteractOrder()
		{
			if (!playerInRange) return;
			
			foreach (var order in activeOrders.Where(o => o.orderStatus == OrderStatus.Available))
			{
				AcceptOrder(order);
				
			}
		}
		
		

		private IEnumerator GenerateOrdersRoutine()
		{
			while (true)
			{
				yield return new WaitForSeconds(orderGenerationInterval);
				if (activeOrders.Count < maxActiveOrders && availableOrderTemplates.Count > 0)
				{
					GenerateRandomOrder();
				}
			}
		}

		private void GenerateRandomOrder()
		{
			if (availableOrderTemplates.Count == 0) return;
            
			OrderTemplate template = availableOrderTemplates[UnityEngine.Random.Range(0, availableOrderTemplates.Count)];
            
			CargoOrder newOrder = new CargoOrder
			{
				orderID = Guid.NewGuid().ToString(),
				orderName = template.orderName,
				description = template.description,
				physicalEntity = template.orderPrefab,
				requiredCargoItems = new List<RequiredCargoItem>(template.requiredItems),
				destinationNPC = template.destinationNPC,
				timeLimit = template.timeLimit,
				isUrgent = template.isUrgent,
				orderStatus = OrderStatus.Available,
				creationTime = Time.time,
				reward = CalculateOrderReward(template)
			};
            
			activeOrders.Add(newOrder);
			OnOrderGenerated?.Invoke(newOrder);
            
			Debug.Log($"Generated new order: {newOrder.orderName} with reward: {newOrder.reward}");
		}
		
		private int CalculateOrderReward(OrderTemplate template)
		{
			float reward = baseReward;
            
			// Add reward based on cargo requirements
			foreach (var item in template.requiredItems)
			{
				reward += item.weight * 2f;
				if (item.fragile) reward *= fragileRewardMultiplier;
			}
            
			if (template.isUrgent) reward *= urgentRewardMultiplier;
            
			return Mathf.RoundToInt(reward);
		}

		public void AcceptOrder(CargoOrder order)
		{
			if(order.orderStatus != OrderStatus.Available) return;
			order.orderStatus = OrderStatus.InProgress;
			order.acceptedTime = Time.time;
			
			// Generate cargo items for the player
			foreach (var requiredItem in order.requiredCargoItems)
			{
				GameObject cargoObject = GenerateCargoItem(requiredItem, order.physicalEntity);
				FragileCargo cargo = cargoObject.GetComponent<FragileCargo>();
				if (cargo != null)
				{
					cargo.AddItemToPlayer();
				}
			}
			
			
			OnOrderAccepted?.Invoke(order);
			Debug.Log($"Order accepted: {order.orderName}");
			
		}

		private GameObject GenerateCargoItem(RequiredCargoItem requiredItem, GameObject orderPrefab)
		{
			// Get cargo from pool
			GameObject cargoObject = CargoPool.GetCargoFromPool(orderPrefab);
			// Get FragileCargo component
			FragileCargo fragileComponent = cargoObject.GetComponent<FragileCargo>();
			if (fragileComponent != null)
			{
				
				fragileComponent.item = requiredItem.itemName;
				fragileComponent.weight = requiredItem.weight;
				fragileComponent.size = 1;
				fragileComponent.fragile = requiredItem.fragile;
        
				// Position cargo beside NPC with some random offset
				float randomDistance = UnityEngine.Random.Range(0f, 1f);
				Vector3 randomOffset = Vector3.right * randomDistance;
        
				Vector3 spawnPosition = transform.position + randomOffset;
				spawnPosition.y = transform.position.y + 1f; // Keep same height as NPC
        
				cargoObject.transform.position = spawnPosition;
				cargoObject.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

				// Reset cargo state
				fragileComponent.ResetFragileCargo();
			}
			return cargoObject;
		}
		
		
		#endregion
		
		
		
		// -------------------------------------------------------------------------------------------------------------
		//-->                                 PLAYER DETECTION & INTERACTION										 <--
		// -------------------------------------------------------------------------------------------------------------
		
		#region Player Detection & Interaction

		private void DetectPlayer()
		{
			Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, interactionRange, playerLayer);
            
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
				playerPorterSystem = foundPlayer.GetComponent<PorterSystem>();
				//ShowInteractionPrompt();
				//EnableHighlight();
			}
			else if (!playerFound && playerInRange)
			{
				playerInRange = false;
				playerTransform = null;
				playerPorterSystem = null;
				//HideInteractionPrompt();
				//DisableHighlight();
				//HideOrderUI();
			}
		}
		#endregion
		
		
		
		
		
		
		
		

		#endregion
		
		
		
		#region Gizmos
        
		private void OnDrawGizmosSelected()
		{
			Gizmos.color = playerInRange ? Color.green : Color.yellow;
			Gizmos.DrawWireSphere(transform.position, interactionRange);
		}
        
		#endregion
	}


	#region Data Classes/Enum
	
	[Serializable]
	public enum NPCType
	{
		OrderGiver,    // Gives orders to player
		OrderReceiver, // Receives completed orders
		Both          // Can both give and receive orders
	}
	
	[Serializable]
	public enum OrderType
	{
		Cargo,
		Materials,
		Weapon
	}
	
    
	[Serializable]
	public enum OrderStatus
	{
		Available,
		InProgress,
		Completed,
		Expired,
		Failed
	}
	
	
	[System.Serializable]
	public class CargoOrder
	{
		public string orderID;
		public string orderName;
		public string description;
		public GameObject physicalEntity;
		public List<RequiredCargoItem> requiredCargoItems;
		public string destinationNPC; // Name of NPC to deliver to
		public float timeLimit; // Time limit in seconds (0 = no limit)
		public bool isUrgent;
		public OrderStatus orderStatus;
		public int reward;
		public float creationTime;
		public float acceptedTime;
		public float completionTime;
	}
    
	[System.Serializable]
	public class RequiredCargoItem
	{
		public string itemName;
		public int quantity = 1;
		public float weight = 10f;
		public float size = 1f;
		public bool fragile = false;
	}
    
	[System.Serializable]
	public class OrderTemplate
	{
		public string orderName;
		public string description;
		public GameObject orderPrefab;
		public List<RequiredCargoItem> requiredItems;
		public string destinationNPC;
		public float timeLimit = 0f;
		public bool isUrgent = false;
	}

	#endregion


	#region Cargo Pool

	public static class CargoPool
	{

		public static int					poolSize = 10;
		private static Queue<GameObject>	cargoPool = new Queue<GameObject>();
		

		public static GameObject GetCargoFromPool(GameObject prefab)
		{
			GameObject cargo;
        
			if (cargoPool.Count > 0)
			{
				cargo = cargoPool.Dequeue();
				cargo.SetActive(true);
			}
			else
			{
				cargo = UnityEngine.Object.Instantiate(prefab);
			}

			return cargo;
		}

		public static void ReturnCargoToPool(GameObject cargo)
		{
			cargo.SetActive(false);
			cargoPool.Enqueue(cargo);
		}
	}

	#endregion
}
