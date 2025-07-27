using UnityEngine;

namespace FXnRXn
{
	[System.Serializable]
	public class OutlineSettings
	{
		[ColorUsage(true, true)]
		public Color outlineColor = new Color(1f, 0.5f, 0f, 1f);
		[Range(0f, 0.1f)]
		public float outlineWidth = 0.03f;
		[Range(0f, 2f)]
		public float outlineIntensity = 1.0f;
		[Range(0f, 10f)]
		public float pulseSpeed = 2f;
		[Range(0f, 1f)]
		public float pulseAmount = 0.3f;
	}

	public class FragileCargoOutline : MonoBehaviour
	{
		
		#region Properties
		[Header("--- Outline Settings ---")]
		public OutlineSettings outlineSettings = new OutlineSettings();
		
		
		private FragileCargo fragileCargo;
		public Material outlineMaterial;
		private Renderer renderer;
		private Material originalMaterial;
		private bool isHighlighted = false;
        
		// Property IDs for performance
		private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
		private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");
		private static readonly int OutlineIntensityID = Shader.PropertyToID("_OutlineIntensity");
		private static readonly int PulseSpeedID = Shader.PropertyToID("_PulseSpeed");
		private static readonly int PulseAmountID = Shader.PropertyToID("_PulseAmount");

		#endregion
		
		#region Unity Callbacks
		

		public void InitializeComponents(FragileCargo _fragileCargo)
		{
			fragileCargo = _fragileCargo;
			// Get all renderers in this object and children
			renderer = GetComponent<Renderer>();
			if (renderer == null) return;
			originalMaterial = renderer.material;
			outlineMaterial = renderer.material;
			ResetOutlineMaterial();
		}

		#endregion
		
		
		#region Methods
		public void EnableOutline()
		{
			if (isHighlighted) return;

			if (fragileCargo != null && fragileCargo.GetCargoState() == FragileCargo.CargoState.Grounded)
			{
				UpdateOutlineMaterial();
			}
            
			isHighlighted = true;
		}

		public void DisableOutline()
		{
			if (!isHighlighted) return;

			ResetOutlineMaterial();
			
			isHighlighted = false;
		}
		
		private void UpdateOutlineMaterial()
		{
			if (outlineMaterial == null) return;
            
			outlineMaterial.SetColor(OutlineColorID, outlineSettings.outlineColor);
			outlineMaterial.SetFloat(OutlineWidthID, outlineSettings.outlineWidth);
			outlineMaterial.SetFloat(OutlineIntensityID, outlineSettings.outlineIntensity);
			outlineMaterial.SetFloat(PulseSpeedID, outlineSettings.pulseSpeed);
			outlineMaterial.SetFloat(PulseAmountID, outlineSettings.pulseAmount);
		}
		
		public void ResetOutlineMaterial()
		{
			if (outlineMaterial == null) return;
            
			outlineMaterial.SetColor(OutlineColorID, outlineSettings.outlineColor);
			outlineMaterial.SetFloat(OutlineWidthID, 0f);
			outlineMaterial.SetFloat(OutlineIntensityID, 0f);
			outlineMaterial.SetFloat(PulseSpeedID, 0f);
			outlineMaterial.SetFloat(PulseAmountID, 0f);
		}

		#endregion
		
		
		#region Helper Methods
		
		public void SetOutlineColor(Color color)
		{
			outlineSettings.outlineColor = color;
			if (isHighlighted)
			{
				outlineMaterial.SetColor(OutlineColorID, color);
			}
		}

		public void SetOutlineWidth(float width)
		{
			outlineSettings.outlineWidth = width;
			if (isHighlighted)
			{
				outlineMaterial.SetFloat(OutlineWidthID, width);
			}
		}
		
		public bool IsHighlighted => isHighlighted;
		
		#endregion

	}
}
