using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Execute before Lean Touch components
[DefaultExecutionOrder(-100)]
public class MouseMoveCamera : MonoBehaviour
{
    [Header("Camera Movement Settings")]
    public bool useMiddleMouseButton = true;
    public bool invertX = false;
    public bool invertY = false;
    [Range(0.1f, 20f)]
    public float movementSensitivity = 8f; // Multiplier for camera movement speed (higher = faster response)
    
    [Header("Anchor Settings")]
    public LayerMask raycastLayers = -1; // What layers to raycast against for anchoring
    public float defaultAnchorDistance = 100f; // Default distance if no raycast hit (increased for no-collider scenes)
    public bool showDebugInfo = false;
    
    private Vector2 lastMousePosition;
    private bool isDragging = false;
    private Camera cam;
    private Vector3 anchorWorldPoint;
    private bool hasValidAnchor = false;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        if (useMiddleMouseButton)
        {
            HandleMouseMovement();
        }
    }
    
    void HandleMouseMovement()
    {
        // Check for middle mouse button down
        if (Input.GetMouseButtonDown(2))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
            EstablishAnchorPoint(lastMousePosition);
        }
        
        // Check for middle mouse button drag
        if (Input.GetMouseButton(2) && isDragging && hasValidAnchor)
        {
            Vector2 currentMousePosition = Input.mousePosition;
            
            // Apply inversion if needed
            Vector2 targetMousePosition = currentMousePosition;
            if (invertX) targetMousePosition.x = lastMousePosition.x - (currentMousePosition.x - lastMousePosition.x);
            if (invertY) targetMousePosition.y = lastMousePosition.y - (currentMousePosition.y - lastMousePosition.y);
            
            MoveToKeepAnchor(targetMousePosition);
        }
        
        // Check for middle mouse button up
        if (Input.GetMouseButtonUp(2))
        {
            isDragging = false;
            hasValidAnchor = false;
        }
    }
    
    void EstablishAnchorPoint(Vector2 mousePosition)
    {
        if (cam == null) return;
        
        // Cast a ray from camera through mouse position
        Ray mouseRay = cam.ScreenPointToRay(mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(mouseRay, out hit, Mathf.Infinity, raycastLayers))
        {
            // Found a world point to anchor to
            anchorWorldPoint = hit.point;
            hasValidAnchor = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"Anchor established at: {anchorWorldPoint}");
            }
        }
        else
        {
            // No hit, create anchor point at default distance
            anchorWorldPoint = mouseRay.origin + mouseRay.direction * defaultAnchorDistance;
            hasValidAnchor = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"No colliders hit - Anchor established at default distance ({defaultAnchorDistance}): {anchorWorldPoint}");
            }
        }
    }
    
    void MoveToKeepAnchor(Vector2 targetMousePosition)
    {
        if (cam == null || !hasValidAnchor) return;
        
        // Calculate where the anchor point currently appears on screen
        Vector3 currentScreenPos = cam.WorldToScreenPoint(anchorWorldPoint);
        
        // Calculate the difference between current screen position and target mouse position
        Vector2 screenDelta = targetMousePosition - new Vector2(currentScreenPos.x, currentScreenPos.y);
        
        // If the delta is very small, don't move (avoid jittering)
        if (screenDelta.magnitude < 0.1f) return;
        
        // Convert screen delta to world space movement
        // We need to move the camera in the opposite direction to make the anchor point follow the mouse
        Vector3 worldDelta = ScreenDeltaToWorldDelta(screenDelta, currentScreenPos.z);
        
        // Move camera by the inverse of the delta
        transform.position -= worldDelta;
        
        // Debug verification
        if (showDebugInfo)
        {
            Vector3 newScreenPos = cam.WorldToScreenPoint(anchorWorldPoint);
            Vector2 error = targetMousePosition - new Vector2(newScreenPos.x, newScreenPos.y);
            Debug.Log($"Screen delta: {screenDelta}, World delta: {worldDelta}, Remaining error: {error.magnitude}");
        }
    }
    
    Vector3 ScreenDeltaToWorldDelta(Vector2 screenDelta, float depth)
    {
        // Convert screen space delta to world space delta at the specified depth
        Vector3 screenPoint1 = new Vector3(0, 0, depth);
        Vector3 screenPoint2 = new Vector3(screenDelta.x, screenDelta.y, depth);
        
        Vector3 worldPoint1 = cam.ScreenToWorldPoint(screenPoint1);
        Vector3 worldPoint2 = cam.ScreenToWorldPoint(screenPoint2);
        
        return worldPoint2 - worldPoint1;
    }
    
    // Property to let other scripts know if middle mouse is being used
    public bool IsUsingMiddleMouse => isDragging;
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (hasValidAnchor && isDragging)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(anchorWorldPoint, 0.5f);
            
            if (cam != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(cam.transform.position, anchorWorldPoint);
            }
        }
    }
} 