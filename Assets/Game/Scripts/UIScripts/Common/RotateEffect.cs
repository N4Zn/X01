using UnityEngine;

/// <summary>
/// Simple script to rotate a UI element or GameObject on the Z axis.
/// </summary>
public class RotateEffect : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 20f; // Degrees per second
    public bool clockwise = true;    // Rotation direction

    private void Update()
    {
        float direction = clockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, direction * rotationSpeed * Time.deltaTime);
    }
}
