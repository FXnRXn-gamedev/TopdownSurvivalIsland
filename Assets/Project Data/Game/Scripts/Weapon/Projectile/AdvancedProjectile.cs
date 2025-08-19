using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using TriInspector;

namespace FXnRXn
{
	public abstract class AdvancedProjectile : MonoBehaviour, IPooledObject
	{
    		
		#region Properties

		[Title("Debug")] 
		[Space(10)] 
		[SerializeField] private bool								debug;
		[Title("Projectile Settings")]
		[Space(10)]
		[SerializeField] protected Rigidbody						rb;
		[SerializeField] protected CapsuleCollider					projectileCollider;
		[SerializeField] protected TrailRenderer					trail;
        
		[Title("Performance")]
		[Space(10)]
		[SerializeField] private bool								useContinuousCollisionDetection = true;
		[SerializeField] private int								maxPenetrations = 2;
		[SerializeField] private LayerMask							penetrableLayers;
        
		[Title("Effects")]
		[Space(10)]
		[SerializeField] private ParticleSystem						impactParticles;
		[SerializeField] private Light								projectileLight;
		[SerializeField] private AnimationCurve						lightIntensityCurve;
        
		protected float damage;
		protected float speed;
		protected float maxRange;
		protected Vector3 startPosition;
		protected float distanceTraveled;
		protected int penetrationCount;
		protected RaycastHit[] raycastHits = new RaycastHit[10];
        
		private float lifeTime;
		private const float MAX_LIFETIME = 10f;
		#endregion
		
		#region Unity Callbacks

		private void Update()
		{
			lifeTime += Time.deltaTime;
			// Update diatance traveled
			distanceTraveled = Vector3.Distance(startPosition, transform.position);
			
			// Check if exceeded range or lifetime
			if (distanceTraveled > maxRange || lifeTime > MAX_LIFETIME)
			{
				ReturnToPool();
				return;
			}
			
			// Update light intensity based on lifetime
			if (projectileLight != null && lightIntensityCurve != null)
			{
				float normalizedTime = lifeTime / MAX_LIFETIME;
				projectileLight.intensity = lightIntensityCurve.Evaluate(normalizedTime);
			}
			
			// Perform continuous raycast for better collision detection
			PerformContinuousCollisionDetection();
		}

		#endregion
		
		#region Methods

		protected void ReturnToPool()
		{
			ObjectPoolManager.Instance.ReturnToPool(gameObject);
		}

		protected virtual void PerformContinuousCollisionDetection()
		{
			if (rb == null) return;
			float moveDistance = rb.linearVelocity.magnitude * Time.deltaTime;
			int hitCount = Physics.RaycastNonAlloc(transform.position, rb.linearVelocity, raycastHits, moveDistance, penetrableLayers);
			for (int i = 0; i < hitCount; i++)
			{
				ProcessHit(raycastHits[i]);
			}
			
			if(debug) Debug.DrawRay(transform.position, rb.linearVelocity * Time.deltaTime, Color.red, 0.1f);
		}
		
		protected virtual void ProcessHit(RaycastHit hit)
		{
			// Override in derived classes
			
		}

		protected virtual void OnCollisionEnter(Collision collision)
		{
			HandleImpact(collision.contacts[0].point, collision.contacts[0].normal, collision.gameObject);
		}

		protected virtual void HandleImpact(Vector3 impactPoint, Vector3 impactNormal, GameObject hitObject)
		{
			// Hit BT
			IBT btTarget = hitObject.GetComponent<IBT>();
			// If we hit a BT, deal damage
			if (btTarget != null)
			{
				btTarget.TakeHematicDamage(damage);
			}
			
			
			// Play impact particles
			if (impactParticles != null)
			{
				impactParticles.transform.position = impactPoint;
				impactParticles.transform.rotation = Quaternion.LookRotation(impactNormal);
				impactParticles.Play();
			}
			
			// Check for penetration
			if (CanPenetrate(hitObject))
			{
				penetrationCount++;
				damage *= 0.7f; // Reduce damage after penetration
                
				if (penetrationCount >= maxPenetrations)
				{
					ReturnToPool();
				}
			}
			else
			{
				ReturnToPool();
			}
		}

		protected virtual bool CanPenetrate(GameObject hitObject)
		{
			return penetrationCount < maxPenetrations && ((1 << hitObject.layer) & penetrableLayers) != 0;
		}
		//--------------------------------------------------------------------------------------------------------------
		// -->									INITIAL SETUP PROJECTILE											 <--
		//--------------------------------------------------------------------------------------------------------------
		
		public void OnObjectSpawn()
		{
			ResetProjectile();
			if (trail != null)
			{
				trail.Clear();
				trail.enabled = true;
			}
		}

		public void OnObjectReturn()
		{
			if (trail != null) trail.enabled = false;
			if (impactParticles != null)
			{
				impactParticles.Stop();
				impactParticles.Clear();
			}
		}

		protected virtual void ResetProjectile()
		{
			distanceTraveled = 0f;
			penetrationCount = 0;
			lifeTime = 0f;
			if (rb != null)
			{
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}
		}

		public virtual void Initialize(float projectileSpeed, float projectileDamage, float range)
		{
			speed = projectileSpeed;
			damage = projectileDamage;
			maxRange = range;
			startPosition = transform.position;
			
			if (rb != null)
			{
				rb.linearVelocity = transform.forward * speed;
                
				if (useContinuousCollisionDetection)
				{
					rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
				}
			}
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion

		
	}
}

