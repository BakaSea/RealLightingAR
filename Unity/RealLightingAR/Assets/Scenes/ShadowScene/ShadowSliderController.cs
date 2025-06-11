using UnityEngine;
using UnityEngine.UI;


public class ShadowSliderController : MonoBehaviour
{
    [Tooltip("Either the Renderer from your prefab instance, or the Material asset itself")]
    public Material targetMaterial;
    [Tooltip("Name of the float property in your shader (e.g. \"_MyFloatParam\")")]
    public string shaderFloatName = "shadow_strength";

    void Awake()
    {
        if (targetMaterial == null)
            Debug.LogError("No Material assigned!", this);

    }

    public void OnSliderChanged(float v)
    {
        if (targetMaterial != null)
            targetMaterial.SetFloat(shaderFloatName, v);
    }
}
