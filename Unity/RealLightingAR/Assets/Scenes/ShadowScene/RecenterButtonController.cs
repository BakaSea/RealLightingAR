using UnityEngine;

public class RecenterButtonController : MonoBehaviour
{
    [Header("Centering Feature")]
    [Tooltip("The world‐space GameObject you want to recenter")]
    public GameObject objectToCenter;
    [Tooltip("Camera used to project/deproject. Defaults to Camera.main")]
    public Camera   cameraToUse;

    public void OnCenterButtonClicked()
    {
        if (objectToCenter == null || cameraToUse == null)
            return;

        // 1) Get the object's current depth (z) in screen‐space
        float depth = cameraToUse.WorldToScreenPoint(objectToCenter.transform.position).z;

        // 2) Build a screen‐space point right in the middle
        Vector3 screenCenter = new Vector3(
            Screen.width  * 0.5f,
            Screen.height * 0.5f,
            depth
        );

        // 3) Convert back to world‐space and move the object
        Vector3 worldCenter = cameraToUse.ScreenToWorldPoint(screenCenter);
        objectToCenter.transform.position = worldCenter;
    }
}
