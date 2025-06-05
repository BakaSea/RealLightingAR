using UnityEngine;
using UnityEngine.Rendering;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections.LowLevel.Unsafe;

[ExecuteInEditMode]
public class SHLightingManager : MonoBehaviour
{
    public ARCameraManager cameraManager;
    public ModelAsset modelAsset;

    private Model runtimeModel;
    private Worker worker;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
        deltaTime = 0;
    }

    private void OnDestroy()
    {
        if (worker != null)
        {
            worker.Dispose();
        }
    }

    bool inferencePending = false;
    Tensor<float> outputTensor;
    float deltaTime;

    // Update is called once per frame
    void Update()
    {
        deltaTime += Time.deltaTime;
        if (!inferencePending)
        {
            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
                return;
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            Texture2D cameraTexture = new Texture2D(cpuImage.width, cpuImage.height, TextureFormat.RGBA32, false);

            var rawTextureData = cameraTexture.GetRawTextureData<byte>();
            try
            {
                unsafe
                {
                    cpuImage.Convert(conversionParams, new System.IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
                }
            } finally
            {
                cpuImage.Dispose();
            }

            cameraTexture.Apply();

            Texture2D inputTexture = new Texture2D(cpuImage.width, cpuImage.height, conversionParams.outputFormat, false);
            Color[] srgbPixels = cameraTexture.GetPixels();
            Color[] linearPixels = new Color[srgbPixels.Length];

            for (int i = 0; i < srgbPixels.Length; ++i)
            {
                linearPixels[i] = srgbPixels[i].linear;
            }

            inputTexture.SetPixels(linearPixels);
            inputTexture.Apply();

            using Tensor inputTensor = TextureConverter.ToTensor(inputTexture, width: 640, height: 512, channels: 3);
            worker.Schedule(inputTensor);
            outputTensor = worker.PeekOutput() as Tensor<float>;
            outputTensor.ReadbackRequest();
            inferencePending = true;
            deltaTime = 0;
        } else if (inferencePending && outputTensor.IsReadbackRequestDone())
        {
            var results = outputTensor.DownloadToArray();
            //Debug.Log(string.Join(", ", results));

            SphericalHarmonicsL2 sh = new SphericalHarmonicsL2();

            for (int i = 0; i < 3; ++i)
            {
                for (int j = 0; j < 9; ++j)
                {
                    sh[i, j] = results[j * 3 + i];
                }
            }

            Debug.Log($"{deltaTime}");

            RenderSettings.ambientProbe = sh;
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientIntensity = 1.0f;
            inferencePending = false;
        }
    }

}
