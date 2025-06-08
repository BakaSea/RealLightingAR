using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

[ExecuteInEditMode]
public class SHManager_ALT : MonoBehaviour
{
    [Header("AR Setup (assign in Inspector)")]
    public ARCameraManager cameraManager;

    [Header("ONNX Model (assign in Inspector)")]
    public ModelAsset modelAsset;

    [Header("Inference Settings")]
    [Tooltip("Tensor resolution (W×H)")]
    public int targetWidth   = 640;
    public int targetHeight  = 512;
    [Tooltip("How many model layers to schedule per frame")]
    public int layersPerFrame = 20;

    [Header("Main Directional Light (ML branch)")]
    public Light mainDirectionalLight;
    [Tooltip("Scale the extracted intensity if needed")]
    public float mainLightIntensityMultiplier = 1f;

    [Range(0,1f), Tooltip("How much of the NEW SH to blend each frame (higher = less smoothing)")]
    public float  shBlendNew = 0.2f;  

    [Tooltip("How many SHs to store in the history")]
    public int shWindowSize = 5;

    private Queue<SphericalHarmonicsL2>  _shHistory;

    // ─────────────── internal ───────────────
    Worker        worker;
    Tensor<float> inputTensor;
    Texture2D     cameraTexture;
    NativeArray<byte> rawTextureData;

    IEnumerator   scheduleIter;
    bool          scheduling;
    bool          inferencePending;

    void Awake()
    {
        if (cameraManager == null || modelAsset == null)
        {
            Debug.LogError("Missing ARCameraManager or ModelAsset!", this);
            enabled = false;
            return;
        }

        // 1) Load & create GPU worker
        var model = ModelLoader.Load(modelAsset);
        worker    = new Worker(model, BackendType.GPUCompute);

        // 2) Pre-allocate the input Tensor: batch=1, RGB, H×W
        inputTensor = new Tensor<float>(new TensorShape(1, 3, targetHeight, targetWidth));

        // 3) Pre-allocate a Texture2D + raw buffer for XRCpuImage
        cameraTexture  = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        rawTextureData = cameraTexture.GetRawTextureData<byte>();

        _shHistory = new Queue<SphericalHarmonicsL2>(shWindowSize);
        var seed = new SphericalHarmonicsL2();
        for (int c = 0; c < 3; ++c)
            for (int b = 0; b < 9; ++b)
                seed[c, b] = 0.5f;
        for (int i = 0; i < shWindowSize; i++)
            _shHistory.Enqueue(seed);
    }

    void OnDisable()
    {
        // safe dispose
        if (worker       != null) { worker.Dispose();       worker = null; }
        if (inputTensor  != null) { inputTensor.Dispose();  inputTensor = null; }
        if (rawTextureData.IsCreated) { rawTextureData.Dispose(); }
        if (cameraTexture != null)
        {
            if (Application.isPlaying)
                Destroy(cameraTexture);
            else
                DestroyImmediate(cameraTexture);
            cameraTexture = null;
        }
    }

    void Update()
    {
        if (!Application.isPlaying || worker == null)
            return;

        // ─── 1) Kick off new inference if idle ───────────────────
        if (!scheduling && !inferencePending)
        {
            if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
                return;

            // Convert CPU image into our pre-allocated Texture2D
            var conv = new XRCpuImage.ConversionParams
            {
                inputRect        = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(targetWidth, targetHeight),
                outputFormat     = TextureFormat.RGBA32,
                transformation   = XRCpuImage.Transformation.MirrorY
            };

            unsafe
            {
                cpuImage.Convert(
                    conv,
                    new System.IntPtr(rawTextureData.GetUnsafePtr()),
                    rawTextureData.Length
                );
            }
            cpuImage.Dispose();
            cameraTexture.Apply();

            // CPU‐side helper: texture → tensor
            TextureConverter.ToTensor(cameraTexture, inputTensor, default);

            // Begin layer‐by‐layer scheduling
            scheduleIter = worker.ScheduleIterable(inputTensor);
            scheduling   = true;
            return;
        }

        // ─── 2) Spread scheduling across frames ────────────────
        if (scheduling)
        {
            int  i       = 0;
            bool hasMore = true;
            while (hasMore && i++ < layersPerFrame)
                hasMore = scheduleIter.MoveNext();

            if (!hasMore)
            {
                scheduling       = false;
                inferencePending = true;

                // Fully‐async GPU→CPU readback
                var outT    = worker.PeekOutput() as Tensor<float>;
                var awaiter = outT.ReadbackAndCloneAsync().GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    var cpuCopy = awaiter.GetResult();
                    ApplySH(cpuCopy);
                    cpuCopy.Dispose();
                    inferencePending = false;
                });
            }
            return;
        }
    }

     void ApplySH(Tensor<float> cpuTensor)
    {
        // 1) build the new SH from your model output
        var newSH = new SphericalHarmonicsL2();
        for (int c = 0; c < 3; ++c)
            for (int b = 0; b < 9; ++b)
                newSH[c, b] = cpuTensor[b * 3 + c];

        // 2) push into history, pop oldest if needed
        _shHistory.Enqueue(newSH);
        if (_shHistory.Count > shWindowSize)
            _shHistory.Dequeue();

        // 3) compute the windowed average
        var avgSH = new SphericalHarmonicsL2();
        foreach (var hist in _shHistory)
            for (int c = 0; c < 3; ++c)
                for (int b = 0; b < 9; ++b)
                    avgSH[c, b] += hist[c, b];

        float inv = 1f / _shHistory.Count;
        for (int c = 0; c < 3; ++c)
            for (int b = 0; b < 9; ++b)
                avgSH[c, b] *= inv;

        // 4) apply ambient probe
        RenderSettings.ambientMode      = AmbientMode.Custom;
        RenderSettings.ambientProbe     = avgSH;
        RenderSettings.ambientIntensity = 1f;

        // 5) extract and apply main directional light from avgSH
        if (mainDirectionalLight != null)
        {
            // compute luminance‐weighted first‐order vector
            var lumR = 0.2126f; var lumG = 0.7152f; var lumB = 0.0722f;
            var v = new Vector3(
                avgSH[0,3]*lumR + avgSH[1,3]*lumG + avgSH[2,3]*lumB,
                avgSH[0,1]*lumR + avgSH[1,1]*lumG + avgSH[2,1]*lumB,
                avgSH[0,2]*lumR + avgSH[1,2]*lumG + avgSH[2,2]*lumB
            );

            if (v.sqrMagnitude > 1e-6f)
            {
                var dir = v.normalized;
                mainDirectionalLight.transform.rotation = Quaternion.LookRotation(dir);

                // evaluate avgSH at that direction
                var dirs    = new Vector3[1] { dir };
                var results = new Color[1];
                avgSH.Evaluate(dirs, results);
                var col = results[0];

                mainDirectionalLight.color     = col;
                mainDirectionalLight.intensity = col.maxColorComponent * mainLightIntensityMultiplier;
            }
        }
    }
}
