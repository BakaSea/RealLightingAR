using UnityEngine;
using UnityEngine.Rendering; // Required for AmbientMode

public class OverrideARSimLighting : MonoBehaviour
{
    public Material hdriSkyboxMaterial; // Assign your "pinkysunset" HDRI Skybox Material here
    public bool applyOverrideInEditor = true;
    [Tooltip("Intensity to use for RenderSettings.ambientIntensity when baking the probe.")]
    public float ambientIntensityForBake = 1.0f;
    [Tooltip("Intensity to use for RenderSettings.reflectionIntensity when baking the probe.")]
    public float reflectionIntensityForBake = 1.0f;


    void Awake() // Changed from Start to Awake to run even earlier
    {
#if UNITY_EDITOR // Only run this logic in the editor for simulation
        if (applyOverrideInEditor && hdriSkyboxMaterial != null)
        {
            Debug.LogWarning("OverrideARSimLighting (Awake): Attempting to set HDRI for simulation: " + hdriSkyboxMaterial.name);

            RenderSettings.skybox = hdriSkyboxMaterial;
            RenderSettings.ambientMode = AmbientMode.Skybox; // Crucial for SH generation from skybox
            RenderSettings.ambientIntensity = ambientIntensityForBake; 
            RenderSettings.reflectionIntensity = reflectionIntensityForBake;

            // This function signals Unity to update the environment lighting,
            // including GI, reflection probes, and importantly, the ambient probe (SH).
            DynamicGI.UpdateEnvironment();

            // Your DominantLightExtractor script, running in Update(),
            // should now read the updated RenderSettings.ambientProbe on the next frame(s).
            Debug.LogWarning("OverrideARSimLighting (Awake): Called DynamicGI.UpdateEnvironment(). Check console for DominantLightExtractor logs on next frames.");
        }
        else if (hdriSkyboxMaterial == null && applyOverrideInEditor)
        {
            Debug.LogError("OverrideARSimLighting (Awake): hdriSkyboxMaterial is not assigned!");
        }
#endif
    }
}