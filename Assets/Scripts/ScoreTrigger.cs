using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[DefaultExecutionOrder(150)] // Execute after Bird but before GameManager
public class ScoreTrigger : MonoBehaviour
{
    // Cached components
    private BoxCollider2D triggerCollider;
    
    // State
    private bool hasScored;
    
    // Cached values
    private static readonly string PlayerTag = "Player";

    private void Awake()
    {
        // Cache component reference
        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasScored) return;

        if (other.CompareTag(PlayerTag) && GameManager.Instance != null)
        {
            hasScored = true;
            GameManager.Instance.AddScore();
            gameObject.SetActive(false); // Deactivate for pooling instead of destroying
        }
    }

    private void OnEnable()
    {
        hasScored = false;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }
}