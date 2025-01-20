using UnityEngine;
using System.Collections.Generic;

public class ParticleManager : MonoBehaviour
{
    [System.Serializable]
    public class ParticleSettings
    {
        public Color startColor = Color.white;
        public Color endColor = new Color(1f, 1f, 1f, 0f);
        public float startSpeed = 5f;
        public float startSize = 0.3f;
        public int burstCount = 10;
        public float lifetime = 1f;
    }

    [Header("Particle Settings")]
    [SerializeField] private ParticleSettings settings;
    [SerializeField] private int poolSize = 5;

    // Cached components and modules
    private ParticleSystem particles;
    private ParticleSystem.MainModule mainModule;
    private ParticleSystem.EmissionModule emissionModule;
    private ParticleSystem.ColorOverLifetimeModule colorModule;
    private ParticleSystem.ShapeModule shapeModule;

    // Cached objects to reduce allocations
    private static readonly Color transparentColor = new Color(1f, 1f, 1f, 0f);
    private ParticleSystem.Burst cachedBurst;
    private Gradient cachedGradient;
    private GradientColorKey[] colorKeys;
    private GradientAlphaKey[] alphaKeys;
    private ParticleSystem.MinMaxGradient minMaxGradient;

    // Object pool
    private static Queue<ParticleManager> particlePool;
    private static bool isPoolInitialized;

    private void Awake()
    {
        InitializeComponents();
        InitializeCachedObjects();
        SetupParticleSystem();
    }

    private void InitializeComponents()
    {
        particles = GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = gameObject.AddComponent<ParticleSystem>();
        }

        // Cache all module references
        mainModule = particles.main;
        emissionModule = particles.emission;
        colorModule = particles.colorOverLifetime;
        shapeModule = particles.shape;
    }

    private void InitializeCachedObjects()
    {
        // Initialize gradient objects
        cachedGradient = new Gradient();
        colorKeys = new GradientColorKey[] { new GradientColorKey(settings.startColor, 0.0f) };
        alphaKeys = new GradientAlphaKey[] 
        {
            new GradientAlphaKey(1.0f, 0.0f),
            new GradientAlphaKey(0.0f, 1.0f)
        };

        // Initialize burst
        cachedBurst = new ParticleSystem.Burst(0.0f, settings.burstCount);

        // Initialize gradient wrapper
        minMaxGradient = new ParticleSystem.MinMaxGradient();
    }

    private void SetupParticleSystem()
    {
        // Main module settings
        mainModule.startLifetime = settings.lifetime;
        mainModule.startSpeed = settings.startSpeed;
        mainModule.startSize = settings.startSize;
        mainModule.loop = false;
        mainModule.playOnAwake = false;
        mainModule.stopAction = ParticleSystemStopAction.Callback;

        // Emission settings
        emissionModule.enabled = true;
        emissionModule.SetBurst(0, cachedBurst);

        // Color over lifetime
        colorModule.enabled = true;
        UpdateGradient(settings.startColor, settings.endColor);

        // Shape module
        shapeModule.enabled = true;
        shapeModule.shapeType = ParticleSystemShapeType.Sphere;
        shapeModule.radius = 0.1f;

        // Initial state
        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void UpdateGradient(Color start, Color end)
    {
        colorKeys[0].color = start;
        cachedGradient.SetKeys(colorKeys, alphaKeys);
        minMaxGradient.gradient = cachedGradient;
        colorModule.color = minMaxGradient;
    }

    private void OnParticleSystemStopped()
    {
        ReturnToPool();
    }

    public void Play()
    {
        if (particles != null && !particles.isPlaying)
        {
            particles.Play();
        }
    }

    public void Stop()
    {
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void SetColors(Color start, Color end)
    {
        settings.startColor = start;
        settings.endColor = end;
        UpdateGradient(start, end);
    }

    #region Object Pooling

    public static void InitializePool()
    {
        if (isPoolInitialized) return;

        particlePool = new Queue<ParticleManager>();
        isPoolInitialized = true;
    }

    public static ParticleManager Get(Vector3 position)
    {
        if (!isPoolInitialized)
        {
            InitializePool();
        }

        ParticleManager manager;
        if (particlePool.Count > 0)
        {
            manager = particlePool.Dequeue();
            manager.transform.position = position;
            manager.gameObject.SetActive(true);
        }
        else
        {
            GameObject newObj = new GameObject("PooledParticle");
            manager = newObj.AddComponent<ParticleManager>();
            manager.transform.position = position;
        }

        return manager;
    }

    private void ReturnToPool()
    {
        if (particlePool != null)
        {
            gameObject.SetActive(false);
            particlePool.Enqueue(this);
        }
    }

    private void OnDestroy()
    {
        // Clear static references when the last particle manager is destroyed
        if (particlePool != null && particlePool.Count == 0)
        {
            particlePool = null;
            isPoolInitialized = false;
        }
    }

    #endregion
}