using System;
using TriInspector;
using UnityEngine;
using UnityEngine.UI;

namespace FXnRXn
{
	public class UIGame : MonoBehaviour
	{
		public static UIGame Instance { get; private set; }
		#region Properties
		
		[Title("Buttons")]
		[Space(10)]
		[Required]
		[SerializeField] private Button							interactButton;
		[Required]
		[SerializeField] private Button							scanButton;
		[Required]
		[SerializeField] private Button							shootButton;
		
		
		[Title("Components")]
		[Space(10)]
		[Required]
		[SerializeField] private Joystick						joystick;
		
		[Title("Stats")]
		[Space(10)]
		[Required]
		[SerializeField] private Slider							blancedSlider;
		
		
		
		
		
		
		public Action<float> OnBlancedSliderChanged;
		
		
		public Joystick Joystick => joystick;
		
		protected Canvas canvas;
		public Canvas Canvas => canvas;
		#endregion
		
		
		#region Methods

		private void Awake()
		{
			if(Instance == null) Instance = this;
			
			canvas = GetComponent<Canvas>();
			if(FindFirstObjectByType<Joystick>() != null) joystick = FindFirstObjectByType<Joystick>();
			
		}

		private void Start()
		{
			if(joystick != null) joystick.Initialise(canvas);
			
			if (interactButton != null)
			{
				interactButton.onClick.RemoveAllListeners();
				interactButton.onClick.AddListener(() =>
				{
					InputHandler.Instance.onInteract?.Invoke();
				});
			}
			
			if (scanButton != null)
			{
				scanButton.onClick.RemoveAllListeners();
				scanButton.onClick.AddListener(() =>
				{
					InputHandler.Instance.onScan?.Invoke();
				});
			}
			
			if (shootButton != null)
			{
				shootButton.onClick.RemoveAllListeners();
				shootButton.onClick.AddListener(() =>
				{
					
				});
			}
			
			
			
			OnBlancedSliderChanged += SetBlancedSliderUI;
			
		}


		private void OnDisable()
		{
			OnBlancedSliderChanged -= SetBlancedSliderUI;
		}


		public void SetBlancedSliderUI(float value)
		{
			if(blancedSlider == null) return;
			
			blancedSlider.value = value;
		}

		#endregion
		
		
		
		
	}
}
