using System;
using UnityEngine;


namespace FXnRXn
{ 
	public class AdvancedFootIK : MonoBehaviour
	{
		#region Properties

		[Header("---	Feet Settings	---")] 
		[SerializeField] private Animator								animator;
		[SerializeField] private bool									enableFeetIK = true;
		[SerializeField] private LayerMask								groundLayer = -1;
		[Range(0f, 2f)] [SerializeField] private float					heightFromGroundRaycast = 1.14f;
		[Range(0f, 2f)] [SerializeField] private float					raycastDownDistance = 1.5f;
		[SerializeField] private float									pelvisOffset = 0f;
		[Range(0f, 1f)] [SerializeField] private float					pelvisUpAndDownSpeed = 0.28f;
		[Range(0f, 1f)] [SerializeField] private float					feetToIkPositionSpeed = 0.5f;
		
		[Header("---    Weight Response Settings    ---")]
		[SerializeField] private float									maxWeightFootSpread = 0.3f;
		[SerializeField] private float									weightBalanceResponseSpeed = 5f;
		[SerializeField] private float									maxFootRotationAngle = 15f;
		[SerializeField] private float									weightSwayInfluence = 0.2f;
		
		[Header("---	Curve Settings	---")]
		public string leftFootAnimVariableName = "LeftFootIK";
		public string rightFootAnimVariableName = "RightFootIK";
		public bool useProIkFeature = false;
		public bool showSolverDebug = true;
		
		private Vector3 rightFootPosition, leftFootPosition, leftFootIKPosition, rightFootIKPosition;
		private Quaternion leftFootIKRotation, rightFootIKRotation;
		private float lastPelvisPositionY, lastRightFootPositionY, lastLeftFootPositionY;
		
		// Reference to PorterSystem
		private PorterSystem porterSystem;
		private float currentWeightRatio;
		private Vector3 lastBalanceOffset;
		private float footSpreadFactor;
		

		#endregion

		#region Unity events

		private void Awake()
		{
			if (animator == null) animator = GetComponent<Animator>();
			if(porterSystem == null) porterSystem = PlayerController.Instance.GetPosterSystem();

			
		}
		
	

		private void FixedUpdate()
		{
			if(!enableFeetIK) return;
			if(animator == null) return;
			
			if (porterSystem != null)
			{
				// Update weight ratio
				currentWeightRatio = porterSystem.GetWeightCarryRatio();
				UpdateWeightBasedPositions();
			}

			AdjustFeetTarget(ref rightFootPosition, HumanBodyBones.RightFoot);
			AdjustFeetTarget(ref leftFootPosition, HumanBodyBones.LeftFoot);
			
			// Find and raycast to the ground to find positions
			FeetPositionSolver(rightFootPosition, ref rightFootIKPosition, ref rightFootIKRotation); // Handle the solver right foot
			FeetPositionSolver(leftFootPosition, ref leftFootIKPosition, ref leftFootIKRotation); // Handle the solver left foot
		}
		

		private void OnAnimatorIK(int layerIndex)
		{
			if(!enableFeetIK) return;
			if(animator == null) return;
			
			MovePelvisHeight();
			
			
			// Apply weight-based adjustments to foot IK
			Vector3 rightFootOffset = Vector3.right * footSpreadFactor + lastBalanceOffset;
			Vector3 leftFootOffset = Vector3.left * footSpreadFactor + lastBalanceOffset;
			
			
			#region Right Foot IK
			// Right foot ik position and rotation
			animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
			if (useProIkFeature)
			{
				animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, animator.GetInteger(rightFootAnimVariableName));
			}

			// Apply weight-based position and rotation
			Vector3 adjustedRightFootPosition = rightFootIKPosition + rightFootOffset;
			Quaternion adjustedRightFootRotation = ApplyWeightBasedRotation(rightFootIKRotation, currentWeightRatio, true);
			MoveFeetToIKPoint(AvatarIKGoal.RightFoot, adjustedRightFootPosition, adjustedRightFootRotation, ref lastRightFootPositionY);
			#endregion
			
			#region Left Foot IK
			// Left foot ik position and rotation
			animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
			if (useProIkFeature)
			{
				animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, animator.GetInteger(leftFootAnimVariableName));
			}

			// Apply weight-based position and rotation
			Vector3 adjustedLeftFootPosition = leftFootIKPosition + leftFootOffset;
			Quaternion adjustedLeftFootRotation = ApplyWeightBasedRotation(leftFootIKRotation, currentWeightRatio, false);
			MoveFeetToIKPoint(AvatarIKGoal.LeftFoot, adjustedLeftFootPosition, adjustedLeftFootRotation, ref lastLeftFootPositionY);
			
			#endregion
		}

		#endregion
		
		#region Methods

		private void UpdateWeightBasedPositions()
		{
			// Calculate foot spread based on weight
			footSpreadFactor = Mathf.Lerp(0, maxWeightFootSpread, currentWeightRatio);

			// Calculate balance-based offset
			Vector3 balanceOffset = Vector3.zero;
			if (porterSystem.PlayerIsMoving())
			{
				// Add subtle sway based on movement
				float swayX = Mathf.Sin(Time.time * 2f) * weightSwayInfluence * currentWeightRatio;
				balanceOffset = new Vector3(swayX, 0, 0);
			}

			lastBalanceOffset = Vector3.Lerp(lastBalanceOffset, balanceOffset, Time.deltaTime * weightBalanceResponseSpeed);
		}

		private void MoveFeetToIKPoint(AvatarIKGoal foot, Vector3 positionIkHolder, Quaternion rotationIkHolder, ref float lastFootPositionY)
		{
			Vector3 targetIkPosition = animator.GetIKPosition(foot);
			if (positionIkHolder != Vector3.zero)
			{
				targetIkPosition = transform.InverseTransformPoint(targetIkPosition);
				positionIkHolder = transform.InverseTransformPoint(positionIkHolder);

				float yVar = Mathf.Lerp(lastFootPositionY, positionIkHolder.y, feetToIkPositionSpeed);
				targetIkPosition.y += yVar;
				lastFootPositionY = yVar;
				targetIkPosition = transform.TransformPoint(targetIkPosition);
				animator.SetIKRotation(foot, rotationIkHolder);
			}
			animator.SetIKPosition(foot, targetIkPosition);
		}

		private void MovePelvisHeight()
		{
			if (rightFootIKPosition == Vector3.zero || leftFootIKPosition == Vector3.zero || lastPelvisPositionY == 0f)
			{
				lastPelvisPositionY = animator.bodyPosition.y;
				return;
			}
			float lOffsetPosition = leftFootIKPosition.y - transform.position.y;
			float rOffsetPosition = rightFootIKPosition.y - transform.position.y;

			float totalOffset = (lOffsetPosition < rOffsetPosition) ? lOffsetPosition : rOffsetPosition;
			// Add weight-based squat
			float weightSquat = currentWeightRatio * 0.1f; // Subtle squat based on weight
			totalOffset -= weightSquat;
			
			Vector3 newPelvisPosition = animator.bodyPosition + Vector3.up * totalOffset;
			newPelvisPosition.y = Mathf.Lerp(lastPelvisPositionY, newPelvisPosition.y, pelvisUpAndDownSpeed);
			
			// Apply additional weight-based horizontal offset
			//newPelvisPosition += lastBalanceOffset * 0.5f; // Not Needed 
			
			animator.bodyPosition = newPelvisPosition;
			lastPelvisPositionY = animator.bodyPosition.y;

		}

		private void FeetPositionSolver(Vector3 fromSkyPosition, ref Vector3 feetIkPositions, ref Quaternion feetIkRotations)
		{
			RaycastHit feetOutHit;
			if(showSolverDebug)
				Debug.DrawLine(fromSkyPosition, fromSkyPosition + Vector3.down * (raycastDownDistance + heightFromGroundRaycast), Color.yellow);
			
			if (Physics.Raycast(fromSkyPosition, Vector3.down, out feetOutHit, raycastDownDistance + heightFromGroundRaycast, groundLayer))
			{
				// Finding our feet ik position from the sky position
				feetIkPositions = fromSkyPosition;
				feetIkPositions.y = feetOutHit.point.y + pelvisOffset;
				feetIkRotations = Quaternion.FromToRotation(Vector3.up, feetOutHit.normal) * transform.rotation; 
				
				return;
			}
			feetIkPositions = Vector3.zero;
		}

		private void AdjustFeetTarget(ref Vector3 feetPositions, HumanBodyBones foot)
		{
			// feetPositions = animator.GetBoneTransform(foot).position;
			// feetPositions.y = transform.position.y + heightFromGroundRaycast;
			
			// Get the initial foot position from the animator
			feetPositions = animator.GetBoneTransform(foot).position;
    
			// Raycast to check ground position
			RaycastHit hit;
			if (Physics.Raycast(feetPositions + Vector3.up * heightFromGroundRaycast, Vector3.down, out hit, raycastDownDistance, groundLayer))
			{
				// Set the Y position to be slightly above the ground hit point
				feetPositions.y = hit.point.y + pelvisOffset;
			}
			else
			{
				// Fallback if no ground is found
				feetPositions.y = transform.position.y + heightFromGroundRaycast;
			}
		}


	
		#endregion

		#region IK Weight Based System

		private Quaternion ApplyWeightBasedRotation(Quaternion baseRotation, float weightRatio, bool isRightFoot)
		{
			// Calculate weight-based rotation angle
			float rotationAngle = maxFootRotationAngle * weightRatio;
			if (!isRightFoot) rotationAngle *= -1;

			// Apply additional rotation based on weight
			Quaternion weightRotation = Quaternion.Euler(0, 0, rotationAngle);
			return baseRotation * weightRotation;
		}
		
		

		#endregion

	
		
		//--------------------------------------------------------------------------------------------------------------
		
	}
	
	
}

