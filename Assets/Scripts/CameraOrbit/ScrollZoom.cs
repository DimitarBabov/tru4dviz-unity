using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lean.Touch;

public class ScrollZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float ClampMin = 10;
    public float ClampMax = 80;
    public float Zoom = 80;
    public float Dampening = 0.1f;
    public float scrollSpeed = 3;
    
    [Header("Mouse Zoom")]
    public bool zoomTowardsMouse = true;
    public float mouseZoomSensitivity = 0.1f;
    public LayerMask zoomRaycastLayers = -1; // What layers to raycast against for zoom target
    public float defaultZoomDistance = 10f; // Default distance if no raycast hit

    float currentZoom;
    private Camera cam;

    // Start is called before the first frame update
    void Start()
    {
        cam = Camera.main;
        if (cam == null) cam = GetComponent<Camera>();
        
        Zoom = cam.fieldOfView;
        currentZoom = Zoom;
    }

    // Update is called once per frame
    void Update()
    {
        float scrollDelta = Input.mouseScrollDelta.y;
        
        if (scrollDelta != 0)
        {
            if (zoomTowardsMouse)
            {
                ZoomTowardsMouse(scrollDelta);
            }
            else
            {
                ZoomCenter(scrollDelta);
            }
        }
        
        // Smooth zoom transition
        currentZoom = Mathf.Lerp(currentZoom, Zoom, Dampening);
        cam.fieldOfView = currentZoom;
    }
    
    void ZoomCenter(float scrollDelta)
    {
        // Original zoom behavior - just change FOV
        Zoom -= scrollDelta * scrollSpeed;
        Zoom = Mathf.Clamp(Zoom, ClampMin, ClampMax);
    }
    
    void ZoomTowardsMouse(float scrollDelta)
    {
        // Get mouse position in screen space
        Vector3 mousePosition = Input.mousePosition;
        
        // Convert to world ray
        Ray mouseRay = cam.ScreenPointToRay(mousePosition);
        
        // Try to find a world point to zoom towards
        Vector3 targetPoint;
        RaycastHit hit;
        
        if (Physics.Raycast(mouseRay, out hit, Mathf.Infinity, zoomRaycastLayers))
        {
            // Found something to zoom towards
            targetPoint = hit.point;
        }
        else
        {
            // No hit, use a point along the ray at default distance
            targetPoint = mouseRay.origin + mouseRay.direction * defaultZoomDistance;
        }
        
        // Calculate zoom factor
        float oldZoom = Zoom;
        Zoom -= scrollDelta * scrollSpeed;
        Zoom = Mathf.Clamp(Zoom, ClampMin, ClampMax);
        
        // Calculate zoom ratio
        float zoomRatio = Zoom / oldZoom;
        
        // Move camera towards/away from target point based on zoom
        if (zoomRatio != 1.0f)
        {
            Vector3 directionToTarget = (targetPoint - transform.position).normalized;
            float moveDistance = scrollDelta * mouseZoomSensitivity * Vector3.Distance(transform.position, targetPoint);
            
            // Move camera towards target when zooming in, away when zooming out
            transform.position += directionToTarget * moveDistance;
        }
    }
    
    [ContextMenu("Toggle Zoom Mode")]
    public void ToggleZoomMode()
    {
        zoomTowardsMouse = !zoomTowardsMouse;
        Debug.Log($"Zoom mode: {(zoomTowardsMouse ? "Mouse Position" : "Center")}");
    }
}
