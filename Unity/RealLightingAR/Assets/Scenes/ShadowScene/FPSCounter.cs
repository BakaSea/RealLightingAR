using UnityEngine;
using TMPro;
using UnityEngine.Profiling;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FpsAndMemoryDisplay : MonoBehaviour
{
    [Tooltip("How often (seconds) to sample the current FPS and memory")]
    public float updateInterval = 0.5f;

    TextMeshProUGUI _text;
    float           _timeLeft;

    // for current FPS
    float _accumFps;
    int   _frames;

    // for all-time average FPS
    float _totalTime;
    int   _totalFrames;

    void Awake()
    {
        _text     = GetComponent<TextMeshProUGUI>();
        _timeLeft = updateInterval;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // accumulate current-FPS
        _timeLeft   -= dt;
        _accumFps   += 1f / dt;
        _frames++;

        // accumulate for average-FPS
        _totalTime   += dt;
        _totalFrames++;

        if (_timeLeft <= 0f)
        {
            // compute FPS values
            float currentFps = (_frames > 0) ? (_accumFps / _frames) : 0f;
            float avgFps     = (_totalTime > 0f) ? (_totalFrames / _totalTime) : 0f;

            // pull memory stats (in bytes)
            long monoUsed   = Profiler.GetMonoUsedSizeLong();
            long totalAlloc = Profiler.GetTotalAllocatedMemoryLong();

            // convert to MB
            float usedMB  = monoUsed   / (1024f * 1024f);
            float allocMB = totalAlloc / (1024f * 1024f);

            // update the on-screen text
            _text.text = string.Format(
                "FPS: {0:F1}\nAvg: {1:F1}\nManaged: {2:F1} MB\nNative:  {3:F1} MB",
                currentFps, avgFps, usedMB, allocMB
            );

            // reset only the current-FPS accumulators
            _timeLeft   = updateInterval;
            _accumFps   = 0f;
            _frames     = 0;
        }
    }

    /// <summary>
    /// Hook this (e.g. via a UI Button OnClick(Boolean)) to reset the average FPS.
    /// </summary>
    public void ResetAverage(bool _)
    {
        _totalTime   = 0f;
        _totalFrames = 0;
    }
}
