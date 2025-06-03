using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Rendering; // Required for SphericalHarmonicsL2

public class RealtimeMainlightExtractor : MonoBehaviour
{


    public ARCameraManager arCameraManager;
    public Light mainDirectionalLight; // Assign your scene's main directional light

    // Optional: Factors to tweak intensity if needed
    public float intensityScalar = 1.0f; // For average brightness
    public float directionalLightIntensityMultiplier = 1.0f; // For main directional light
    public float manualRotationSpeed = 20f; // Degrees per second for rotation around Y-axis
    public bool useManualDebugRotation = false;

    void Update(){
        if (mainDirectionalLight != null && useManualDebugRotation)
        {
            // Rotate the light around the world Y-axis
            mainDirectionalLight.transform.Rotate(Vector3.up, manualRotationSpeed * Time.deltaTime, Space.World);
        }
    }

    void OnEnable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }
        else
        {
            Debug.LogError("ARCameraManager is not assigned on ARRealTimeLightEstimator.");
        }
    }

    void OnDisable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {   
        // if (Time.frameCount % 60 == 0) Debug.Log("ARLight: OnCameraFrameReceived called."); // Check if this event fires
        // 1. Ambient Spherical Harmonics
        if (eventArgs.lightEstimation.ambientSphericalHarmonics.HasValue)
        {
            // Apply the estimated SH to the scene's ambient probe for ambient lighting
            RenderSettings.ambientProbe = eventArgs.lightEstimation.ambientSphericalHarmonics.Value;
            // You might need to call DynamicGI.UpdateEnvironment(); if RenderSettings.ambientMode is not Skybox or if lighting doesn't update
        }

        // 2. Main Directional Light Estimation
        if (mainDirectionalLight != null)
        {
            // Use main light direction if available
            if (eventArgs.lightEstimation.mainLightDirection.HasValue)
            {
                mainDirectionalLight.transform.rotation = Quaternion.LookRotation(eventArgs.lightEstimation.mainLightDirection.Value);
            }

            // Use main light color if available (ARKit provides it in linear space)
            if (eventArgs.lightEstimation.mainLightColor.HasValue)
            {
                // Ensure your project is in Linear color space for best results.
                // If in Gamma, you might need color space conversion.
                mainDirectionalLight.color = eventArgs.lightEstimation.mainLightColor.Value;
            }
            else if (eventArgs.lightEstimation.averageColorTemperature.HasValue)
            {
                // Fallback to color temperature if specific main light color isn't available
                mainDirectionalLight.colorTemperature = eventArgs.lightEstimation.averageColorTemperature.Value;
            }

            //DEBUG INFO
            if (Time.frameCount % 60 == 0 && Time.frameCount > 1)
            {

                
                // ADD THIS LINE TO LOG THE ACTUAL OUTPUT DIRECTION:
                Debug.Log("Directional Light ACTUAL Output Direction (transform.forward): " + mainDirectionalLight.transform.forward.ToString("F3"));
                
                // Optionally, log Euler angles again to confirm rotation
                // Debug.Log("Directional Light Actual Rotation (eulerAngles): " + directionalLight.transform.eulerAngles.ToString("F3"));

                Debug.Log("Calculated Dominant Light Color (from SH.Evaluate): " + mainDirectionalLight.color.ToString("F3"));

                //eventArgs.lightEstimation.averageBrightness.HasValue
                Debug.Log("Calculated Dominant Light Intensity (from SH.Evaluate): " + mainDirectionalLight.intensity.ToString("F3"));
            }

            // // Use main light intensity (lumens) or average intensity as a base
            // if (eventArgs.lightEstimation.mainLightIntensityLumens.HasValue)
            // {
            //     // Convert lumens to Unity directional light intensity.
            //     // This conversion can be tricky. ARKit lumens are per physical camera sensor area.
            //     // Unity's directional light intensity is more abstract.
            //     // A common approach is to use averageBrightness and then scale.
            //     // For a simple start, you can try this, but it often needs calibration:
            //     // mainDirectionalLight.intensity = eventArgs.lightEstimation.mainLightIntensityLumens.Value / 1000.0f * directionalLightIntensityMultiplier; // Needs tuning
            //     // It's often more stable to use averageIntensityCelsius (see below)
            // }

            // // A more stable way for intensity is often using averageIntensityCelsius for overall brightness
            // // and then applying a multiplier for the directional light.
            if (eventArgs.lightEstimation.averageBrightness.HasValue)
            {
                // This value represents the estimated brightness of the scene (typically 0 to 2, can go higher).
                // You'll need to scale this to a suitable range for your directional light's intensity.
                mainDirectionalLight.intensity = eventArgs.lightEstimation.averageBrightness.Value * directionalLightIntensityMultiplier;
            }

            // // Set ambient intensity for the scene (affects how bright non-directly lit areas are)
            // if (eventArgs.lightEstimation.averageIntensityCelsius.HasValue)
            // {
            //     RenderSettings.ambientIntensity = eventArgs.lightEstimation.averageIntensityCelsius.Value * intensityScalar;
            // }
        }
    }

}
