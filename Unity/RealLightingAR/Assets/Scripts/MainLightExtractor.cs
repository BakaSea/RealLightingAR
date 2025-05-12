using UnityEngine;
using UnityEngine.Rendering; // Required for SphericalHarmonicsL2

public class DominantLightExtractor : MonoBehaviour
{
    public Light directionalLight; // Assign your scene's directional light here

    // Arrays for SH evaluation (pre-allocate to avoid garbage collection in Update)
    private Vector3[] _shDirectionArray = new Vector3[1];
    private Color[] _shColorArray = new Color[1];

    void Update() // Or run once on Start(), or when environment changes
    {
        if (directionalLight == null)
        {
            // Only log error once to avoid spamming console
            if (Time.frameCount < 2) // Or use a boolean flag
            {
                Debug.LogError("Directional Light not assigned to DominantLightExtractor on " + gameObject.name);
            }
            return;
        }

        // Get the Spherical Harmonics from the current environment lighting
        SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;

        // --- Log the raw SH L0 coefficients (overall ambient color) ---
        // These are the most basic indicators of whether any light is in the SH probe
        if (Time.frameCount % 60 == 0) // Log these only once a second to avoid spam
        {
            Debug.Log("Ambient Probe SH L0 (RGB): (" +
                      ambientProbe[0, 0].ToString("F3") + ", " + // R channel, L0 M0
                      ambientProbe[1, 0].ToString("F3") + ", " + // G channel, L0 M0
                      ambientProbe[2, 0].ToString("F3") + ")");  // B channel, L0 M0
        }

        // --- Extract Dominant Light Direction (from L1 band) ---
        Vector3 l1Direction = Vector3.zero;

        Vector3 l1_r = new Vector3(ambientProbe[0, 3], ambientProbe[0, 1], ambientProbe[0, 2]);
        Vector3 l1_g = new Vector3(ambientProbe[1, 3], ambientProbe[1, 1], ambientProbe[1, 2]);
        Vector3 l1_b = new Vector3(ambientProbe[2, 3], ambientProbe[2, 1], ambientProbe[2, 2]);

        l1Direction = 0.2126f * l1_r + 0.7152f * l1_g + 0.0722f * l1_b;

        if (l1Direction.sqrMagnitude < 0.001f) {
            l1Direction = Vector3.down; // Default if L1 is very weak (uniform ambient)
        } else {
            l1Direction.Normalize();
        }
        
        directionalLight.transform.rotation = Quaternion.LookRotation(-l1Direction);

        // --- Extract Dominant Light Color/Intensity ---
        _shDirectionArray[0] = l1Direction;
        ambientProbe.Evaluate(_shDirectionArray, _shColorArray);
        Color dominantLightColor = _shColorArray[0];

        dominantLightColor.r = Mathf.Max(0, dominantLightColor.r);
        dominantLightColor.g = Mathf.Max(0, dominantLightColor.g);
        dominantLightColor.b = Mathf.Max(0, dominantLightColor.b);
        
        directionalLight.color = dominantLightColor;
        float intensity = dominantLightColor.maxColorComponent > 0 ? 1.0f : 0.0f;
        #if UNITY_EDITOR
        // Optional: Slight boost if color is very dark but not black, for editor visibility
        if (intensity > 0 && dominantLightColor.maxColorComponent > 0.001f && dominantLightColor.maxColorComponent < 0.1f)
        {
            intensity = 1.5f; // Or some other boost factor
        }
        #endif
        directionalLight.intensity = intensity;

        //debug
        // directionalLight.intensity = 10.0f;
        // directionalLight.color = new Color(0, 0, 1);

        // --- Log the calculated values for the directional light ---
        if (Time.frameCount % 60 == 0) // Log these only once a second
        {
            Debug.Log("Calculated L1 Direction for Light: " + (-l1Direction).ToString("F3")); // Direction the light points
            Debug.Log("Calculated Dominant Light Color (from SH.Evaluate): " + dominantLightColor.ToString("F3"));
            Debug.Log("Set Directional Light Color: " + directionalLight.color.ToString("F3"));
            Debug.Log("Set Directional Light Intensity: " + directionalLight.intensity.ToString("F3"));
        }
    }
}