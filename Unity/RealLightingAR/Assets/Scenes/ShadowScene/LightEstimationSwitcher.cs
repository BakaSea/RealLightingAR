using UnityEngine;

public class LightEstimationSwitcher : MonoBehaviour
{
    [Tooltip("Your ML-based SH estimator (SHManager_ALT)")]
    public SHManager_ALT mlEstimator;

    [Tooltip("Your ARFoundation-API estimator (RealtimeMainlightExtractor)")]
    public RealtimeMainlightExtractor apiEstimator;

    [Tooltip("When true use ML; when false use ARFoundation’s built-in light estimation")]
    public bool useMLModel = false;

    void Start()
    {
        ApplyMode(useMLModel);
    }

    public void ApplyMode(bool useMLModel)
    {
        if (mlEstimator  != null) mlEstimator.enabled  = useMLModel;
        if (apiEstimator != null) apiEstimator.enabled = !useMLModel;
    }
}
