using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using FXnRXn;

[RequireComponent(typeof(Camera))]
public class AdvancedDeathStrandingScanner : MonoBehaviour
{
    [Header("Scan Settings")]
    public float scanDuration = 4f;
    public float maxScanRadius = 50f;
    public float scanCooldown = 8f;
    public KeyCode scanKey = KeyCode.T;
    public LayerMask scanLayer = -1;

    [Header("Visual Settings")]
    public Gradient scanColorGradient;
    public float scanWidth = 3f;
    public float gridSize = 2f;
    public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float distortionStrength = 0.15f;
    public float normalDistortion = 0.3f;
    public float rippleFrequency = 8f;
    public float rippleAmplitude = 0.05f;

    [Header("Object Highlighting")]
    public float highlightIntensity = 5f;
    public float highlightDuration = 3f;
    public Color highlightColor = new Color(0.2f, 0.8f, 1f, 1f);
    public float pulseSpeed = 3f;

    [Header("Effects")]
    public ParticleSystem scanParticles;
    public AudioSource scanAudio;
    public Light scanLight;
    public float maxLightIntensity = 5f;
    public float lightRangeMultiplier = 1.5f;

    private Material scanMaterial;
    private float scanTimer;
    private float cooldownTimer;
    private bool isScanning;
    private bool isCoolingDown;
    private RenderTexture distortionRT;
    private Camera scanCamera;
    private List<Renderer> highlightedObjects = new List<Renderer>();
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Material highlightMaterial;
    private float currentScanRadius;
    private float currentIntensity;
    private float pulseTimer;

    void Start()
    {
        scanCamera = GetComponent<Camera>();
        
        // Create scan material
        scanMaterial = new Material(Shader.Find("Hidden/AdvancedDeathStrandingScan"));
        
        // Create distortion render texture
        distortionRT = new RenderTexture(Screen.width / 2, Screen.height / 2, 0, RenderTextureFormat.ARGBHalf);
        distortionRT.wrapMode = TextureWrapMode.Repeat;
        
        // Create highlight material
        highlightMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        highlightMaterial.SetColor("_BaseColor", highlightColor);
        highlightMaterial.EnableKeyword("_EMISSION");
        highlightMaterial.SetColor("_EmissionColor", highlightColor * highlightIntensity);
        highlightMaterial.SetFloat("_Smoothness", 0.9f);
        highlightMaterial.SetFloat("_Metallic", 0.8f);
        
        // Initialize shader properties
        Shader.SetGlobalFloat("_ScanIntensity", 0);
        
        HandleInput();
    }

    void Update()
    {
        
        UpdateScan();
        UpdateCooldown();
        UpdateHighlightedObjects();
    }

    void HandleInput()
    {
        InputHandler.Instance.onInteract += StartScan;
    }

    void StartScan()
    {
        if (isScanning || isCoolingDown) return;
        
        isScanning = true;
        scanTimer = 0f;
        currentScanRadius = 0f;
        currentIntensity = 0f;
        pulseTimer = 0f;
        
        // Play effects
        if (scanParticles) scanParticles.Play();
        if (scanAudio) scanAudio.Play();
        if (scanLight)
        {
            scanLight.enabled = true;
            scanLight.intensity = 0;
        }
    }

    void UpdateScan()
    {
        if (!isScanning) return;
        
        scanTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(scanTimer / scanDuration);
        
        // Calculate current scan radius with easing
        currentScanRadius = Mathf.Lerp(0, maxScanRadius, EaseOutQuad(progress));
        
        // Calculate intensity with curve
        currentIntensity = intensityCurve.Evaluate(progress);
        
        // Update shader properties
        Shader.SetGlobalVector("_ScanOrigin", transform.position);
        Shader.SetGlobalFloat("_ScanRadius", currentScanRadius);
        Shader.SetGlobalFloat("_ScanIntensity", currentIntensity);
        Shader.SetGlobalFloat("_DistortionStrength", distortionStrength * currentIntensity);
        Shader.SetGlobalFloat("_NormalDistortion", normalDistortion * currentIntensity);
        Shader.SetGlobalFloat("_RippleFrequency", rippleFrequency);
        Shader.SetGlobalFloat("_RippleAmplitude", rippleAmplitude * currentIntensity);
        
        // Update scan material properties
        Color currentColor = scanColorGradient.Evaluate(progress);
        scanMaterial.SetColor("_ScanColor", currentColor);
        scanMaterial.SetFloat("_ScanWidth", scanWidth);
        scanMaterial.SetFloat("_GridSize", gridSize);
        
        // Update light
        if (scanLight)
        {
            scanLight.intensity = maxLightIntensity * currentIntensity;
            scanLight.range = currentScanRadius * lightRangeMultiplier;
            scanLight.color = currentColor;
        }
        
        // Detect objects in scan radius
        if (progress > 0.3f && progress < 0.9f)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, currentScanRadius, scanLayer);
            foreach (var collider in hitColliders)
            {
                HighlightObject(collider.GetComponent<Renderer>());
            }
        }
        
        // End scan
        if (scanTimer >= scanDuration)
        {
            EndScan();
        }
    }

    void EndScan()
    {
        isScanning = false;
        isCoolingDown = true;
        cooldownTimer = 0f;
        
        // Reset shader intensity
        Shader.SetGlobalFloat("_ScanIntensity", 0);
        
        // Stop effects
        if (scanLight) scanLight.enabled = false;
        if (scanParticles) scanParticles.Stop();
    }

    void UpdateCooldown()
    {
        if (!isCoolingDown) return;
        
        cooldownTimer += Time.deltaTime;
        if (cooldownTimer >= scanCooldown)
        {
            isCoolingDown = false;
        }
    }

    void UpdateHighlightedObjects()
    {
        pulseTimer += Time.deltaTime * pulseSpeed;
        float pulseValue = (Mathf.Sin(pulseTimer) + 1f) * 0.5f;
        
        foreach (var renderer in highlightedObjects)
        {
            if (renderer != null)
            {
                highlightMaterial.SetColor("_EmissionColor", highlightColor * highlightIntensity * pulseValue);
            }
        }
    }

    void HighlightObject(Renderer renderer)
    {
        if (renderer == null || highlightedObjects.Contains(renderer)) return;
        
        // Save original materials
        if (!originalMaterials.ContainsKey(renderer))
        {
            originalMaterials[renderer] = renderer.materials;
        }
        
        // Apply highlight material
        Material[] highlightMats = new Material[renderer.materials.Length];
        for (int i = 0; i < highlightMats.Length; i++)
        {
            highlightMats[i] = highlightMaterial;
        }
        renderer.materials = highlightMats;
        
        highlightedObjects.Add(renderer);
        
        // Schedule highlight removal
        StartCoroutine(RemoveHighlightAfterDelay(renderer, highlightDuration));
    }

    System.Collections.IEnumerator RemoveHighlightAfterDelay(Renderer renderer, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (renderer != null && originalMaterials.ContainsKey(renderer))
        {
            renderer.materials = originalMaterials[renderer];
        }
        
        highlightedObjects.Remove(renderer);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (isScanning)
        {
            // Render distortion pass
            Graphics.Blit(source, distortionRT, scanMaterial, 0);
            
            // Set distortion texture for main pass
            scanMaterial.SetTexture("_DistortionTex", distortionRT);
            
            // Render main scan effect
            Graphics.Blit(source, destination, scanMaterial, 1);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }

    void OnDestroy()
    {
        if (distortionRT != null) distortionRT.Release();
    }

    float EaseOutQuad(float t)
    {
        return t * (2 - t);
    }

    void OnDrawGizmosSelected()
    {
        if (isScanning)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, currentScanRadius);
        }
    }
}