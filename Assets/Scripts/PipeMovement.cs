using UnityEngine;

[DefaultExecutionOrder(100)] // Execute after GameManager
public class PipeMovement : MonoBehaviour
{
    private float speed = 5f;
    private bool isInitialized;
    private Transform cachedTransform;
    
    // Cached vectors to avoid allocations
    private static readonly Vector3 LeftMovement = Vector3.left;
    private Vector3 currentPosition;
    private Vector3 newPosition;

    private void Awake()
    {
        cachedTransform = transform;
        currentPosition = cachedTransform.position;
    }

    public void Initialize(float moveSpeed)
    {
        speed = moveSpeed;
        isInitialized = true;
        currentPosition = cachedTransform.position;
    }

    private void OnEnable()
    {
        if (!isInitialized)
        {
            Initialize(speed);
        }
    }

    private void Update()
    {
        if (!isInitialized || (GameManager.Instance != null && GameManager.Instance.IsGameOver))
        {
            return;
        }

        // Calculate new position directly to avoid transform.Translate allocations
        float movement = speed * Time.deltaTime;
        currentPosition.x -= movement;
        cachedTransform.position = currentPosition;
    }

    public void SetSpeed(float newSpeed)
    {
        if (!Mathf.Approximately(speed, newSpeed))
        {
            speed = newSpeed;
        }
    }
}