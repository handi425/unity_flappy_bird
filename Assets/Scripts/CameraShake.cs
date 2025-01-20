using UnityEngine;

[DefaultExecutionOrder(200)] // Execute after other updates
public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [SerializeField] private float traumaReductionRate = 1f;
    [SerializeField] private float maxAngle = 10f;
    [SerializeField] private float maxOffset = 0.5f;
    [SerializeField] private float noiseFrequency = 10f;

    [Header("Performance Settings")]
    [SerializeField] private bool useLowQualityMode = false;
    [SerializeField] private int lowQualityFrameSkip = 2;

    // Cached transform
    private Transform cachedTransform;
    
    // Cached original state
    private Vector3 originalPosition;
    private static readonly Quaternion identityRotation = Quaternion.identity;
    
    // Shake state
    private float trauma;
    private Vector3 currentShakeOffset;
    private Quaternion currentShakeRotation;
    
    // Cached values to reduce allocations
    private static readonly Vector3 zAxis = new Vector3(0f, 0f, 1f);
    private Vector3 tempPosition;
    
    // Performance optimization
    private int frameCounter;
    private float lastShakeAmount;
    private float[] cachedPerlinSamples;
    private const int PERLIN_CACHE_SIZE = 360;
    private float perlinSampleIndex;

    private void Awake()
    {
        cachedTransform = transform;
        InitializePerlinCache();
    }

    private void Start()
    {
        originalPosition = cachedTransform.position;
        currentShakeRotation = identityRotation;
        currentShakeOffset = Vector3.zero;
    }

    private void InitializePerlinCache()
    {
        if (useLowQualityMode)
        {
            cachedPerlinSamples = new float[PERLIN_CACHE_SIZE];
            for (int i = 0; i < PERLIN_CACHE_SIZE; i++)
            {
                cachedPerlinSamples[i] = (Mathf.PerlinNoise(i * 0.1f, 0f) * 2f - 1f);
            }
        }
    }

    private void LateUpdate()
    {
        if (trauma <= 0)
        {
            // Early exit if no shake
            if (currentShakeOffset != Vector3.zero || currentShakeRotation != identityRotation)
            {
                ResetCamera();
            }
            return;
        }

        // Skip frames in low quality mode
        if (useLowQualityMode && frameCounter++ % lowQualityFrameSkip != 0)
        {
            return;
        }

        float shake = trauma * trauma; // Square for more dramatic effect
        if (Mathf.Approximately(shake, lastShakeAmount))
        {
            // Skip calculation if shake amount hasn't changed significantly
            return;
        }
        lastShakeAmount = shake;

        if (useLowQualityMode)
        {
            ApplyLowQualityShake(shake);
        }
        else
        {
            ApplyHighQualityShake(shake);
        }

        // Apply final transforms
        tempPosition = originalPosition + currentShakeOffset;
        cachedTransform.SetPositionAndRotation(tempPosition, currentShakeRotation);

        // Reduce trauma over time
        trauma = Mathf.Max(trauma - traumaReductionRate * Time.deltaTime, 0f);
    }

    private void ApplyLowQualityShake(float shake)
    {
        // Use pre-cached perlin samples
        perlinSampleIndex = (perlinSampleIndex + 1) % PERLIN_CACHE_SIZE;
        float sampleX = cachedPerlinSamples[(int)perlinSampleIndex];
        float sampleY = cachedPerlinSamples[((int)perlinSampleIndex + PERLIN_CACHE_SIZE/3) % PERLIN_CACHE_SIZE];
        float rotation = cachedPerlinSamples[((int)perlinSampleIndex + 2*PERLIN_CACHE_SIZE/3) % PERLIN_CACHE_SIZE];

        currentShakeOffset.x = maxOffset * shake * sampleX;
        currentShakeOffset.y = maxOffset * shake * sampleY;
        currentShakeRotation = Quaternion.AngleAxis(maxAngle * shake * rotation, zAxis);
    }

    private void ApplyHighQualityShake(float shake)
    {
        float time = Time.time * noiseFrequency;
        
        // Calculate random but smooth offset using Perlin noise
        currentShakeOffset.x = maxOffset * shake * (Mathf.PerlinNoise(time, 0f) * 2f - 1f);
        currentShakeOffset.y = maxOffset * shake * (Mathf.PerlinNoise(0f, time) * 2f - 1f);
        
        // Calculate rotation
        float angle = maxAngle * shake * (Mathf.PerlinNoise(time, time) * 2f - 1f);
        currentShakeRotation = Quaternion.AngleAxis(angle, zAxis);
    }

    public void AddTrauma(float amount)
    {
        trauma = Mathf.Min(trauma + amount, 1f);
        lastShakeAmount = -1f; // Force recalculation
    }

    public void ResetCamera()
    {
        trauma = 0f;
        currentShakeOffset = Vector3.zero;
        currentShakeRotation = identityRotation;
        cachedTransform.SetPositionAndRotation(originalPosition, identityRotation);
        lastShakeAmount = 0f;
    }

    private void OnValidate()
    {
        // Update quality settings at runtime
        if (Application.isPlaying && useLowQualityMode && cachedPerlinSamples == null)
        {
            InitializePerlinCache();
        }
    }
}