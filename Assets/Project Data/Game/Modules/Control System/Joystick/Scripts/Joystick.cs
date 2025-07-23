using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FXnRXn
{
	public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
	{
		public enum JoystickType
		{
			Fixed,
			Dynamic
		}
		#region Variables
		public static Joystick Instance { get; private set; }
		
		[Header("--- Joystick Settings ---")]
		[SerializeField] protected JoystickType joystickType;
		[SerializeField] protected Image backgroundImage;
		[SerializeField] protected Image handleImage;
		
		[Space]
		[SerializeField] private Color backgroundActiveColor = Color.white;
		[SerializeField] private Color backgroundDisableColor = Color.white;

		[SerializeField] private Color handleActiveColor = Color.white;
		[SerializeField] private Color handleDisableColor = Color.white;

		[Space]
		[SerializeField] private float handleRange = 1;
		[SerializeField] private float deadZone = 0;
		
		[Header("--- Tutorial -- [Not Implemented] ---")]
		[SerializeField] private bool useTutorial;
		[SerializeField] private GameObject pointerGameObject;
		
		//------------------------------------------------------------------------------------------------------------->
		private RectTransform baseRectTransform;
		private RectTransform backgroundRectTransform;
		private RectTransform handleRectTransform;

		private bool isActive;
		public bool IsMovementInputNonZero => isActive;
		


		private bool canDrag;

		private Canvas canvas;
		private Camera canvasCamera;

		protected Vector2 input = Vector2.zero;
		public Vector3 Input => input;
		public Vector3 MovementInput => new Vector3(input.x, 0, input.y);

		private Vector2 defaultAnchoredPosition;
		
		private Animator joystickAnimator;
		private bool isTutorialDisplayed;
		private bool hideVisualsActive;
		// Events
		public event SimpleCallback OnMovementInputActivated;

		#endregion
		
		#region Methods
		
		private void Awake()
		{
			Instance = this;
			if (InputHandler.InputType == InputType.UIJoystick)
			{
				gameObject.SetActive(true);
			}
			else
			{
				gameObject.SetActive(false);
			}
			
			baseRectTransform = GetComponent<RectTransform>();
			backgroundRectTransform = backgroundImage.rectTransform;
			handleRectTransform = handleImage.rectTransform;
			defaultAnchoredPosition = backgroundRectTransform.anchoredPosition;
			//joystickAnimator = GetComponent<Animator>();
		}

		public void Initialise(Canvas canvas)
		{
			this.canvas = canvas;
			if (canvas == null)
			{
				Debug.LogError("Joystick must be placed inside a Canvas");
				enabled = false;
				return;
			}
			if(canvas.renderMode == RenderMode.ScreenSpaceCamera) canvasCamera = canvas.worldCamera;
			
			Vector2 center = new Vector2(0.5f, 0.5f);
			backgroundRectTransform.pivot = center;
			handleRectTransform.anchorMin = center;
			handleRectTransform.anchorMax = center;
			handleRectTransform.pivot = center;
			handleRectTransform.anchoredPosition = Vector2.zero;

			isActive = false;
			
			// TODO -> Turorial Animation
			
			// if (useTutorial)
			// {
			// 	joystickAnimator.enabled = true;
			// 	isTutorialDisplayed = true;
			// 	pointerGameObject.SetActive(true);
			// }
			// else
			// {
			// 	joystickAnimator.enabled = false;
			// 	isTutorialDisplayed = false;
			// 	pointerGameObject.SetActive(false);
			// }
			//---------------------------
			
			UpdateVisuals();
			
		}
		
		//--------------------------------------------------------------------------------------------------------------
		
		public void OnPointerDown(PointerEventData eventData)
		{
			// if (!useTutorial && isTutorialDisplayed)
			// {
			// 	isTutorialDisplayed = false;
			// 	joystickAnimator.enabled = false;
			// 	pointerGameObject.SetActive(false);
			// }
			switch (joystickType)
			{
				case JoystickType.Fixed:
					break;
				case JoystickType.Dynamic:
					backgroundRectTransform.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
					break;
			}
			isActive = true;
			OnMovementInputActivated?.Invoke();
			OnDrag(eventData);
			UpdateVisuals();
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (!isActive) return;

			Vector2 position = RectTransformUtility.WorldToScreenPoint(canvasCamera, backgroundRectTransform.position);
			Vector2 radius = backgroundRectTransform.sizeDelta / 2;
			input = (eventData.position - position) / (radius * canvas.scaleFactor);
			HandleInput(input.magnitude, input.normalized);
			handleRectTransform.anchoredPosition = input * radius * handleRange;
		}
		
		protected void HandleInput(float magnitude, Vector2 normalised)
		{
			input = magnitude > deadZone 
				? (magnitude > 1 ? normalised : input) 
				: Vector2.zero;
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			if (!isActive) return;
			isActive = false;
			ResetControl();
		}
		
		
		//--------------------------------------------------------------------------------------------------------------
		
		public void ResetControl()
		{
			input = Vector2.zero;
			handleRectTransform.anchoredPosition = Vector2.zero;
			backgroundRectTransform.anchoredPosition = defaultAnchoredPosition;
			UpdateVisuals();
		}
		
		private void UpdateVisuals()
		{
			backgroundImage.color = CoreExtensions.WithAlpha(
				isActive ? backgroundActiveColor : backgroundDisableColor,
				hideVisualsActive ? 0f : backgroundImage.color.a
			);

			handleImage.color = CoreExtensions.WithAlpha(
				isActive ? handleActiveColor : handleDisableColor,
				hideVisualsActive ? 0f : handleImage.color.a
			);
		}
		
		protected Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
		{
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    baseRectTransform, 
				    screenPosition, 
				    canvasCamera, 
				    out Vector2 localPoint))
			{
				Vector2 pivotOffset = baseRectTransform.pivot * baseRectTransform.sizeDelta;
				return localPoint - (backgroundRectTransform.anchorMax * baseRectTransform.sizeDelta) + pivotOffset;
			}
			return Vector2.zero;
		}

		public void HideVisuals()
		{
			hideVisualsActive = true;
			UpdateVisuals();
		}

		public void ShowVisuals()
		{
			hideVisualsActive = false;
			UpdateVisuals();
		}
		
		#endregion

		
	}
	
}


