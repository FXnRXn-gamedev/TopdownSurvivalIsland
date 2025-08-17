#pragma warning disable 0067
#pragma warning disable 0414


using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TriInspector;

namespace FXnRXn
{
	[System.Serializable]
	public class ObjectInfo
	{
		public string objectName = "Unknown Object";
		public string description = "No description available";
		public Sprite icon;
		public Color highlightColor = Color.yellow;
		public ObjectCategory category = ObjectCategory.Resource;
		public int value = 0;
		public bool isRare = false;
	}

	public enum ObjectCategory
	{
		Resource,
		Equipment,
		Weapon,
		Vehicle,
		Structure,
		Enemy,
		NPC,
		Collectible
	}
	public class ScannableObject : MonoBehaviour
	{
		#region Properties
		
		[Title("Object Information")]
		public ObjectInfo						objectInfo;
    
		[Title("Scan Settings")]
		public float							scanRadius = 50f;
		public bool								canBeScanned = true;
		public LayerMask						scanLayer = -1;
    
		[Title("Highlight Settings")]
		public Renderer[]						renderersToHighlight;
		public Material							highlightMaterial;
    
		[Title("UI Settings")]
		[Space(10)]
		public Vector3							uiOffset = new Vector3(0, 2, 0);
		[Required]
		public GameObject						canvasObj;
		[Required]
		public GameObject						panel;
		[Required]
		public Image							panelImage;
    
		// Private variables
		private bool isHighlighted = false;
		private bool isScanned = false;
		private Material[] originalMaterials;
		private GameObject highlightUI;
		private Canvas worldCanvas;
		private RectTransform uiRect;
		private TextMeshProUGUI nameText;
		private TextMeshProUGUI descriptionText;
		private Image iconImage;
		private CanvasGroup canvasGroup;
    
		// Static references for UI management
		private static Camera playerCamera;
		private static Transform playerTransform;
		
		
		#endregion
		
		#region Unity Callbacks
		
		void Start()
		{
			// Cache original materials
			if (renderersToHighlight != null && renderersToHighlight.Length > 0)
			{
				originalMaterials = new Material[renderersToHighlight.Length];
				for (int i = 0; i < renderersToHighlight.Length; i++)
				{
					if (renderersToHighlight[i] != null)
					{
						originalMaterials[i] = renderersToHighlight[i].material;
					}
				}
			}
        
			// Find player camera if not set
			if (playerCamera == null)
			{
				playerCamera = CameraController.Instance?.GetMainCamera;
			}
        
			CreateHighlightUI();
		}
		
		void Update()
		{
			// Make UI face camera
			if (isHighlighted && worldCanvas != null && playerCamera != null)
			{
				worldCanvas.transform.LookAt(worldCanvas.transform.position + playerCamera.transform.rotation * Vector3.forward,
					playerCamera.transform.rotation * Vector3.up);
			}
		}
		
		#endregion
		
		#region Methods

		void CreateHighlightUI()
		{
	        // Create world space canvas for object info
	        if (canvasObj != null)
	        {
		        canvasObj.name = $"ScanUI_{objectInfo.objectName}";
	        }
	        else
	        { 
		        canvasObj = new GameObject($"ScanUI_{objectInfo.objectName}");
	        }
	        canvasObj.transform.SetParent(transform);
	        canvasObj.transform.localPosition = uiOffset;
	        
	        worldCanvas = canvasObj.GetComponent<Canvas>();
	        worldCanvas.renderMode = RenderMode.WorldSpace;
	        worldCanvas.worldCamera = playerCamera;
        
	        var canvasScaler = canvasObj.GetComponent<CanvasScaler>();
	        canvasScaler.dynamicPixelsPerUnit = 10;
        
	        canvasGroup = canvasObj.GetComponent<CanvasGroup>();
	        canvasGroup.alpha = 0;
	        canvasGroup.interactable = false;
	        canvasGroup.blocksRaycasts = false;
        
        // Create UI panel
        if (panel == null)
        {
	        panel = new GameObject("InfoPanel");
        }
        panel.transform.SetParent(canvasObj.transform);
        
        uiRect = panel.GetComponent<RectTransform>();
        uiRect.sizeDelta = new Vector2(300, 120);
        uiRect.localPosition = Vector3.zero;
        uiRect.localScale = Vector3.one * 1f; // Scale down for world space
        
        panelImage.color = new Color(0, 0, 0, 0f);
        panelImage.raycastTarget = false;
        
        // // Create icon
        // if (objectInfo.icon != null)
        // {
        //     GameObject iconObj = new GameObject("Icon");
        //     iconObj.transform.SetParent(panel.transform);
        //     
        //     var iconRect = iconObj.AddComponent<RectTransform>();
        //     iconRect.sizeDelta = new Vector2(60, 60);
        //     iconRect.anchoredPosition = new Vector2(-100, 20);
        //     
        //     iconImage = iconObj.AddComponent<Image>();
        //     iconImage.sprite = objectInfo.icon;
        //     iconImage.raycastTarget = false;
        // }
        //
        // // Create name text
        // GameObject nameObj = new GameObject("NameText");
        // nameObj.transform.SetParent(panel.transform);
        //
        // var nameRect = nameObj.AddComponent<RectTransform>();
        // nameRect.sizeDelta = new Vector2(200, 30);
        // nameRect.anchoredPosition = new Vector2(20, 30);
        //
        // nameText = nameObj.AddComponent<TextMeshProUGUI>();
        // nameText.text = objectInfo.objectName;
        // nameText.fontSize = 18;
        // nameText.color = objectInfo.highlightColor;
        // nameText.fontStyle = FontStyles.Bold;
        // nameText.raycastTarget = false;
        //
        // // Create description text
        // GameObject descObj = new GameObject("DescriptionText");
        // descObj.transform.SetParent(panel.transform);
        //
        // var descRect = descObj.AddComponent<RectTransform>();
        // descRect.sizeDelta = new Vector2(200, 40);
        // descRect.anchoredPosition = new Vector2(20, -10);
        //
        // descriptionText = descObj.AddComponent<TextMeshProUGUI>();
        // descriptionText.text = objectInfo.description;
        // descriptionText.fontSize = 12;
        // descriptionText.color = Color.white;
        // descriptionText.raycastTarget = false;
        //
        // // Create category/value text if applicable
        // if (objectInfo.value > 0)
        // {
        //     GameObject valueObj = new GameObject("ValueText");
        //     valueObj.transform.SetParent(panel.transform);
        //     
        //     var valueRect = valueObj.AddComponent<RectTransform>();
        //     valueRect.sizeDelta = new Vector2(200, 20);
        //     valueRect.anchoredPosition = new Vector2(20, -35);
        //     
        //     var valueText = valueObj.AddComponent<TextMeshProUGUI>();
        //     valueText.text = $"Value: {objectInfo.value}";
        //     valueText.fontSize = 10;
        //     valueText.color = Color.cyan;
        //     valueText.raycastTarget = false;
        // }
        
        highlightUI = canvasObj;
        highlightUI.SetActive(false);
		}
		
		
		public void OnScanned()
		{
			if (!canBeScanned || isScanned) return;
        
			isScanned = true;
			StartHighlight();
        
			// Add to ScanFeature's detected objects list
			ScanFeatureURP.RegisterScannedObject(this);
		}
		
		
		public void StartHighlight()
		{
			if (isHighlighted) return;
        
			isHighlighted = true;
        
			// Apply highlight materials
			if (highlightMaterial != null && renderersToHighlight != null)
			{
				for (int i = 0; i < renderersToHighlight.Length; i++)
				{
					if (renderersToHighlight[i] != null)
					{
						renderersToHighlight[i].material = highlightMaterial;
						// Set highlight color
						renderersToHighlight[i].material.SetColor("_Color", objectInfo.highlightColor);
						renderersToHighlight[i].material.SetColor("_EmissionColor", objectInfo.highlightColor * 0.5f);
					}
				}
			}
        
			// Show UI with animation
			if (highlightUI != null)
			{
				highlightUI.SetActive(true);
				canvasGroup.alpha = 0;
				uiRect.localScale = Vector3.one * 0.005f;
            
				var sequence = DOTween.Sequence();
				sequence.Append(canvasGroup.DOFade(1f, 0.5f));
				sequence.Join(uiRect.DOScale(Vector3.one * 0.01f, 0.5f).SetEase(Ease.OutBack));
			}
		}
		
		
		public void StopHighlight()
		{
			if (!isHighlighted) return;
        
			isHighlighted = false;
        
			// Restore original materials
			if (originalMaterials != null && renderersToHighlight != null)
			{
				for (int i = 0; i < renderersToHighlight.Length; i++)
				{
					if (renderersToHighlight[i] != null && i < originalMaterials.Length)
					{
						renderersToHighlight[i].material = originalMaterials[i];
					}
				}
			}
        
			// Hide UI with animation
			if (highlightUI != null && canvasGroup != null)
			{
				var sequence = DOTween.Sequence();
				sequence.Append(canvasGroup.DOFade(0f, 0.3f));
				sequence.Join(uiRect.DOScale(Vector3.one * 0.005f, 0.3f).SetEase(Ease.InBack));
				sequence.OnComplete(() => highlightUI.SetActive(false));
			}
		}
		
		
    
		public static void SetPlayerReferences(Camera camera, Transform player)
		{
			playerCamera = camera;
			playerTransform = player;
		}
		
		#endregion
		
		//--------------------------------------------------------------------------------------------------------------

		void OnDrawGizmosSelected()
		{
			// Draw scan radius
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, scanRadius);
		}
		
		#region Helper
		
		
		#endregion
	}
}

