using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Execute before Lean Touch components
[DefaultExecutionOrder(-100)]
public class KeyTranslate : MonoBehaviour
{
    [Header("Translation Settings")]
    public float speed = 2f;
    
    // Update is called once per frame
    void Update()
    {
        HandleKeyboardTranslation();
    }
    
    void HandleKeyboardTranslation()
    {
        Vector3 movement = Vector3.zero;
        
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
        {
            movement.x -= Time.deltaTime * speed;
        }
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
        {
            movement.x += Time.deltaTime * speed;
        }
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
        {
            movement.z += Time.deltaTime * speed;
        }
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            movement.z -= Time.deltaTime * speed;
        }
        
        if (movement != Vector3.zero)
        {
            transform.Translate(movement);
        }
    }
}
