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
            arCameraManager.requestedLightEstimation =
            LightEstimation.AmbientSphericalHarmonics;
    
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
        if (!eventArgs.lightEstimation.ambientSphericalHarmonics.HasValue)
            return;
        // 1) grab the SH probe
        var sh = eventArgs.lightEstimation.ambientSphericalHarmonics.Value;

        // 2) extract a “dominant direction” from the first‐order bands (l=1)
        //    we use a luminance weight so bright colors pull the direction
        const float lumR = 0.2126f, lumG = 0.7152f, lumB = 0.0722f;
        Vector3 v = new Vector3(
        sh[0,3]*lumR + sh[1,3]*lumG + sh[2,3]*lumB,  // Y11 → x
        sh[0,1]*lumR + sh[1,1]*lumG + sh[2,1]*lumB,  // Y1-1→ y
        sh[0,2]*lumR + sh[1,2]*lumG + sh[2,2]*lumB   // Y10 → z
        );

        if (v.sqrMagnitude < 1e-6f)
            return;

        var dir = v.normalized;

        // 3) sample that direction from the SH to get color
        var dirs    = new Vector3[1] { dir };
        var results = new Color[1];
        sh.Evaluate(dirs, results);
        Color mainColor = results[0];

        // 4) use the max channel as intensity
        float mainIntensity = mainColor.maxColorComponent * directionalLightIntensityMultiplier;

        // 5) apply to your light
        mainDirectionalLight.transform.rotation = Quaternion.LookRotation(dir);
        mainDirectionalLight.color             = mainColor;
        mainDirectionalLight.intensity         = mainIntensity;

        // 6) also apply ambient probe
        RenderSettings.ambientMode  = AmbientMode.Custom;
        RenderSettings.ambientProbe = sh;
        RenderSettings.ambientIntensity = 1f;

         if (Time.frameCount % 60 == 0 && Time.frameCount > 1)
        {
            Debug.Log("dir: " + dir);
            Debug.Log("mainColor: " + mainColor);
            Debug.Log("mainIntensity: " + mainIntensity);
        }
    }

}
