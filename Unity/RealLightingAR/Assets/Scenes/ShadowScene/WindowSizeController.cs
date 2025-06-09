using UnityEngine;

public class WindowSizeController : MonoBehaviour
{
    [Tooltip("Either the Renderer from your prefab instance, or the Material asset itself")]
    public SHManager_ALT Manager;

    void Awake()
    {
        if (Manager == null)
            Debug.LogError("No Manager assigned!", this);

    }

    public void OnSliderChanged(float v)
    {
        if (Manager != null)
            Manager.shWindowSize = (int)v;
    }
}
