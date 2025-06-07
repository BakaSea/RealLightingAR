using System.Collections;
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
    public int layersPerFrame = 5;

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
                transformation   = XRCpuImage.Transformation.None
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
        // cpuTensor length == 27 (9 bands × 3 channels)
        var sh = new SphericalHarmonicsL2();
        for (int c = 0; c < 3; ++c)
            for (int b = 0; b < 9; ++b)
                sh[c, b] = cpuTensor[b * 3 + c];

        RenderSettings.ambientMode      = AmbientMode.Custom;
        RenderSettings.ambientProbe     = sh;
        RenderSettings.ambientIntensity = 1f;
    }
}
