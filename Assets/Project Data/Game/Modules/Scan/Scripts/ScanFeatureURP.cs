using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using TriInspector;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;


namespace FXnRXn
{
	public class ScanFeatureURP : ScriptableRendererFeature
	{
		#region Properties
		public Settings settings = new Settings();
		static ScanFeatureURP _instance;
		CustomRenderPass _myPass;
		
		// ///
		static List<ScannableObject> detectedObjects = new List<ScannableObject>();
		static List<ScannableObject> highlightedObjects = new List<ScannableObject>();
		static float objectHighlightDuration = 10f; // How long objects stay highlighted
		// ///
		
		

		readonly static int ScanColorHead = Shader.PropertyToID( "scanColorHead" );
		readonly static int ScanColor = Shader.PropertyToID( "scanColor" );

		readonly static int OutlineWidth = Shader.PropertyToID( "outlineWidth" );
		readonly static int OutlineBrightness = Shader.PropertyToID( "outlineBrightness" );
		readonly static int OutlineStarDistance = Shader.PropertyToID( "outlineStarDistance" );

		readonly static int ScanLineWidth = Shader.PropertyToID( "scanLineWidth" );
		readonly static int ScanLineInterval = Shader.PropertyToID( "scanLineInterval" );
		readonly static int ScanLineBrightness = Shader.PropertyToID( "scanLineBrightness" );
		readonly static int ScanRange = Shader.PropertyToID( "scanRange" );

		readonly static int HeadScanLineDistance = Shader.PropertyToID( "headScanLineDistance" );
		readonly static int HeadScanLineWidth = Shader.PropertyToID( "headScanLineWidth" );
		readonly static int HeadScanLineBrightness = Shader.PropertyToID( "headScanLineBrightness" );
		readonly static int ScanCenterWs = Shader.PropertyToID( "scanCenterWS" );

		// 地形标记的参数
		readonly static int ColorAlpha = Shader.PropertyToID( "colorAlpha" );


		static bool canScan = true;
		static bool showMark = false;
		static Tween markTween;
		
		#endregion
		
		
		#region Methods
		
		// Add this method to register scanned objects
		public static void RegisterScannedObject(ScannableObject scannableObject)
		{
			if (!detectedObjects.Contains(scannableObject))
			{
				detectedObjects.Add(scannableObject);
			}
		}
		
		public static void ExecuteScan( Transform player ) 
		{
			//StartScan( player ).Forget();
			StartScanWithObjects(player).Forget();
		}
		
		// // Replace your existing StartScan method with this enhanced version
	    static async UniTaskVoid StartScanWithObjects(Transform player) 
	    {
	        if (!canScan) 
	        {
	            return;
	        }
	        canScan = false;
	        showMark = true;

	        // Clear previously highlighted objects
	        foreach (var obj in highlightedObjects)
	        {
	            if (obj != null)
	            {
	                obj.StopHighlight();
	            }
	        }
	        highlightedObjects.Clear();

	        // Detect scannable objects in range
	        DetectScannableObjects(player);

	        //万一上一个mark还没消失，手动取消
	        markTween?.Kill();
	        var scanCenter = player.position - player.forward * 2;

	        var material = _instance.settings.scanMaterial;
	        var markMaterial = _instance.settings.markMaterial;
	        material.SetVector(ScanCenterWs, scanCenter);

	        // 控制扫描线前进
	        material.SetFloat(HeadScanLineDistance, 4);
	        material.DOFloat(250, HeadScanLineDistance, 3.5f).SetEase(Ease.InSine).onComplete += () => {
	            canScan = true;
	        };

	        // 随着距离前进，扫描范围变大
	        material.SetFloat(ScanRange, 1);
	        material.DOFloat(5, ScanRange, 1.5f).SetEase(Ease.InSine).SetDelay(1);

	        // 控制扫描线和最前方的扫描线颜色颜色
	        material.SetFloat(ScanLineBrightness, 0.3f);
	        material.SetFloat(HeadScanLineBrightness, 0);
	        material.DOFloat(1, ScanLineBrightness, 0.2f).SetDelay(0.25f);
	        material.DOFloat(1, HeadScanLineBrightness, 0.1f).SetDelay(0.25f);
	        material.DOFloat(0, ScanLineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);
	        material.DOFloat(0, HeadScanLineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);

	        // 控制轮廓
	        material.SetFloat(OutlineBrightness, 1);
	        material.SetFloat(OutlineStarDistance, 0);
	        material.DOFloat(0, OutlineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);
	        material.DOFloat(30, OutlineStarDistance, 1f).SetEase(Ease.InCubic);

	        // 控制地形标记的透明度
	        markMaterial.SetFloat(ColorAlpha, 0);
	        markMaterial.DOFloat(1, ColorAlpha, 1f);
	        markTween = markMaterial.DOFloat(0, ColorAlpha, 1f).SetDelay(7);
	        markTween.onComplete += () => {
	            showMark = false;
	        };

	        // Start object highlighting with a slight delay for dramatic effect
	        HighlightDetectedObjects().Forget();

	        //生成地形标记
	        await GenerateTerrainMarks(player);
	    }

		static async UniTaskVoid StartScan( Transform player ) 
		{
			if( !canScan ) {
				return;
			}
			
			canScan = false;
			showMark = true;
			
			markTween?.Kill();
			var scanCenter = player.position - player.forward * 2;

			var material = _instance.settings.scanMaterial;
			var markMaterial = _instance.settings.markMaterial;
			material.SetVector( ScanCenterWs, scanCenter );
			
			
			material.SetFloat( HeadScanLineDistance, 4 );
			material.DOFloat( 250, HeadScanLineDistance, 3.5f ).SetEase( Ease.InSine ).onComplete += () => {
				canScan = true;
			};

			
			material.SetFloat( ScanRange, 1 );
			material.DOFloat( 5, ScanRange, 1.5f ).SetEase( Ease.InSine ).SetDelay( 1 );
			
			material.SetFloat( ScanLineBrightness, 0.3f );
			material.SetFloat( HeadScanLineBrightness, 0 );
			material.DOFloat( 1, ScanLineBrightness, 0.2f ).SetDelay( 0.25f );
			material.DOFloat( 1, HeadScanLineBrightness, 0.1f ).SetDelay( 0.25f );
			material.DOFloat( 0, ScanLineBrightness, 0.5f ).SetDelay( 2.25f ).SetEase( Ease.Linear );
			material.DOFloat( 0, HeadScanLineBrightness, 0.5f ).SetDelay( 2.25f ).SetEase( Ease.Linear );

			
			material.SetFloat( OutlineBrightness, 1 );
			material.SetFloat( OutlineStarDistance, 0 );
			material.DOFloat( 0, OutlineBrightness, 0.5f ).SetDelay( 2.25f ).SetEase( Ease.Linear );
			material.DOFloat( 30, OutlineStarDistance, 1f ).SetEase( Ease.InCubic );

			
			markMaterial.SetFloat( ColorAlpha, 0 );
			markMaterial.DOFloat( 1, ColorAlpha, 1f );
			markTween = markMaterial.DOFloat( 0, ColorAlpha, 1f ).SetDelay( 7 );
			markTween.onComplete += () => {
				showMark = false;
			};

			
			await GenerateTerrainMarks( player );
		}
		
		static ProfilerMarker _generateTerrainMarks = new ProfilerMarker( "GenerateTerrainMarks" );
		struct Marks {
			public Vector3 markPosition;
			public int markCategory;
		}
		static Marks[] _marks; 
		const int horizentalCount = 70; 
		const int verticalCount = 50; 
		const float gridStep = 0.5f; 
		
		static void ShootParticle( Vector3 position, Vector3 normal, int index = 3 ) {
		float distanceToCamera01 = Vector3.Distance( position, Camera.main.transform.position ) / 20 + 0.5f;

		GameObject instance;
		switch( index ) {
			case 3:
				instance = Instantiate( _instance.settings.markParticle3 );
				break;
			case 2:
				instance = Instantiate( _instance.settings.markParticle2 );
				break;
			default:
				instance = Instantiate( _instance.settings.markParticle1 );
				break;
		}
		instance.transform.position = position;
		instance.transform.localScale = Random.Range( 0.5f, 1.5f ) * Vector3.one * distanceToCamera01;
		instance.transform.GetChild( 0 ).localScale = Random.Range( 2f, 5f ) * Vector3.one * distanceToCamera01;
	}
		
		
		// // Add this new method for detecting scannable objects
		static void DetectScannableObjects(Transform player)
		{
			// Find all scannable objects in a large radius
			ScannableObject[] allScannableObjects = FindObjectsOfType<ScannableObject>();
        
			foreach (var scannableObj in allScannableObjects)
			{
				if (scannableObj == null || !scannableObj.canBeScanned) continue;
            
				float distance = Vector3.Distance(player.position, scannableObj.transform.position);
            
				// Check if object is within scan range
				if (distance <= scannableObj.scanRadius)
				{
					// Additional raycast check to see if object is not completely obscured
					Vector3 directionToObject = (scannableObj.transform.position - player.position).normalized;
					if (Physics.Raycast(player.position, directionToObject, out RaycastHit hit, distance))
					{
						// If we hit the scannable object or something close to it, it's detectable
						if (hit.collider.GetComponent<ScannableObject>() == scannableObj || 
						    Vector3.Distance(hit.point, scannableObj.transform.position) < 2f)
						{
							scannableObj.OnScanned();
							highlightedObjects.Add(scannableObj);
						}
					}
				}
			}
		}
		
		
		// // Add this method to handle object highlighting timing
		static async UniTaskVoid HighlightDetectedObjects()
		{
			// Wait for scan animation to reach objects
			await UniTask.Delay(System.TimeSpan.FromSeconds(1.5f));
        
			// Highlight objects with staggered timing for dramatic effect
			for (int i = 0; i < highlightedObjects.Count; i++)
			{
				if (highlightedObjects[i] != null)
				{
					highlightedObjects[i].StartHighlight();
                
					// Small delay between each object highlight
					if (i < highlightedObjects.Count - 1)
					{
						await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f));
					}
				}
			}
        
			// Auto-hide highlights after duration
			await UniTask.Delay(System.TimeSpan.FromSeconds(objectHighlightDuration));
        
			foreach (var obj in highlightedObjects)
			{
				if (obj != null)
				{
					obj.StopHighlight();
				}
			}
		}
		
		// // Add this method to manually clear all highlights (useful for UI/menu systems)
		public static void ClearAllHighlights()
		{
			foreach (var obj in highlightedObjects)
			{
				if (obj != null)
				{
					obj.StopHighlight();
				}
			}
			highlightedObjects.Clear();
		}
		
		// // Add this to get currently highlighted objects (useful for interaction systems)
		public static List<ScannableObject> GetHighlightedObjects()
		{
			return new List<ScannableObject>(highlightedObjects);
		}

		static async UniTask GenerateTerrainMarks( Transform player )
		{
			
			Array.Clear( _marks, 0, _marks.Length );
			var forward = player.forward;
			var right = player.right;


			
			Vector3 position = player.position - forward * 2 + Vector3.up * 100;
			var rayCastPos = position - right * horizentalCount / 2 * gridStep - forward * ( 3 * gridStep );

			
			for( int i = 0; i < verticalCount; i++ ) {
				_generateTerrainMarks.Begin();
				for( int j = 0; j < horizentalCount; j++ ) {
					Physics.Raycast( rayCastPos, Vector3.down, out RaycastHit hit, 300, LayerMask.GetMask( "Scan", "Road", "Ground" ) );
					if( hit.collider is null ) {
						rayCastPos += right * gridStep;
						continue;
					}
					var normal = hit.normal;

					
					if( hit.collider.isTrigger ) {
						Physics.Raycast( rayCastPos, Vector3.down, out hit, 300, LayerMask.GetMask( "Scan", "Ground" ) );
						_marks[i * horizentalCount + j].markCategory = 0;
						_marks[i * horizentalCount + j].markPosition = hit.point;
					} else if( normal.y < 0.75f ) {
						_marks[i * horizentalCount + j].markCategory = 3;
						
						if( Random.Range( 0f, 1f ) < 0.3f ) {
							_marks[i * horizentalCount + j].markPosition = hit.point;
							ShootParticle( hit.point, normal, 3 );
						}
					} else if( normal.y < 0.85f ) {
						_marks[i * horizentalCount + j].markCategory = 2;
						_marks[i * horizentalCount + j].markPosition = hit.point;
						if( Random.Range( 0f, 1f ) < 0.0003 ) {
							ShootParticle( hit.point, normal, 1 );
						}
					} else {
						_marks[i * horizentalCount + j].markCategory = 1;
						_marks[i * horizentalCount + j].markPosition = hit.point;
						if( Random.Range( 0f, 1f ) < 0.0002 ) {
							ShootParticle( hit.point, normal, 1 );
						}
					}

					rayCastPos += right * gridStep;

					// debug 显示绘制
					// if( hit.normal.y < 0.8f ) {
					// 	Debug.DrawLine( hit.point, hit.point + hit.normal * 0.2f, Color.red, 10 );
					// } else if( hit.normal.y < 0.9f ) {
					// 	Debug.DrawLine( hit.point, hit.point + hit.normal * 0.2f, Color.yellow, 10 );
					// } else {
					// 	Debug.DrawLine( hit.point, hit.point + hit.normal * 0.2f, Color.cyan, 10 );
					// }
				}
				_generateTerrainMarks.End();

				rayCastPos -= right * horizentalCount * gridStep;
				rayCastPos += forward * gridStep;
				
				//每次生成一行地形标记后，等待一帧，并绘制当前帧的地形标记
				await UniTask.Yield();

			
			}
		}

	
		public override void Create() 
		{
			if( settings.scanMaterial == null ) return;
			if( !Application.isPlaying ) return;
			
			_marks = new Marks[horizentalCount * verticalCount];
			_myPass = new CustomRenderPass( settings );
			_instance = this;

		}

		public override void SetupRenderPasses( ScriptableRenderer renderer, in RenderingData renderingData ) 
		{
			if( settings.scanMaterial == null ) return;
			if( !Application.isPlaying ) return;

			if( renderingData.cameraData.cameraType == CameraType.Game ) 
			{
				_myPass.renderPassEvent = settings.renderEvent;
				_myPass.ConfigureInput( ScriptableRenderPassInput.Color );
				_myPass.ConfigureInput( ScriptableRenderPassInput.Normal );
				_myPass.ConfigureInput( ScriptableRenderPassInput.Depth );
			}
		}

	
		public override void AddRenderPasses( ScriptableRenderer renderer, ref RenderingData renderingData ) 
		{
			if( settings.scanMaterial == null ) return;
			if( !Application.isPlaying ) return;
			
			renderer.EnqueuePass( _myPass );
		}
		
		class CustomRenderPass : ScriptableRenderPass 
		{
			
		RTHandle _cameraColor;
		RTHandle _cameraDepth;
		RTHandle _cameraNormal;
		RTHandle _tempTex;
		//纹理描述器
		RenderTextureDescriptor m_Descriptor;
		//cmd name
		string _passName;
		Settings settings;

		GraphicsBuffer _graphicsBuffer;
		GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;
		ComputeBuffer _computeBuffer;
		//初始类的时候传入材质

		Mesh mesh;
		public CustomRenderPass( Settings settings ) {
			_graphicsBuffer = new GraphicsBuffer( GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size );
			_commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
			_computeBuffer = new ComputeBuffer( horizentalCount * verticalCount, sizeof( float ) * 4 );

			mesh = new Mesh{
				vertices = new Vector3[6],
				uv = new[]{
					new Vector2( 0, 0 ),
					new Vector2( 1, 1 ),
					new Vector2( 0, 1 ),
					new Vector2( 0, 0 ),
					new Vector2( 1, 0 ),
					new Vector2( 1, 1 ),
				}
			};

			var scanMaterial = settings.scanMaterial;

			scanMaterial.SetColor( ScanColorHead, settings.scanColorHead );
			scanMaterial.SetColor( ScanColor, settings.scanColor );
			scanMaterial.SetFloat( OutlineWidth, settings.outlineWidth );
			scanMaterial.SetFloat( OutlineBrightness, settings.outlineBrightness );
			scanMaterial.SetFloat( OutlineStarDistance, settings.outlineStarDistance );

			scanMaterial.SetFloat( ScanLineWidth, settings.scanLineWidth );
			scanMaterial.SetFloat( ScanLineInterval, settings.scanLineInterval );
			scanMaterial.SetFloat( ScanLineBrightness, settings.scanLineBrightness );
			scanMaterial.SetFloat( ScanRange, settings.scanRange );

			scanMaterial.SetFloat( HeadScanLineDistance, settings.headScanLineDistance );
			scanMaterial.SetFloat( HeadScanLineWidth, settings.headScanLineWidth );

			scanMaterial.SetVector( ScanCenterWs, settings.scanCenterWS );
			_passName = "ScanEffect";
			this.settings = settings;
		}


		public override void OnCameraSetup( CommandBuffer cmd, ref RenderingData renderingData ) 
		{
			
			_cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
			_cameraDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
			
			m_Descriptor = new RenderTextureDescriptor( Screen.width, Screen.height, RenderTextureFormat.Default, 0 )
			{
				depthBufferBits = 0 
			};
			
			RenderingUtils.ReAllocateIfNeeded( ref _tempTex, m_Descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name:"_TempTex" );
			ConfigureTarget( _tempTex );
		}

		
		public override void Execute( ScriptableRenderContext context, ref RenderingData renderingData ) {
			
			if( renderingData.cameraData.camera.cameraType != CameraType.Game ) return;
			if( settings.scanMaterial == null ) return;
			
			CommandBuffer cmd = CommandBufferPool.Get( name:_passName );

			
			using( new ProfilingScope( cmd, new ProfilingSampler( cmd.name ) ) ) 
			{
				Blitter.BlitCameraTexture( cmd, _cameraDepth, _cameraColor, settings.scanMaterial, 0 ); //blit到rt上
				
				
				if( showMark ) 
				{
					cmd.SetRenderTarget( _cameraColor, _cameraDepth );
					var matProp = new MaterialPropertyBlock();
					_computeBuffer.SetData( _marks );
					matProp.SetBuffer( "markBuffer", _computeBuffer );
					_commandData[0].indexCountPerInstance = 6;
					_commandData[0].instanceCount = horizentalCount * verticalCount;
					_graphicsBuffer.SetData( _commandData );
					cmd.DrawMeshInstancedIndirect( mesh, 0, settings.markMaterial, 0, _graphicsBuffer, 0, matProp );
				}
			}
			
			context.ExecuteCommandBuffer( cmd );
			cmd.Clear();
			CommandBufferPool.Release( cmd );
		}

		
		public override void OnCameraCleanup( CommandBuffer cmd )
		{

		}

		~CustomRenderPass() {
			_graphicsBuffer.Dispose();
			_computeBuffer.Dispose();
			Debug.Log( "释放buffer" );
		}

	}
		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion

		
	}

	[Serializable]
	public class Settings
	{
		public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingTransparents;
		[FormerlySerializedAs( "scanShader" )]
		public Material scanMaterial;
		
		[Title( "Static Settings" )]
		[Space(10)]
		public Color scanColorHead = Color.blue;
		public Color scanColor = Color.blue;
		public float outlineWidth = 0.1f;
		public float scanLineWidth = 1f;
		public float scanLineInterval = 1f;
		public float headScanLineWidth = 1f;
		
		[Title( "Dynamics Settings(control by code)" )]
		[Space(10)]
		public float scanLineBrightness = 1f;
		public float scanRange = 1f;
		public float outlineBrightness = 1f;
		public float headScanLineDistance = 8f;
		public Vector3 scanCenterWS = new Vector3( 123.05f, 36.3f, 147.86f );
		public float outlineStarDistance = 30f;

		[Title( "Render Mark" )]
		[Space(10)]
		public Material markMaterial;
		public GameObject markParticle3;
		public GameObject markParticle2;
		public GameObject markParticle1;
	}
}

