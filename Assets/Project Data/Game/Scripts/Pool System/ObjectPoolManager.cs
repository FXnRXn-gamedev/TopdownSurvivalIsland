using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using TriInspector;



namespace FXnRXn
{
	public class ObjectPoolManager : MonoBehaviour
	{
		#region Singleton
		private static ObjectPoolManager _instance;
		public static ObjectPoolManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = FindFirstObjectByType<ObjectPoolManager>();
					if (_instance == null)
					{
						GameObject go = new GameObject("ObjectPoolManager");
						_instance = go.AddComponent<ObjectPoolManager>();
					}
				}
				return _instance;
			}
		}
		#endregion
		
		
    		
		#region Properties
		[Title("Pool Configuration")]
		[Space(10)]
		[SerializeField] private List<PooledObject>					itemsToPool = new List<PooledObject>();
		[SerializeField] private Transform							poolContainer;
        
		private Dictionary<string, Queue<GameObject>> poolDictionary;
		private Dictionary<string, PooledObject> poolSettings;
		private Dictionary<string, Transform> poolContainers;
		private Dictionary<string, int> poolSizes;
        
		[Title("Performance")]
		[Space(10)]
		[SerializeField] private bool								warmPools = true;
		[SerializeField] private int								preWarmFrameDelay = 3;
		
		#endregion
		
		#region Unity Callbacks
		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return;
			}
            
			_instance = this;
			DontDestroyOnLoad(gameObject);
			InitializePools();
		}
		
		
		#endregion
		
		#region Methods

		private void InitializePools()
		{
			poolDictionary = new Dictionary<string, Queue<GameObject>>();
			poolSettings = new Dictionary<string, PooledObject>();
			poolContainers = new Dictionary<string, Transform>();
			poolSizes = new Dictionary<string, int>();

			if (poolContainer == null)
			{
				poolContainer = new GameObject("PoolContainer").transform;
				poolContainer.SetParent(transform);
			}

			foreach (PooledObject item in itemsToPool)
			{
				CreatePool(item);
			}

			if (warmPools)
			{
				StartCoroutine(WarmPools());
			}
		}
		
		
		private void CreatePool(PooledObject pooledObject)
		{
			Queue<GameObject> objectPool = new Queue<GameObject>();
			poolSettings[pooledObject.tag] = pooledObject;
			poolSizes[pooledObject.tag] = 0;

			Transform container = poolContainer;
			if (pooledObject.useContainer)
			{
				GameObject containerGO = new GameObject($"Pool_{pooledObject.tag}");
				containerGO.transform.SetParent(poolContainer);
				container = containerGO.transform;
				poolContainers[pooledObject.tag] = container;
			}

			for (int i = 0; i < pooledObject.poolSize; i++)
			{
				GameObject obj = CreatePooledObject(pooledObject, container);
				objectPool.Enqueue(obj);
			}

			poolDictionary[pooledObject.tag] = objectPool;
		}
		
		private GameObject CreatePooledObject(PooledObject pooledObject, Transform parent)
		{
			GameObject obj = Instantiate(pooledObject.prefab, parent);
			obj.SetActive(false);
            
			// Add pooled object component for tracking
			PooledObjectInstance poolInstance = obj.AddComponent<PooledObjectInstance>();
			poolInstance.poolTag = pooledObject.tag;
            
			poolSizes[pooledObject.tag]++;
            
			return obj;
		}
		
		public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
		{
			if (!poolDictionary.ContainsKey(tag))
			{
				Debug.LogWarning($"Pool with tag {tag} doesn't exist!");
				return null;
			}

			Queue<GameObject> pool = poolDictionary[tag];
			GameObject objectToSpawn = null;

			// Find inactive object in pool
			if (pool.Count > 0)
			{
				objectToSpawn = pool.Dequeue();
                
				// If object was destroyed, create new one
				if (objectToSpawn == null)
				{
					objectToSpawn = CreateNewPoolObject(tag);
				}
                
				pool.Enqueue(objectToSpawn);
			}
			else if (poolSettings[tag].expandable && poolSizes[tag] < poolSettings[tag].maxSize)
			{
				objectToSpawn = CreateNewPoolObject(tag);
				pool.Enqueue(objectToSpawn);
			}
			else
			{
				// Reuse oldest active object if at max size
				objectToSpawn = pool.Dequeue();
				pool.Enqueue(objectToSpawn);
			}

			if (objectToSpawn != null)
			{
				objectToSpawn.transform.position = position;
				objectToSpawn.transform.rotation = rotation;
				objectToSpawn.SetActive(true);

				// Reset pooled object
				IPooledObject pooledObj = objectToSpawn.GetComponent<IPooledObject>();
				pooledObj?.OnObjectSpawn();
			}

			return objectToSpawn;
		}
		
		private GameObject CreateNewPoolObject(string tag)
		{
			Transform container = poolContainers.ContainsKey(tag) ? poolContainers[tag] : poolContainer;
			return CreatePooledObject(poolSettings[tag], container);
		}

		public void ReturnToPool(GameObject obj)
		{
			PooledObjectInstance poolInstance = obj.GetComponent<PooledObjectInstance>();
			if (poolInstance == null) return;

			IPooledObject pooledObj = obj.GetComponent<IPooledObject>();
			pooledObj?.OnObjectReturn();

			obj.SetActive(false);
			obj.transform.SetParent(poolContainers.ContainsKey(poolInstance.poolTag) ? 
				poolContainers[poolInstance.poolTag] : poolContainer);
		}
		
		private System.Collections.IEnumerator WarmPools()
		{
			yield return new WaitForSeconds(preWarmFrameDelay * Time.deltaTime);
            
			foreach (var kvp in poolDictionary)
			{
				Queue<GameObject> pool = kvp.Value;
				List<GameObject> warmedObjects = new List<GameObject>();
                
				// Activate and deactivate all objects to warm them
				while (pool.Count > 0)
				{
					GameObject obj = pool.Dequeue();
					obj.SetActive(true);
					warmedObjects.Add(obj);
					yield return null; // Spread over frames
				}
                
				foreach (GameObject obj in warmedObjects)
				{
					obj.SetActive(false);
					pool.Enqueue(obj);
				}
			}
		}
		
		public void PreloadPool(string tag, int amount)
		{
			if (!poolSettings.ContainsKey(tag)) return;
            
			Transform container = poolContainers.ContainsKey(tag) ? poolContainers[tag] : poolContainer;
			Queue<GameObject> pool = poolDictionary[tag];
            
			for (int i = 0; i < amount; i++)
			{
				if (poolSizes[tag] >= poolSettings[tag].maxSize) break;
                
				GameObject obj = CreatePooledObject(poolSettings[tag], container);
				pool.Enqueue(obj);
			}
		}
		
		public int GetPoolSize(string tag)
		{
			return poolSizes.ContainsKey(tag) ? poolSizes[tag] : 0;
		}

		public int GetActiveCount(string tag)
		{
			if (!poolDictionary.ContainsKey(tag)) return 0;
            
			return poolDictionary[tag].Count(obj => obj != null && obj.activeInHierarchy);
		}

		#endregion
		
		//--------------------------------------------------------------------------------------------------------------


		#region Helper
		
		
		#endregion
	}

	public interface IPooledObject
	{
		void OnObjectSpawn();
		void OnObjectReturn();
	}
	
	
	
	[Serializable]
	public class PooledObject
	{
		public string tag;
		public GameObject prefab;
		public int poolSize = 20;
		public bool expandable = true;
		public bool useContainer = true;
		public int maxSize = 100;
	}
}

