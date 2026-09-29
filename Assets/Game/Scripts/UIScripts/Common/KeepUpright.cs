using UnityEngine;

/// <summary>
/// Keeps the object's rotation at zero (upright) regardless of its parent's rotation.
/// </summary>
public class KeepUpright : MonoBehaviour
{
    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}
