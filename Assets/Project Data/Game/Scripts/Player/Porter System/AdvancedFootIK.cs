using System;
using System.Collections;
using TriInspector;
using UnityEngine;
using Random = System.Random;


namespace FXnRXn
{ 
	public class AdvancedFootIK : MonoBehaviour
	{
		#region Properties
		[Title("Feet Settings")]
		[Space(10)]
		[Required]
		[SerializeField] private Animator								animator;
		[SerializeField] private bool									enableFeetIK = true;
		[SerializeField] private LayerMask								groundLayer = -1;
		[Range(0f, 2f)] [SerializeField] private float					heightFromGroundRaycast = 1.14f;
		[Range(0f, 2f)] [SerializeField] private float					raycastDownDistance = 1.5f;
		[SerializeField] private float									pelvisOffset = 0f;
		[Range(0f, 1f)] [SerializeField] private float					pelvisUpAndDownSpeed = 0.28f;
		[Range(0f, 1f)] [SerializeField] private float					feetToIkPositionSpeed = 0.5f;
		
		[Title("Weight Response Settings")]
		[Space(10)]
		[SerializeField] private float									maxWeightFootSpread = 0.3f;
		[SerializeField] private float									weightBalanceResponseSpeed = 5f;
		[SerializeField] private float									maxFootRotationAngle = 15f;
		[SerializeField] private float									weightSwayInfluence = 0.2f;
		[Range(0, 0.2f)][SerializeField] private float					weightInfluencePelvis = 0.05f;

		[Title("Body Lean Settings")] 
		[Space(10)]
		[SerializeField] private bool									enableBodyLean;
		[Range(0, 1)][SerializeField] private float						leanTriggerWeightThreshold;
		[Range(0, 1)][SerializeField] private float						leanTriggerBalanceThreshold;
		[SerializeField] private float									maxLeanAngle = 25f;
		[SerializeField] private float									leanSpeed = 2f;
		[SerializeField] private float									leanRecoverySpeed = 3f;
		[SerializeField] private float									timeBeforeCargoDrops = 2f;
		[SerializeField] private float									cargoDropPercentage = 0.4f;
		[SerializeField] private float									howManyLeanHappen = 3f;
		
		
		
		[Title("Curve Settings")]
		[Space(10)]
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
		
		// Lean
		private bool isLeaning = false;
		private float currentLeanAngle = 0f;
		private float targetLeanAngle = 0f;
		private float leanStartTime = 0f;
		private bool hasDroppedCargo = false;
		private Coroutine leanCoroutine;
		private float currentBalance = 1f;

		

		#endregion

		#region Unity events

		private void Awake()
		{
			if (animator == null) animator = GetComponent<Animator>();
			if(porterSystem == null) porterSystem = PlayerController.Instance.GetPosterSystem();
		}

		private void Start()
		{
			ResetLean();
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
				
				// Update Balance
				UpdateBalance();

				if (enableBodyLean)
				{
					CheckLeanConditions();
					
				}
			}

			AdjustFeetTarget(ref rightFootPosition, HumanBodyBones.RightFoot);
			AdjustFeetTarget(ref leftFootPosition, HumanBodyBones.LeftFoot);
			
			// Find and raycast to the ground to find positions
			FeetPositionSolver(rightFootPosition, animator.GetBoneTransform(HumanBodyBones.RightFoot).transform,ref rightFootIKPosition, ref rightFootIKRotation); // Handle the solver right foot// // Newly Added
			FeetPositionSolver(leftFootPosition, animator.GetBoneTransform(HumanBodyBones.LeftFoot).transform, ref leftFootIKPosition, ref leftFootIKRotation); // Handle the solver left foot // Newly Added
		}
		

		private void OnAnimatorIK(int layerIndex)
		{
			if(!enableFeetIK) return;
			if(animator == null) return;
			
			MovePelvisHeight();
			
			// Apply body lean if enabled
			if (enableBodyLean)
			{
				ApplyBodyLean();
			}
			
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
		
		#region Methods : Feet IK

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
			float weightSquat = currentWeightRatio * weightInfluencePelvis; // Subtle squat based on weight
			totalOffset -= weightSquat;
			
			Vector3 newPelvisPosition = animator.bodyPosition + Vector3.up * totalOffset;
			newPelvisPosition.y = Mathf.Lerp(lastPelvisPositionY, newPelvisPosition.y, pelvisUpAndDownSpeed);
			
			// Apply additional weight-based horizontal offset
			//newPelvisPosition += lastBalanceOffset * 0.5f; // Not Needed 
			
			animator.bodyPosition = newPelvisPosition;
			lastPelvisPositionY = animator.bodyPosition.y;

		}

		private void FeetPositionSolver(Vector3 fromSkyPosition, Transform foot, ref Vector3 feetIkPositions, ref Quaternion feetIkRotations) // // Newly Added : Transform foot,
		{
			RaycastHit feetOutHit;
			if(showSolverDebug)
				Debug.DrawLine(fromSkyPosition, fromSkyPosition + Vector3.down * (raycastDownDistance + heightFromGroundRaycast), Color.yellow);
			
			if (Physics.Raycast(fromSkyPosition, Vector3.down, out feetOutHit, raycastDownDistance + heightFromGroundRaycast, groundLayer))
			{
				// Finding our feet ik position from the sky position
				feetIkPositions = fromSkyPosition;
				feetIkPositions.y = feetOutHit.point.y + pelvisOffset;
				//feetIkRotations = Quaternion.FromToRotation(Vector3.up, feetOutHit.normal) * transform.rotation; // Commented 
				// // Newly added(To solve foot Rotation)
				Quaternion rp = Quaternion.LookRotation(foot.transform.parent.forward, foot.parent.up);
				Vector3 footRot = new Vector3(0f, Quaternion.Inverse(rp).eulerAngles.y, 0f);
				feetIkRotations = Quaternion.FromToRotation(Vector3.up, feetOutHit.normal) * Quaternion.Euler(footRot);
				
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

		#region Body Lean System

		private void UpdateBalance()
		{
			currentBalance = porterSystem.GetCurrentBalance() / 100f;
		}

		private void CheckLeanConditions()
		{
			bool shouldLean = porterSystem.GetWeightCarryRatio() >= leanTriggerWeightThreshold &&
			                  currentBalance <= leanTriggerBalanceThreshold;

			if (shouldLean && !isLeaning)
			{
				StartLeaning();
			}
			else if (!shouldLean && isLeaning)
			{
				StopLeaning();
			}
		}

		private void StartLeaning()
		{
			isLeaning = true;
			hasDroppedCargo = false;
			leanStartTime = Time.time;
			
			// Determine lean direction based on balance
			float leanDirection = currentBalance < 0.5f ? 1f : -1f;
			if (UnityEngine.Random.value > 0.5f) leanDirection *= -1f; // Add some randomness

			targetLeanAngle = maxLeanAngle * leanDirection;
			
			// Start the cargo drop timer
			if (leanCoroutine != null)
			{
				StopCoroutine(leanCoroutine);
			}

			leanCoroutine = StartCoroutine(CargoDropTimer());
			
			Debug.Log($"Player started leaning! Weight: {currentWeightRatio:F2}, Balance: {currentBalance:F2}");
		}

		private void StopLeaning()
		{
			isLeaning = false;
			targetLeanAngle = 0f;

			if (leanCoroutine != null)
			{
				StopCoroutine(leanCoroutine);
				leanCoroutine = null;
			}
			
			Debug.Log("Player stopped leaning - balance recovered!");
		}

		private void ApplyBodyLean()
		{
			// Smoothly interpolate to target lean angle
			float lerpSpeed = isLeaning ? leanSpeed : leanRecoverySpeed;
			currentLeanAngle = Mathf.Lerp(currentLeanAngle, targetLeanAngle, Time.deltaTime * lerpSpeed);
			
			// Apply lean to bones
			ApplyLeanToBone(HumanBodyBones.Spine, currentLeanAngle * 1f);
			ApplyLeanToBone(HumanBodyBones.Chest, currentLeanAngle * 1f);
			ApplyLeanToBone(HumanBodyBones.UpperChest, currentLeanAngle * 1f);
		}

		private void ApplyLeanToBone(HumanBodyBones bone, float leanAngle)
		{
			Transform boneTransform = animator.GetBoneTransform(bone);
			if (boneTransform == null) return;
			
			// Create lean rotation around y-axis (side lean)
			Quaternion leanRotation = Quaternion.Euler(0, leanAngle, 0);
			
			// Apply the lean rotation to the bone
			animator.SetBoneLocalRotation(bone, boneTransform.localRotation * leanRotation);
		}

		private IEnumerator CargoDropTimer()
		{
			yield return new WaitForSeconds(timeBeforeCargoDrops);
			
			if (isLeaning && !hasDroppedCargo)
			{
				DropCargo();
			}
		}

		private void DropCargo()
		{
			hasDroppedCargo = true;
			
			// Calculate amount to drop
			float currentCargoWeight = porterSystem.GetCurrentCarryWeight();
			float amountToDrop = currentCargoWeight * cargoDropPercentage;
			
			// Call cargo drop method
			if (porterSystem != null)
			{
				porterSystem.CalculateToRemoveCargo(0.4f);
				Debug.Log($"Dropping {cargoDropPercentage * 100}% of cargo! Amount: {amountToDrop}");
			}
			
			// After dropping cargo, player should recover balance
			float amount = Mathf.Min(porterSystem.GetCurrentBalance()+ 0.3f, 1f);
			porterSystem.IncreaseBalance(amount);
			
			Debug.Log($"Cargo dropped due to imbalance! Dropped: {amountToDrop} units");
		}
		
		

		public void ResetLean()
		{
			enableBodyLean = true;
			leanTriggerWeightThreshold = Mathf.Clamp01(UnityEngine.Random.Range(0.6f, 0.8f));
			leanTriggerBalanceThreshold = Mathf.Clamp01(UnityEngine.Random.Range(0.45f, 0.6f));
		}

		#endregion

		
		//--------------------------------------------------------------------------------------------------------------

		#region Helper
		
		// Public method to manually trigger lean (for testing or external systems)
		public void TriggerLean(float intensity = 1f)
		{
			if (!enableBodyLean) return;
			
			targetLeanAngle = maxLeanAngle * intensity * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
			isLeaning = true;
			
			if (leanCoroutine != null)
			{
				StopCoroutine(leanCoroutine);
			}
			leanCoroutine = StartCoroutine(CargoDropTimer());
		}
		

		#endregion

		public bool IsCurrentlyLeaning() => isLeaning;

	}
	
	
}

