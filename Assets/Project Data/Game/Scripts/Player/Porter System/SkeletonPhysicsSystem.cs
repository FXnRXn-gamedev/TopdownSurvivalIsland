using System;
using UnityEngine;

namespace FXnRXn
{
	public class SkeletonPhysicsSystem : MonoBehaviour
	{
		#region Properties
		[Header("-------------		Skeleton Tracking		-------------")]
		[Space(10)]
		[SerializeField] private Transform									skeletonRoot; // Usually the Hip bone
		[SerializeField] private Transform									cargoAnchor;
		[SerializeField] private Animator									animator;

		[Header("-------------		Physics Settings		-------------")]
		[Space(10)]
		[SerializeField] private float										followSpeed = 5f;
		[SerializeField] private float										rotationFollowSpeed = 3f;
		[SerializeField] private float										maxFollowDistance = 2f;
		[SerializeField] private bool										useSmoothing = true;
		
		[Header("-------------		Weight-Based Sway		-------------")]
		[Space(10)]
		[SerializeField] private float										swayIntensity = 1f;
		[SerializeField] private float										swayFrequency = 1.5f;
		[SerializeField] private float										weightSwayMultiplier = 2f;
		[SerializeField] private AnimationCurve								swayFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
		
		
		[Header("-------------		Lean Response		-------------")]
		[Space(10)]
		[SerializeField] private float										leanResponseIntensity = 0.8f;
		[SerializeField] private float										leanDelayTime = 0.1f;
		[SerializeField] private float										leanRecoverySpeed = 4f;
        
		
		[Header("-------------		Fall Physics		-------------")]
		[Space(10)]
		[SerializeField] private float										fallGravityMultiplier = 1.5f;
		[SerializeField] private float										fallSwayIntensity = 3f;
		[SerializeField] private bool										enableFallPhysics = true;


		// Private variables
		private PorterSystem porterSystem;
		private AdvancedFootIK footIK;
		private Vector3 baseAnchorLocalPosition;
		private Quaternion baseAnchorLocalRotation;
		private Vector3 currentSwayOffset;
		private Vector3 targetSwayOffset;
		private float swayTimer;
		private Vector3 velocity;
		private bool isPlayerFalling;
		private float leanTimer;
        
		// Skeleton tracking
		private Vector3 lastSkeletonPosition;
		private Quaternion lastSkeletonRotation;
		private Vector3 skeletonVelocity;
		private float skeletonAngularVelocity;

		#endregion


		#region Unity callback

		private void Awake()
		{
			// Get components
			if (porterSystem == null) porterSystem = GetComponent<PorterSystem>();
			if (animator == null) animator = GetComponentInChildren<Animator>();
			//if (skeletonRoot == null) skeletonRoot = animator.GetBoneTransform(HumanBodyBones.Spine).transform;
			if (footIK == null) footIK = GetComponentInChildren<AdvancedFootIK>();
			cargoAnchor = porterSystem.GetCargoAnchor();
			
			// Store base transform
			if (cargoAnchor != null)
			{
				baseAnchorLocalPosition = cargoAnchor.localPosition;
				baseAnchorLocalRotation = cargoAnchor.localRotation;
			}
		}

		private void Start()
		{
			if (skeletonRoot != null)
			{
				lastSkeletonPosition = skeletonRoot.position;
				lastSkeletonRotation = skeletonRoot.rotation;
			}
		}

		private void Update()
		{
			if (cargoAnchor == null || skeletonRoot == null) return;
			
			UpdateSkeletonTracking();
			UpdateWeightBasedSway();
			UpdateLeanResponse();
			UpdateFallPhysics();
			ApplyCargoAnchorTransform();
		}

		#endregion

		#region Method

		// -------------------------------------------------------------------------------------------------------------
		//-->                                 SKELETON TRACKING SYSTEM
		// -------------------------------------------------------------------------------------------------------------

		#region Skeleton Tracking System

		private void UpdateSkeletonTracking()
		{
			// Calculate skeleton velocity and angular velocity
			Vector3 currentSkeletonPosition = skeletonRoot.position;
			Quaternion currentSkeletonRotation = skeletonRoot.rotation;
			skeletonVelocity = (currentSkeletonPosition - lastSkeletonPosition) / Time.deltaTime;
			skeletonAngularVelocity = Quaternion.Angle(currentSkeletonRotation, lastSkeletonRotation) / Time.deltaTime;
			lastSkeletonPosition = currentSkeletonPosition;
			lastSkeletonRotation = currentSkeletonRotation;
			
		}

		private Vector3 CalculateSkeletonInfluence()
		{
			Vector3 influence = Vector3.zero;
			// Position influence based on skeleton movement
			Vector3 skeletonOffset = skeletonRoot.position - transform.position; // cargoAnchor.position
			skeletonOffset.y = 0; // Remove vertical offset to prevent cargo floating
			// Limit the influence distance
			if (skeletonOffset.magnitude > maxFollowDistance)
			{
				skeletonOffset = skeletonOffset.normalized * maxFollowDistance;
			}
			influence += skeletonOffset * 0.1f; // Subtle influence
			// Add velocity-based influence
			influence += skeletonVelocity * 0.05f;

			return influence;
		}

		private Quaternion CalculateSkeletonRotationInfluence()
		{
			if (porterSystem == null) return Quaternion.identity;
			
			float weightRatio = porterSystem.GetWeightCarryRatio();
			if (weightRatio < 0.1f) return Quaternion.identity;
			
			// Get the skeleton's rotation relative to the character
			Quaternion skeletonRelativeRotation = Quaternion.Inverse(transform.rotation) * skeletonRoot.rotation;
            
			// Extract only the relevant rotation components (mainly X and Z for lean)
			Vector3 skeletonEuler = skeletonRelativeRotation.eulerAngles;
            
			// Normalize angles to -180 to 180 range
			if (skeletonEuler.x > 180) skeletonEuler.x -= 360;
			//if (skeletonEuler.z > 180) skeletonEuler.z -= 360;
            
			// Apply weight-based influence
			float influenceStrength = weightRatio * leanResponseIntensity;
			skeletonEuler *= influenceStrength;
            
			return Quaternion.Euler(skeletonEuler * 0.3f); // Reduce intensity
		}

		#endregion
		
		
		
		// -------------------------------------------------------------------------------------------------------------
		//-->                                 WEIGHT-BASED SWAY SYSTEM
		// -------------------------------------------------------------------------------------------------------------
		
		#region Weight-Based Sway System

		private void UpdateWeightBasedSway()
		{
			
		}
		
		
		
		#endregion
		
		// -------------------------------------------------------------------------------------------------------------
		//-->                                 LEAN RESPONSE SYSTEM
		// -------------------------------------------------------------------------------------------------------------
		
		#region Lean Response System

		private void UpdateLeanResponse()
		{
			
		}
		
		
		#endregion
		
		// -------------------------------------------------------------------------------------------------------------
		//-->                                 FALL PHYSICS SYSTEM
		// -------------------------------------------------------------------------------------------------------------
		
		#region Fall Physics System

		private void UpdateFallPhysics()
		{
			
		}
		
		
		#endregion
		
		// -------------------------------------------------------------------------------------------------------------
		//-->                                 TRANSFORM APPLICATION
		// -------------------------------------------------------------------------------------------------------------
		
		#region Transform Application

		private void ApplyCargoAnchorTransform()
		{
			// Smooth the sway offset
			if (useSmoothing)
			{
				currentSwayOffset = Vector3.Lerp(currentSwayOffset, targetSwayOffset, Time.deltaTime * followSpeed);
			}
			else
			{
				currentSwayOffset = targetSwayOffset;
			}
            
			// Calculate final position
			Vector3 skeletonInfluence = CalculateSkeletonInfluence();
			Vector3 finalPosition = baseAnchorLocalPosition + currentSwayOffset + skeletonInfluence;
            
			// Calculate final rotation
			Quaternion skeletonRotationInfluence = CalculateSkeletonRotationInfluence();
			Quaternion finalRotation = baseAnchorLocalRotation * skeletonRotationInfluence;
            
			// Apply transforms
			cargoAnchor.localPosition = Vector3.Lerp(cargoAnchor.localPosition, finalPosition, Time.deltaTime * followSpeed);
			cargoAnchor.localRotation = Quaternion.Lerp(cargoAnchor.localRotation, finalRotation, Time.deltaTime * rotationFollowSpeed);
		}
		
		#endregion
		
		
		
		#region Helper Methods

		

		#endregion
		
		

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------

		
		
		#region Debug
        
		private void OnDrawGizmosSelected()
		{
			if (skeletonRoot != null && cargoAnchor != null)
			{
				// Draw skeleton root
				Gizmos.color = Color.green;
				Gizmos.DrawWireSphere(skeletonRoot.position, 0.1f);
                
				// Draw cargo anchor
				Gizmos.color = Color.blue;
				Gizmos.DrawWireSphere(cargoAnchor.position, 0.15f);
                
				// Draw connection line
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(skeletonRoot.position, cargoAnchor.position);
                
				// Draw sway influence
				if (Application.isPlaying)
				{
					Gizmos.color = Color.red;
					Vector3 swayPos = cargoAnchor.position + currentSwayOffset;
					Gizmos.DrawWireSphere(swayPos, 0.08f);
				}
			}
		}
        
		#endregion
	}
}
