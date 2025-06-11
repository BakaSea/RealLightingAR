using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Rendering; // Required for SphericalHarmonicsL2

public class RealtimeMainlightExtractor : MonoBehaviour
{

    public ARCameraManager arCameraManager;
    public ARCameraManager cameraManager
        {
            get => arCameraManager;
            set
            {
                if (arCameraManager == value)
                    return;

                if (arCameraManager != null)
                    arCameraManager.frameReceived -= FrameChanged;

                arCameraManager = value;

                if (arCameraManager != null & enabled)
                    arCameraManager.frameReceived += FrameChanged;
            }
        }

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
            arCameraManager.frameReceived += FrameChanged;
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
            arCameraManager.frameReceived -= FrameChanged;
        }
    }

    void FrameChanged(ARCameraFrameEventArgs args)
    {   
        if (!args.lightEstimation.ambientSphericalHarmonics.HasValue ||
            !args.lightEstimation.mainLightDirection.HasValue)
            return;

        // 1) grab the SH probe
        var sh = args.lightEstimation.ambientSphericalHarmonics.Value;
        
        // 2) grab the main light direction from the platform
        var dir = args.lightEstimation.mainLightDirection.Value;

        // 3) sample that direction from the SH to get color
        var dirs    = new Vector3[1] { dir };
        var results = new Color[1];
        sh.Evaluate(dirs, results);
        Color mainColor = results[0];

        // 4) use the max channel as intensity
        float mainIntensity = mainColor.maxColorComponent * directionalLightIntensityMultiplier;

        // 5) apply to your light
        if(mainDirectionalLight != null)
        {
            mainDirectionalLight.transform.rotation = Quaternion.LookRotation(dir);
            mainDirectionalLight.color             = mainColor;
            mainDirectionalLight.intensity         = mainIntensity;
        }

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
