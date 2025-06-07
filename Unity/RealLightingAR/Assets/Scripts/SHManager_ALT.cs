using UnityEngine;
using UnityEngine.Rendering;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections; // Required for Coroutines

public class SHManager_ALT : MonoBehaviour
{
    [Tooltip("The ARCameraManager from your scene's AR Camera.")]
    public ARCameraManager cameraManager;

    [Tooltip("The Sentis model asset file.")]
    public ModelAsset modelAsset;

    [Header("Inference Settings")]
    [Tooltip("How often to run inference, in seconds. Higher values are better for performance.")]
    public float inferenceInterval = 0.5f; // e.g., run twice per second

    [Tooltip("The Sentis backend to use. GPUCompute is generally best for on-device performance.")]
    public BackendType backendType = BackendType.GPUCompute;
    
    // --- Sentis Model ---
    private Model m_RuntimeModel;
    private Worker m_Worker;

    // --- State Management ---
    private bool m_InferenceRunning = false;
    private float m_Timer = 0f;

    // --- Reusable Assets to Avoid Garbage Collection ---
    private Texture2D m_CameraTexture;
    private Tensor<float> m_OutputTensor;

    void Start()
    {
        // Load the model and create the inference worker once.
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        m_Worker = new Worker(m_RuntimeModel, backendType);

        // Pre-allocate the output tensor to avoid creating it repeatedly.
        // Sentis documentation often shows peeking the output, which we'll do inside the coroutine for safety.

        Debug.Log($"SHLightingManager started. Inference will run every {inferenceInterval} seconds.");
    }

    void Update()
    {
        // Use a simple timer to trigger inference periodically instead of every frame.
        m_Timer += Time.deltaTime;
        if (m_Timer >= inferenceInterval)
        {
            if (!m_InferenceRunning)
            {
                m_Timer = 0f;
                StartCoroutine(RunInferenceCoroutine());
            }
        }
    }

    private IEnumerator RunInferenceCoroutine()
    {
        m_InferenceRunning = true;
        // Log that we are starting a new inference cycle.
        // Debug.Log("Starting new SH inference cycle...");

        // 1. Acquire the latest camera image from the CPU.
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            // If we fail to get an image, end this attempt and wait for the next interval.
            m_InferenceRunning = false;
            yield break;
        }

        // 2. Perform the image conversion asynchronously.
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
            outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        // Create the texture only if it doesn't exist or dimensions have changed.
        if (m_CameraTexture == null || m_CameraTexture.width != conversionParams.outputDimensions.x || m_CameraTexture.height != conversionParams.outputDimensions.y)
        {
            m_CameraTexture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, TextureFormat.RGBA32, false);
        }

        var buffer = m_CameraTexture.GetRawTextureData<byte>();
        try
        {
            unsafe
            {
                cpuImage.Convert(conversionParams, new System.IntPtr(buffer.GetUnsafePtr()), buffer.Length);
            }
        } finally
        {
            cpuImage.Dispose();
        }

        // Wait for the asynchronous conversion to complete without blocking the main thread.
        while (!conversionRequest.status.IsDone())
        {
            yield return null;
        }

        // IMPORTANT: We must dispose of the XRCpuImage when we're done with it.
        cpuImage.Dispose();

        // Check if the conversion was successful.
        if (conversionRequest.status != XRCpuImage.AsyncConversionStatus.Ready)
        {
            Debug.LogError($"Failed to convert CPU image. Status: {conversionRequest.status}");
            m_InferenceRunning = false;
            yield break;
        }

        // 3. Upload texture data to the GPU and create the input tensor.
        m_CameraTexture.Apply();
        using (var inputTensor = TextureConverter.ToTensor(m_CameraTexture))
        {
            // 4. Schedule the inference job.
            m_Worker.Schedule(inputTensor);
        }

        // 5. Peek the output tensor and request a non-blocking readback.
        m_OutputTensor = m_Worker.PeekOutput() as Tensor<float>;
        m_OutputTensor.ReadbackRequest();

        // Wait for the GPU to finish inference and for the data to be ready on the CPU.
        while (!m_OutputTensor.IsReadbackRequestDone())
        {
            yield return null;
        }

        // 6. Process the results.
        ApplySHResults(m_OutputTensor);

        m_InferenceRunning = false;
        // Debug.Log("SH inference cycle complete.");
    }

    private void ApplySHResults(Tensor<float> output)
    {
        // Download the data from the tensor. This is now a fast operation.
        var results = output.DownloadToArray();
        output.Dispose();

        // Create and apply the SphericalHarmonicsL2 struct.
        SphericalHarmonicsL2 sh = new SphericalHarmonicsL2();
        for (int i = 0; i < 3; ++i) // For R, G, B channels
        {
            for (int j = 0; j < 9; ++j) // For the 9 SH coefficients
            {
                // Assuming the model outputs a flat array of 27 floats: [r0,g0,b0, r1,g1,b1, ...]
                // The access pattern might need to be adjusted based on your model's exact output layout.
                // Common layout is planar: [r0..r8, g0..g8, b0..b8]
                // Another is interleaved: [r0,g0,b0, r1,g1,b1, ...]
                // This code assumes the interleaved format: results[j * 3 + i]
                sh[i, j] = results[j * 3 + i];
            }
        }

        // Apply the new SH to the scene's lighting settings.
        RenderSettings.ambientProbe = sh;
        RenderSettings.ambientMode = AmbientMode.Custom;
        // You might want to scale the ambient intensity based on the SH L0 term or a separate model output.
        RenderSettings.ambientIntensity = 1.0f; 
    }

    private void OnDestroy()
    {
        // Clean up the worker and model when the object is destroyed.
        m_Worker?.Dispose();
        Object.Destroy(m_CameraTexture); // Clean up the texture we created
    }
}
