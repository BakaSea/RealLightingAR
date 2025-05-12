using UnityEngine;
using UnityEngine.Rendering; // Required for SphericalHarmonicsL2

public class MainLightExtractor : MonoBehaviour
{
    public Light directionalLight;

    private Vector3[] _shDirectionArray = new Vector3[1];
    private Color[] _shColorArray = new Color[1];

    void Update()
    {
        if (directionalLight == null)
        {
            // Log error only once
            if (Time.frameCount < 2) Debug.LogError("Directional Light not assigned on " + gameObject.name);
            return;
        }

        SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;

        // --- Log SH L0 ---
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log("Ambient Probe SH L0 (RGB): (" +
                      ambientProbe[0, 0].ToString("F3") + ", " +
                      ambientProbe[1, 0].ToString("F3") + ", " +
                      ambientProbe[2, 0].ToString("F3") + ")");
        }

        // --- Extract L1 Vector ---
        Vector3 l1_r = new Vector3(ambientProbe[0, 3], ambientProbe[0, 1], ambientProbe[0, 2]);
        Vector3 l1_g = new Vector3(ambientProbe[1, 3], ambientProbe[1, 1], ambientProbe[1, 2]);
        Vector3 l1_b = new Vector3(ambientProbe[2, 3], ambientProbe[2, 1], ambientProbe[2, 2]);
        Vector3 l1Vector = 0.2126f * l1_r + 0.7152f * l1_g + 0.0722f * l1_b;

        Vector3 lightSourceDirection; // Direction light COMES FROM

        if (l1Vector.sqrMagnitude < 0.001f)
        {
            // Default to coming straight from above if SH are basically zero
            lightSourceDirection = Vector3.up;
        }
        else
        {
            lightSourceDirection = l1Vector.normalized;
        }

        // --- Ensure Light Source is from Upper Hemisphere ---
        // If calculated source Y is below horizon (negative Y), flip it.
        if (lightSourceDirection.y < 0)
        {
            lightSourceDirection.y = -lightSourceDirection.y;
            // Optional: Re-normalize if needed after flipping Y
            // lightSourceDirection.Normalize(); 
            if (Time.frameCount % 60 == 0) Debug.LogWarning("Original light source was below horizon, flipping Y.");
        }
        // Ensure Y is at least slightly positive to avoid issues with LookRotation if perfectly horizontal
        if (Mathf.Approximately(lightSourceDirection.y, 0)) {
             lightSourceDirection.y = 0.01f; 
             lightSourceDirection.Normalize();
        }


        // Direction the light component should POINT (opposite of source)
        Vector3 lightPointingDirection = -lightSourceDirection;

        // Set Rotation
        directionalLight.transform.rotation = Quaternion.LookRotation(lightPointingDirection, Vector3.up); // Added Vector3.up as up reference

        // --- Extract Dominant Light Color/Intensity ---
        // Use the (potentially flipped) source direction for color evaluation
        _shDirectionArray[0] = lightSourceDirection;
        ambientProbe.Evaluate(_shDirectionArray, _shColorArray);
        Color dominantLightColor = _shColorArray[0];

        dominantLightColor.r = Mathf.Max(0, dominantLightColor.r);
        dominantLightColor.g = Mathf.Max(0, dominantLightColor.g);
        dominantLightColor.b = Mathf.Max(0, dominantLightColor.b);

        directionalLight.color = dominantLightColor;

        // Intensity Calculation (same as before, maybe needs tuning)
        float intensity = dominantLightColor.maxColorComponent > 0 ? 1.0f : 0.0f;
        #if UNITY_EDITOR
        if (intensity > 0 && dominantLightColor.maxColorComponent > 0.001f && dominantLightColor.maxColorComponent < 0.1f)
        {
            intensity = 1.5f;
        }
        #endif
        // Apply manual intensity boost if needed for testing
        intensity *= 3.0f; // Uncomment to force brightness

        directionalLight.intensity = intensity;


        // --- Log calculated values ---
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log("Calculated Light Source Direction (FROM): " + lightSourceDirection.ToString("F3")); // Log source
            Debug.Log("Calculated Light Pointing Direction (TO): " + lightPointingDirection.ToString("F3")); // Log pointing
            Debug.Log("Calculated Dominant Light Color (from SH.Evaluate): " + dominantLightColor.ToString("F3"));
            Debug.Log("Set Directional Light Color: " + directionalLight.color.ToString("F3"));
            Debug.Log("Set Directional Light Intensity: " + directionalLight.intensity.ToString("F3"));
        }
    }
}