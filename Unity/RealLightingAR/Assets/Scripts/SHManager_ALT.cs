using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

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

    [Tooltip("How many SHs to store in the history")]
    public int shWindowSize = 7;

    private Queue<SphericalHarmonicsL2>  _shHistory;

    // ─────────────── internal ───────────────
    Worker        worker;
    Tensor<float> inputTensor;
    Texture2D     cameraTexture;
    NativeArray<byte> rawTextureData;

    IEnumerator   scheduleIter;
    bool          scheduling;
    bool          inferencePending;

    void OnEnable()
    {
        // (Re)initialize everything
        if (cameraManager == null || modelAsset == null)
        {
            Debug.LogError("Missing ARCameraManager or ModelAsset!", this);
            enabled = false;
            return;
        }

        // load & worker
        var model = ModelLoader.Load(modelAsset);
        worker    = new Worker(model, BackendType.GPUCompute);

        // tensor, texture, raw buffer, history…
        inputTensor    = new Tensor<float>(new TensorShape(1,3,targetHeight,targetWidth));
        cameraTexture  = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        rawTextureData = cameraTexture.GetRawTextureData<byte>();

        _shHistory = new Queue<SphericalHarmonicsL2>(shWindowSize);
        var seed = new SphericalHarmonicsL2();
        for (int c = 0; c < 3; c++)
            for (int b = 0; b < 9; b++)
                seed[c,b] = 0.5f;
        for (int i = 0; i < shWindowSize; i++)
            _shHistory.Enqueue(seed);
        
        // Reset the inference state machine
        scheduling       = false;
        inferencePending = false;
        scheduleIter     = null;
    }

    void OnDisable()
    {
        // tear down in reverse
        scheduling       = false;
        inferencePending = false;
        scheduleIter     = null;

        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        if (inputTensor != null)
        {
            inputTensor.Dispose();
            inputTensor = null;
        }
        if (rawTextureData.IsCreated)
        {
            rawTextureData.Dispose();
        }
        if (cameraTexture != null)
        {
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
        if (scheduling&& scheduleIter != null)
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
                    Tensor<float> cpuCopy = null;
                    try
                    {
                        cpuCopy = awaiter.GetResult();
                        if (worker == null) return; // Component was disabled.
                        ApplySH(cpuCopy);
                    }
                    finally
                    {
                        cpuCopy?.Dispose();
                        if (worker != null)
                        {
                            inferencePending = false;
                        }
                    }
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

        // 3) compute the trimmed‐mean SH
        var trimmedSH = new SphericalHarmonicsL2();
        int   W       = _shHistory.Count;
        int   trim    = 2;                         // drop 2 lowest & 2 highest
        int   kept    = Mathf.Max(1, W - 2*trim);  // how many we actually average
        float inv     = 1f / kept;
        var   buf     = new float[W];

        for (int c = 0; c < 3; ++c)
        {
            for (int b = 0; b < 9; ++b)
            {
                // collect history for this (c,b)
            int i = 0;
            foreach (var hist in _shHistory)
                buf[i++] = hist[c,b];

            Array.Sort(buf, 0, W);

            // sum the middle slice [trim .. W-1-trim]
            float sum = 0f;
            for (int k = trim; k < W - trim; ++k)
                sum += buf[k];

            // store the trimmed-mean
            trimmedSH[c,b] = sum * inv;
        }
    }

    // 4) apply ambient probe
    RenderSettings.ambientMode      = AmbientMode.Custom;
    RenderSettings.ambientProbe     = trimmedSH;
    RenderSettings.ambientIntensity = 1f;

    // 5) extract & apply main directional light from trimmedSH
    if (mainDirectionalLight != null)
    {
        // compute luminance‐weighted first‐order vector
        var lumR = 0.2126f; var lumG = 0.7152f; var lumB = 0.0722f;
        var v = new Vector3(
            trimmedSH[0,3]*lumR + trimmedSH[1,3]*lumG + trimmedSH[2,3]*lumB,
            trimmedSH[0,1]*lumR + trimmedSH[1,1]*lumG + trimmedSH[2,1]*lumB,
            trimmedSH[0,2]*lumR + trimmedSH[1,2]*lumG + trimmedSH[2,2]*lumB
        );

        if (v.sqrMagnitude > 1e-6f)
        {
            var dir = v.normalized;
            mainDirectionalLight.transform.rotation = Quaternion.LookRotation(dir);

            // evaluate trimmedSH at that direction
            var dirs    = new Vector3[1] { dir };
            var results = new Color[1];
            trimmedSH.Evaluate(dirs, results);
            var col = results[0];

            mainDirectionalLight.color     = col;
            mainDirectionalLight.intensity = col.maxColorComponent * mainLightIntensityMultiplier;
         }
        }
    }
}
