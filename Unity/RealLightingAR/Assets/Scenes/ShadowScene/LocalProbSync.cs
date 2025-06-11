using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

public class LocalProbeSync : MonoBehaviour
{
    public AREnvironmentProbeManager environmentProbeManager; // Assign your XROrigin's manager
    public ReflectionProbe customLocalProbe; // Assign your "CustomProbe" here

    private bool initialProbeRendered = false;

    void OnEnable()
    {
        if (environmentProbeManager == null)
        {
            Debug.LogError("LocalProbeSync: AREnvironmentProbeManager not assigned!");
            enabled = false;
            return;
        }
        if (customLocalProbe == null)
        {
            Debug.LogError("LocalProbeSync: Custom Local Probe not assigned!");
            enabled = false;
            return;
        }

        // environmentProbeManager.trackablesChanged += OnArEnvironmentProbesChanged;
        Debug.Log("LocalProbeSync: Subscribed to trackablesChanged.");
    }

    void OnDisable()
    {
        if (environmentProbeManager != null)
        {
            // environmentProbeManager.trackablesChanged -= OnArEnvironmentProbesChanged;
            Debug.Log("LocalProbeSync: Unsubscribed from trackablesChanged.");
        }
    }

    // Attempt an initial render after a short delay for AR system to stabilize
    System.Collections.IEnumerator Start()
    {
        yield return new WaitForSeconds(1.5f); // Wait for AR session and initial probe data
        if (customLocalProbe != null && !initialProbeRendered)
        {
            customLocalProbe.RenderProbe();
            initialProbeRendered = true;
            Debug.Log($"LocalProbeSync: Initial triggered render for {customLocalProbe.name}.");
        }
    }

    void OnArEnvironmentProbesChanged(ARTrackablesChangedEventArgs<AREnvironmentProbe> args)
    {
        // If any AR environment probe is added or updated,
        // it's a good time to update our local reflection probe,
        // as the global environment it uses as a background might have changed.
        if (args.added.Count > 0 || args.updated.Count > 0)
        {
            if (customLocalProbe != null)
            {
                int renderId = customLocalProbe.RenderProbe();
                initialProbeRendered = true; // Mark as rendered if it happens due to this event
                if (Time.frameCount % 60 == 0) // Log less frequently
                {
                    Debug.Log($"LocalProbeSync: AR Environment Probes changed. Requesting render for {customLocalProbe.name}. Render ID: {renderId}");
                }
            }
        }
    }
}