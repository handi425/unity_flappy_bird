using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PipeSpawner : MonoBehaviour
{
    [Header("Pipe Settings")]
    [SerializeField] private GameObject[] pipePrefabs;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float pipeSpeed = 5f;
    [SerializeField] private float heightRange = 2f;
    [SerializeField] private float gapSize = 4f;

    [Header("Spawn Variation")]
    [SerializeField] private float minHeight = -2f;
    [SerializeField] private float maxHeight = 2f;
    [SerializeField] [Range(0f, 1f)] private float specialPipeChance = 0.3f;

    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private GameObject scoreTriggerPrefab;

    // Cached components and values
    private Camera mainCamera;
    private float pipeWidth;
    private float despawnX;
    private Vector2 viewportLeft;
    private Coroutine spawnCoroutine;
    private Transform poolContainer;
    
    // Object pools
    private Queue<GameObject> pipePool;
    private Queue<GameObject> scoreTriggerPool;
    private List<GameObject> activePipeGroups;
    
    // Cached vectors
    private static readonly Vector3 UpVector = Vector3.up;
    private static readonly Vector3 DownVector = Vector3.down;
    private static readonly Vector3 ZeroVector = Vector3.zero;
    private static readonly Vector2 DefaultTriggerSize = new Vector2(0.5f, 1f);

    // Reusable wait objects
    private WaitForSeconds spawnWait;

    private void Awake()
    {
        enabled = false;
        CreatePoolContainer();
        InitializePoolCollections();
        CacheComponents();
    }

    private void CreatePoolContainer()
    {
        // Create a container for pooled objects that won't be destroyed on scene load
        GameObject container = GameObject.Find("PipePoolContainer");
        if (container == null)
        {
            container = new GameObject("PipePoolContainer");
            DontDestroyOnLoad(container);
        }
        poolContainer = container.transform;
    }

    private void InitializePoolCollections()
    {
        pipePool = new Queue<GameObject>();
        scoreTriggerPool = new Queue<GameObject>();
        activePipeGroups = new List<GameObject>();
    }

    private void CacheComponents()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            viewportLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
            despawnX = viewportLeft.x - 2f;
        }
        
        spawnWait = new WaitForSeconds(spawnInterval);
    }

    private void Start()
    {
        if (!ValidateComponents()) return;
        InitializePools();
    }

    private bool ValidateComponents()
    {
        if (pipePrefabs == null || pipePrefabs.Length == 0)
        {
            Debug.LogError("No pipe prefabs assigned!");
            return false;
        }

        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found!");
            enabled = false;
            return false;
        }

        var pipeRenderer = pipePrefabs[0].GetComponent<SpriteRenderer>();
        if (pipeRenderer != null)
        {
            pipeWidth = pipeRenderer.bounds.size.x;
        }

        return true;
    }

    private void InitializePools()
    {
        ClearPools();

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledPipe();
            CreatePooledScoreTrigger();
        }
    }

    private void ClearPools()
    {
        // Clear existing pools
        while (pipePool.Count > 0)
        {
            var obj = pipePool.Dequeue();
            if (obj != null) Destroy(obj);
        }

        while (scoreTriggerPool.Count > 0)
        {
            var obj = scoreTriggerPool.Dequeue();
            if (obj != null) Destroy(obj);
        }

        // Clear active groups
        foreach (var group in activePipeGroups)
        {
            if (group != null) Destroy(group);
        }
        activePipeGroups.Clear();
    }

    private void CreatePooledPipe()
    {
        if (pipePrefabs == null || pipePrefabs.Length == 0 || poolContainer == null) return;

        GameObject pipe = Instantiate(pipePrefabs[0], poolContainer);
        SetupPipe(pipe, "PooledPipe");
        pipe.SetActive(false);
        pipePool.Enqueue(pipe);
    }

    private void CreatePooledScoreTrigger()
    {
        if (scoreTriggerPrefab == null || poolContainer == null) return;

        GameObject trigger = Instantiate(scoreTriggerPrefab, poolContainer);
        trigger.tag = "ScoreTrigger";
        var triggerCollider = trigger.GetComponent<BoxCollider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.size = DefaultTriggerSize;
        }
        trigger.SetActive(false);
        scoreTriggerPool.Enqueue(trigger);
    }

    private GameObject GetPooledPipe()
    {
        if (pipePool.Count == 0)
        {
            CreatePooledPipe();
        }
        return pipePool.Count > 0 ? pipePool.Dequeue() : null;
    }

    private GameObject GetPooledScoreTrigger()
    {
        if (scoreTriggerPool.Count == 0)
        {
            CreatePooledScoreTrigger();
        }
        return scoreTriggerPool.Count > 0 ? scoreTriggerPool.Dequeue() : null;
    }

    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        if (obj.CompareTag("ScoreTrigger"))
        {
            scoreTriggerPool.Enqueue(obj);
        }
        else
        {
            pipePool.Enqueue(obj);
        }
    }

    private void OnEnable()
    {
        if (!gameObject.activeInHierarchy) return;
        
        if (spawnCoroutine == null)
        {
            spawnCoroutine = StartCoroutine(SpawnRoutine());
        }
    }

    private void OnDisable()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (enabled && !GameManager.Instance.IsGameOver)
        {
            SpawnPipePair();
            yield return spawnWait;
        }
    }

    private void SpawnPipePair()
    {
        try
        {
            if (!gameObject.activeInHierarchy) return;

            GameObject selectedPrefab = GetRandomPipePrefab();
            if (selectedPrefab == null) return;

            // Create pipe group
            Vector3 spawnPos = transform.position;
            spawnPos.y += Random.Range(minHeight, maxHeight);
            GameObject pipeGroup = new GameObject($"PipeGroup_{Time.frameCount}");
            pipeGroup.transform.position = spawnPos;

            // Get pooled objects
            GameObject topPipe = GetPooledPipe();
            GameObject bottomPipe = GetPooledPipe();
            GameObject scoreTrigger = GetPooledScoreTrigger();

            if (topPipe == null || bottomPipe == null || scoreTrigger == null)
            {
                // If any object is null, clean up and return
                if (topPipe != null) ReturnToPool(topPipe);
                if (bottomPipe != null) ReturnToPool(bottomPipe);
                if (scoreTrigger != null) ReturnToPool(scoreTrigger);
                Destroy(pipeGroup);
                return;
            }

            // Setup pipes
            topPipe.transform.SetParent(pipeGroup.transform);
            topPipe.transform.localPosition = UpVector * (gapSize / 2);
            topPipe.transform.localRotation = Quaternion.Euler(0, 0, 180);
            topPipe.SetActive(true);

            bottomPipe.transform.SetParent(pipeGroup.transform);
            bottomPipe.transform.localPosition = DownVector * (gapSize / 2);
            bottomPipe.transform.localRotation = Quaternion.identity;
            bottomPipe.SetActive(true);

            // Setup score trigger
            scoreTrigger.transform.SetParent(pipeGroup.transform);
            scoreTrigger.transform.localPosition = ZeroVector;
            var triggerCollider = scoreTrigger.GetComponent<BoxCollider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.size = new Vector2(DefaultTriggerSize.x, gapSize * 0.8f);
            }
            scoreTrigger.SetActive(true);

            // Add movement
            var groupMovement = pipeGroup.AddComponent<PipeMovement>();
            groupMovement.Initialize(pipeSpeed);

            // Track active group
            activePipeGroups.Add(pipeGroup);

            StartCoroutine(CleanupPipeGroup(pipeGroup));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error spawning pipes: {e.Message}");
        }
    }

    private IEnumerator CleanupPipeGroup(GameObject pipeGroup)
    {
        if (pipeGroup == null) yield break;

        yield return new WaitUntil(() => 
            pipeGroup == null || 
            !pipeGroup.activeInHierarchy || 
            pipeGroup.transform.position.x < despawnX
        );
        
        if (pipeGroup != null)
        {
            // Return objects to pools
            foreach (Transform child in pipeGroup.transform)
            {
                ReturnToPool(child.gameObject);
            }
            
            activePipeGroups.Remove(pipeGroup);
            Destroy(pipeGroup);
        }
    }

    private GameObject GetRandomPipePrefab()
    {
        if (pipePrefabs == null || pipePrefabs.Length == 0) return null;

        return (pipePrefabs.Length > 1 && Random.value < specialPipeChance) 
            ? pipePrefabs[Random.Range(1, pipePrefabs.Length)] 
            : pipePrefabs[0];
    }

    private void SetupPipe(GameObject pipe, string pipeName)
    {
        if (pipe == null) return;

        pipe.name = pipeName;
        pipe.tag = "Obstacle";
        pipe.layer = LayerMask.NameToLayer("Obstacle");
        
        var pipeCollider = pipe.GetComponent<BoxCollider2D>();
        if (pipeCollider == null)
        {
            pipeCollider = pipe.AddComponent<BoxCollider2D>();
        }
        pipeCollider.isTrigger = false;
    }

    public void IncreaseSpeed()
    {
        pipeSpeed = Mathf.Min(pipeSpeed + 0.5f, 10f);
        specialPipeChance = Mathf.Min(specialPipeChance + 0.1f, 0.6f);
    }

    public void ResetSpawner()
    {
        StopAllCoroutines();
        foreach (var group in activePipeGroups.ToArray())
        {
            if (group != null)
            {
                foreach (Transform child in group.transform)
                {
                    ReturnToPool(child.gameObject);
                }
                Destroy(group);
            }
        }
        activePipeGroups.Clear();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        ClearPools();
    }
}