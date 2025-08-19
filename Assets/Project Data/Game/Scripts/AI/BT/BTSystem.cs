#pragma warning disable 0067
#pragma warning disable 0414


using UnityEngine;
using System;
using System.Collections.Generic;
using TriInspector;
using UnityEngine.UI;


namespace FXnRXn
{
	public class BTSystem : MonoBehaviour, IPooledObject, IBT
	{
		#region Singleton
		public static BTSystem Instance { get; private set; }

		private void Awake()
		{
			if(Instance == null) Instance = this;
			Initialize();
		}

		#endregion
		
		
    		
		#region Properties
		
		[Title("BT Configuration")]
		[Space(10)]
		[SerializeField] private float			maxHealth = 100f;
		[SerializeField] private float			currentHealth;
		[SerializeField] private BTType			btType = BTType.Gazer;
		[SerializeField] private float			detectionRange = 15f;
		[SerializeField] private float			attackRange = 3f;
        
		[Title("Visual Settings")]
		[Space(10)]
		[SerializeField] private Renderer		btRenderer;
		[SerializeField] private Material		normalMaterial;
		[SerializeField] private Material		revealedMaterial;
		[SerializeField] private Material		damagedMaterial;
		[SerializeField] private float			fadeSpeed = 2f;

		[Title("UI")] 
		[Space(10)] 
		[SerializeField] private Slider healthUISlider;
        
		[Title("Tar Pool")]
		[Space(10)]
		[SerializeField] private GameObject		tarPoolPrefab;
		[SerializeField] private float			tarPoolRadius = 5f;
		[SerializeField] private float			tarSlowEffect = 0.5f;
        
		[Title("Audio")]
		[Space(10)]
		[SerializeField] private AudioSource	btAudioSource;
		[SerializeField] private AudioClip[]	btSounds;
		[SerializeField] private AudioClip		deathSound;
        
		private bool isRevealed = false;
		private float revealTimer = 0f;
		private Coroutine revealCoroutine;
		private MaterialPropertyBlock propertyBlock;
		private Transform playerTransform;
		private bool isDead = false;
        
		// State machine
		private BTState currentState = BTState.Idle;
		private Dictionary<BTState, System.Action> stateActions;

		public Action<float> onHealthChanged;
		public Action<BTSystem> OnBTDeath;
		#endregion
		
		#region Unity Callbacks

		private void Initialize()
		{
			propertyBlock = new MaterialPropertyBlock();
			InitializeStateMachine();
		}

		private void OnEnable()
		{
			onHealthChanged += HealthChange;
		}

		private void OnDisable()
		{
			onHealthChanged -= HealthChange;
		}
		

		#endregion
		
		#region Methods

		public void OnObjectSpawn()
		{
			currentHealth = maxHealth;
			onHealthChanged?.Invoke(currentHealth / 100f);
			isDead = false;
			isRevealed = false;
			currentState = BTState.Idle;

			if (btRenderer != null)
			{
				btRenderer.material = normalMaterial;
				SetVisibility(0f);
			}
			
			// Find player
			GameObject player = PlayerController.Instance.gameObject;
			if (player != null) playerTransform = player.transform;
			else Debug.LogError("Player not found!");
			
			StartCoroutine(BTBehaviorLoop());
		}

		public void OnObjectReturn()
		{
			StopAllCoroutines();
			if (revealCoroutine != null)
			{
				StopCoroutine(revealCoroutine);
				revealCoroutine = null;
			}
		}

		private System.Collections.IEnumerator BTBehaviorLoop()
		{
			while (!isDead)
			{
				if (stateActions.ContainsKey(currentState))
				{
					stateActions[currentState]?.Invoke();
				}
				yield return new WaitForSeconds(0.1f);
			}
			
		}
		
		//--------------------------------------------------------------------------------------------------------------
		//-->										BT STATE														 <--
		//--------------------------------------------------------------------------------------------------------------

		private void InitializeStateMachine()
		{
			stateActions = new Dictionary<BTState, System.Action>
			{
				{ BTState.Idle, IdleState },
				{ BTState.Searching, SearchingState },
				{ BTState.Pursuing, PursuingState },
				{ BTState.Attacking, AttackingState },
				{ BTState.Retreating, RetreatingState }
			};
		}

		private void IdleState()
		{
			if(playerTransform == null) return;
			
			float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
			if (distanceToPlayer <= detectionRange)
			{
				currentState = BTState.Searching;
				PlayRandomSound();
			}
			// else
			// {
			// 	currentState = BTState.Idle;
			// }
			// //
		}

		private void SearchingState()
		{
			if (playerTransform == null) return;
            
			float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            
			if (distanceToPlayer <= detectionRange * 0.5f)
			{
				currentState = BTState.Pursuing;
			}
			else if (distanceToPlayer > detectionRange)
			{
				currentState = BTState.Idle;
			}
			
			// Wander behavior
			Vector3 randomDirection = UnityEngine.Random.insideUnitSphere;
			randomDirection.y = 0;
			transform.position += randomDirection.normalized * Time.deltaTime;
		}

		private void PursuingState()
		{
			if (playerTransform == null) return;
            
			float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            
			if (distanceToPlayer <= attackRange)
			{
				currentState = BTState.Attacking;
			}
			else if (distanceToPlayer > detectionRange * 1.5f)
			{
				currentState = BTState.Searching;
			}
			else
			{
				// Move towards player
				Vector3 direction = (playerTransform.position - transform.position).normalized;
				float moveSpeed = btType == BTType.Catcher ? 4f : 2f;
				transform.position += direction * moveSpeed * Time.deltaTime;
                
				// Create tar trail
				if (UnityEngine.Random.value < 0.1f)
				{
					CreateTarPool(transform.position);
				}
			}
		}

		private void AttackingState()
		{
			if (playerTransform == null) return;
            
			float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            
			if (distanceToPlayer > attackRange)
			{
				currentState = BTState.Pursuing;
			}
			else
			{
				// Perform attack
				PerformAttack();
			}
		}

		private void RetreatingState()
		{
			if (playerTransform == null) return;
            
			// Move away from player
			Vector3 direction = (transform.position - playerTransform.position).normalized;
			transform.position += direction * 3f * Time.deltaTime;
            
			float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
			if (distanceToPlayer > detectionRange)
			{
				currentState = BTState.Idle;
			}
		}

		private void PerformAttack()
		{
			// Attack logic based on BT type
			switch (btType)
			{
				case BTType.Gazer:
					// Grab attack
					Debug.Log("BT Grab Attack!");
					break;
                    
				case BTType.Catcher:
					// Tar throw attack
					ThrowTar();
					break;
                    
				case BTType.Giant:
					// Area attack
					AreaAttack();
					break;
			}
            
			PlayRandomSound();
		}

		private void ThrowTar()
		{
			if (tarPoolPrefab != null && playerTransform != null)
			{
				Vector3 throwDirection = (playerTransform.position - transform.position).normalized;
				Vector3 spawnPos = transform.position + throwDirection * 2f;
                
				GameObject tar = ObjectPoolManager.Instance.SpawnFromPool("TarPool", spawnPos, Quaternion.identity);
				if (tar == null)
				{
					tar = Instantiate(tarPoolPrefab, spawnPos, Quaternion.identity);
				}
                
				Rigidbody tarRb = tar.GetComponent<Rigidbody>();
				if (tarRb != null)
				{
					tarRb.AddForce((throwDirection + Vector3.up * 0.5f) * 10f, ForceMode.Impulse);
				}
			}
		}

		private void AreaAttack()
		{
			Collider[] hits = Physics.OverlapSphere(transform.position, attackRange * 2f);
			foreach (Collider hit in hits)
			{
				if (hit.CompareTag("Player"))
				{
					// Apply damage or effect
					Debug.Log("Giant BT Area Attack!");
				}
			}
            
			// Create multiple tar pools
			for (int i = 0; i < 5; i++)
			{
				Vector3 randomPos = transform.position + UnityEngine.Random.insideUnitSphere * attackRange;
				randomPos.y = transform.position.y;
				CreateTarPool(randomPos);
			}
		}

		private void CreateTarPool(Vector3 position)
		{
			if (tarPoolPrefab != null)
			{
				GameObject pool = ObjectPoolManager.Instance.SpawnFromPool("TarPool", position, Quaternion.identity);
				if (pool == null)
				{
					pool = Instantiate(tarPoolPrefab, position, Quaternion.identity);
				}
                
				// TarPool tarComponent = pool.GetComponent<TarPool>();
				// if (tarComponent != null)
				// {
				// 	tarComponent.Initialize(tarPoolRadius, tarSlowEffect);
				// }
			}
		}

		public void TakeHematicDamage(float damage)
		{
			if (isDead) return;
			
			currentHealth -= damage;
			onHealthChanged?.Invoke(currentHealth / 100f);
            
			// Visual feedback
			StartCoroutine(DamageFlash());
            
			// Reveal on damage
			Reveal(3f);
            
			if (currentHealth <= 0)
			{
				Die();
			}
			else if (currentHealth < maxHealth * 0.3f)
			{
				currentState = BTState.Retreating;
			}
		}

		private void HealthChange(float health)
		{
			if (healthUISlider != null) healthUISlider.value = health;
		}

		private System.Collections.IEnumerator DamageFlash()
		{
			yield return null;
		}

		public void Reveal(float duration)
		{
			
		}

		private System.Collections.IEnumerator RevealCoroutine(float duration)
		{
			yield return null;
		}

		private void SetVisibility(float alpha)
		{
			
		}

		public bool IsRevealed()
		{
			return false;
		}

		private void Die()
		{
			
		}

		private System.Collections.IEnumerator ReturnToPoolAfterDelay(float delay)
		{
			yield return null;
		}

		private void PlayRandomSound()
		{
			
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		private void OnDrawGizmosSelected()
		{
			// Detection range
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, detectionRange);
            
			// Attack range
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(transform.position, attackRange);
		}
		#endregion
	}

	public enum BTType
	{
		Gazer,
		Catcher,
		Giant
	}

	public enum BTState
	{
		Idle,
		Searching,
		Pursuing,
		Attacking,
		Retreating
	}

	public interface IBT
	{
		void TakeHematicDamage(float damage);
		void Reveal(float duration);
		bool IsRevealed();
	}
	
}

