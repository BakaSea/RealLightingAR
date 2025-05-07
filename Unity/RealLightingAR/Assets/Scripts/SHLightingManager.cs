using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class SHLightingManager : MonoBehaviour {

    public Cubemap environmentMap;
    public float intensity = 1.0f;

    void OnValidate() {
        Start();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        SphericalHarmonicsL2 sh;
        if (environmentMap != null) {
            LightProbes.GetInterpolatedProbe(transform.position, null, out sh);
        } else {
            sh = new SphericalHarmonicsL2();
            sh.AddDirectionalLight(new Vector3(0, 1, 0), Color.red, 1.0f);
            sh.AddDirectionalLight(new Vector3(0, -1, 0), Color.green, 1.0f);
            sh.AddDirectionalLight(new Vector3(1, 0, 0), Color.blue, 1.0f);
        }
        RenderSettings.ambientProbe = sh;
        RenderSettings.ambientMode = AmbientMode.Custom;
        RenderSettings.ambientIntensity = intensity;
    }

    // Update is called once per frame
    void Update() {
        
    }

}
