using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class Bird : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.8f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip flapSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip scoreSound;
    
    // Cached components
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;
    private ParticleSystem particleSystem;
    private Camera mainCamera;
    private CameraShake cameraShake;
    
    // State
    private bool isDead = false;
    private bool canMove = false;
    private Vector3 startPosition;
    
    // Cached vectors to reduce GC allocation
    private static readonly Vector2 UpVector = Vector2.up;
    private static readonly Vector2 ZeroVector = Vector2.zero;
    
    // Reusable objects to avoid GC
    private WaitForSeconds deathAnimationDelay;
    private WaitForEndOfFrame endOfFrameWait;
    private Color colorWhite = Color.white;
    private Color colorRed = Color.red;

    private void Awake()
    {
        // Cache all component references
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        particleSystem = GetComponent<ParticleSystem>();
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraShake = mainCamera.GetComponent<CameraShake>();
        }

        // Store initial position
        startPosition = transform.position;

        // Initialize reusable objects
        deathAnimationDelay = new WaitForSeconds(0.5f);
        endOfFrameWait = new WaitForEndOfFrame();

        // Set up rigidbody properties
        rb.gravityScale = 0f; // We'll handle gravity manually
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public void StartMoving()
    {
        canMove = true;
        rb.velocity = ZeroVector;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        isDead = false;

        // Reset visual state
        if (spriteRenderer != null)
        {
            spriteRenderer.color = colorWhite;
        }
    }

    private void Update()
    {
        if (!canMove || isDead) return;

        // Handle input (both mouse click and touch)
        if (Input.GetMouseButtonDown(0))
        {
            Flap();
        }

        // Apply custom gravity
        rb.velocity += UpVector * gravity * Time.deltaTime;
    }

    private void Flap()
    {
        // Reset vertical velocity and apply jump force
        rb.velocity = UpVector * jumpForce;
        
        // Play flap sound
        if (flapSound != null)
        {
            audioSource.PlayOneShot(flapSound);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Ground"))
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        canMove = false;
        
        StartCoroutine(PlayDeathAnimation());
    }

    private IEnumerator PlayDeathAnimation()
    {
        // Play hit sound
        if (hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        // Trigger camera shake
        if (cameraShake != null)
        {
            cameraShake.AddTrauma(0.6f);
        }

        // Add upward force and rotation on death
        rb.velocity = UpVector * jumpForce * 0.5f;
        
        // Fall animation
        float fallTime = 0;
        float fallDuration = 1f;
        
        while (fallTime < fallDuration)
        {
            fallTime += Time.deltaTime;
            transform.Rotate(0, 0, -300f * Time.deltaTime);
            yield return endOfFrameWait;
        }

        // Trigger particle effect
        if (particleSystem != null)
        {
            particleSystem.Play();
        }

        // Change color to red with fade
        if (spriteRenderer != null)
        {
            float fadeTime = 0;
            float fadeDuration = 0.5f;
            Color originalColor = spriteRenderer.color;
            
            while (fadeTime < fadeDuration)
            {
                fadeTime += Time.deltaTime;
                spriteRenderer.color = Color.Lerp(originalColor, colorRed, fadeTime / fadeDuration);
                yield return endOfFrameWait;
            }
        }

        // Disable physics movement
        rb.velocity = ZeroVector;
        rb.isKinematic = true;

        yield return deathAnimationDelay;

        // Notify game manager after animation
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || !canMove) return;

        if (other.CompareTag("ScoreTrigger"))
        {
            GameManager.Instance.AddScore();
            if (scoreSound != null)
            {
                audioSource.PlayOneShot(scoreSound);
            }
        }
    }

    private void OnEnable()
    {
        // Reset state when object is enabled
        isDead = false;
        canMove = false;
        rb.isKinematic = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = colorWhite;
        }
        transform.rotation = Quaternion.identity;
        transform.position = startPosition;
    }
}