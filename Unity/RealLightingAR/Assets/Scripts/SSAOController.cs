using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SSAOController : MonoBehaviour {

    public ScriptableRendererFeature ssaoFeature;

    public void SetSSAOState(bool enabled) {
        if (ssaoFeature != null) {
            ssaoFeature.SetActive(enabled);
        }
    }

}
