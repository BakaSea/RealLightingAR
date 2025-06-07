using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections.LowLevel.Unsafe;


[ExecuteInEditMode]
public class SHManager_ALT : MonoBehaviour
{
    public ARCameraManager cameraManager;
    public ModelAsset     modelAsset;

    // ─── scheduling + readback state ───────────────────
    public int k_LayersPerFrame = 20;
    private IEnumerator    m_Schedule;
    bool           m_Scheduling   = false;
    bool           inferencePending = false;
    Tensor<float>  outputTensor;
    float          deltaTime       = 0f;

    // ─── preallocated resources ────────────────────────
    Texture2D       cameraTexture;
    NativeArray<byte> rawTextureData;
    Tensor<float>   inputTensor;
    const int       targetW = 640, targetH = 512;

    void Start()
    {
        // 1) load + worker
        var model  = ModelLoader.Load(modelAsset);
        var worker = new Worker(model, BackendType.GPUCompute);

        // 2) pre-alloc cameraTexture + raw buffer
        cameraTexture   = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        rawTextureData  = cameraTexture.GetRawTextureData<byte>();

        // 3) pre-alloc input tensor
        inputTensor     = new Tensor<float>(new TensorShape(1, 3, targetH, targetW));

        // stash worker on the component
        this.worker    = worker;
    }

    void OnDestroy()
    {
        // only dispose if it was ever created
        if (inputTensor != null)
        {
            inputTensor.Dispose();
            inputTensor = null;
        }

        if (rawTextureData.IsCreated)
        {
            rawTextureData.Dispose();
        }

        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
    }


    void Update()
    {
        deltaTime += Time.deltaTime;

        // ─── 1) if neither scheduling nor readback pending, kick off new inference ─────────
        if (!m_Scheduling && !inferencePending)
        {
            if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
                return;

            // convert into our reusable Texture2D
            var conv = new XRCpuImage.ConversionParams {
                inputRect        = new RectInt(0,0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(targetW, targetH),
                outputFormat     = TextureFormat.RGBA32,
                transformation   = XRCpuImage.Transformation.MirrorY
            };

            unsafe {
                cpuImage.Convert(
                    conv,
                    new System.IntPtr(rawTextureData.GetUnsafePtr()),
                    rawTextureData.Length
                );
            }


            cpuImage.Dispose();
            cameraTexture.Apply();

            TextureConverter.ToTensor(cameraTexture,
                                    inputTensor,   // <- existing field
                                    default);

            // start **iterable** schedule
            m_Schedule     = worker.ScheduleIterable(inputTensor);
            m_Scheduling   = true;
            deltaTime      = 0f;
        }

        // ─── 2) if we’re in the middle of scheduling, do k layers/frame ───────────────────────
        if (m_Scheduling)
        {
            int it = 0;
            bool hasMore = true;
            while (hasMore && it++ < k_LayersPerFrame)
                hasMore = m_Schedule.MoveNext();

            if (!hasMore)
            {
                // all layers scheduled → kick off async readback
                m_Scheduling     = false;
                inferencePending = true;

                outputTensor = worker.PeekOutput() as Tensor<float>;
                outputTensor.ReadbackRequest();
            }
            return; // don’t do the readback check in the same frame
        }

        // ─── 3) poll for readback completion ──────────────────────────────────────────────────
        if (inferencePending && outputTensor.IsReadbackRequestDone())
        {
            var results = outputTensor.DownloadToArray();
            outputTensor.Dispose();

            // apply your SH
            var sh = new SphericalHarmonicsL2();
            for (int c = 0; c < 3; ++c)
                for (int i = 0; i < 9; ++i)
                    sh[c, i] = results[i*3 + c];

            RenderSettings.ambientMode      = AmbientMode.Custom;
            RenderSettings.ambientProbe     = sh;
            RenderSettings.ambientIntensity = 1.0f;

            inferencePending = false;
        }
    }

    // stash the worker here
    Worker worker;
}
