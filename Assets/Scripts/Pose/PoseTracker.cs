using System;
using System.Globalization;
using Unity.InferenceEngine;
using UnityEngine;

namespace PostureCheck
{
    /// <summary>One body point. Position is in <see cref="FrontCamera.Frame"/> pixels, y up.</summary>
    public struct Landmark
    {
        public Vector2 position;
        /// <summary>Depth relative to the hips, roughly in the same pixel units. Negative = closer.</summary>
        public float depth;
        /// <summary>0..1: how likely the point is visible (not hidden behind the body).</summary>
        public float visibility;
        /// <summary>0..1: how likely the point is inside the image.</summary>
        public float presence;
    }

    /// <summary>
    /// Runs BlazePose (pose detector + landmark model, from MediaPipe) on the front camera with
    /// Unity Inference Engine and exposes the 33 landmarks (MediaPipe numbering).
    /// Two stages per camera frame:
    /// 1. The detector finds the person and two keypoints (hip centre and a point that sets size and tilt).
    /// 2. The landmark model runs on a crop rotated and scaled from those keypoints.
    /// </summary>
    public class PoseTracker : MonoBehaviour
    {
        public const int LandmarkCount = 33;

        [SerializeField] FrontCamera frontCamera;
        [SerializeField] ModelAsset detectorModel;
        [Tooltip("pose_landmarks_detector_lite (fast) or _full (more accurate).")]
        [SerializeField] ModelAsset landmarkerModel;
        [SerializeField] TextAsset anchorsCsv;
        [SerializeField] ComputeShader imageToTensor;
        [Range(0, 1)] [SerializeField] float detectionThreshold = 0.5f;

        /// <summary>Latest landmarks. Valid only while <see cref="HasPose"/> is true.</summary>
        public Landmark[] Landmarks { get; } = new Landmark[LandmarkCount];
        public bool HasPose { get; private set; }
        /// <summary>Detector confidence of the latest frame (0..1).</summary>
        public float DetectionScore { get; private set; }
        /// <summary>Time from submitting a frame to having its landmarks, in milliseconds.</summary>
        public float LatencyMs { get; private set; }
        /// <summary>Increases by one after every processed camera frame.</summary>
        public int Version { get; private set; }

        const int DetectorSize = 224;
        const int LandmarkerSize = 256;
        const int AnchorCount = 2254;
        const float CropScale = 1.25f; // crop radius relative to hip-to-scale-point distance (MediaPipe)

        Worker m_Detector;
        Worker m_Landmarker;
        Tensor<float> m_DetectorInput;
        Tensor<float> m_LandmarkerInput;
        Vector2[] m_Anchors;
        int m_Kernel;

        static readonly int s_Source = Shader.PropertyToID("_Source");
        static readonly int s_Output = Shader.PropertyToID("_Output");
        static readonly int s_OutWidth = Shader.PropertyToID("_OutWidth");
        static readonly int s_OutHeight = Shader.PropertyToID("_OutHeight");
        static readonly int s_SourceSize = Shader.PropertyToID("_SourceSize");
        static readonly int s_Row0 = Shader.PropertyToID("_Row0");
        static readonly int s_Row1 = Shader.PropertyToID("_Row1");

        async void Start()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError("PoseTracker: this device has no compute shader support.");
                enabled = false;
                return;
            }

            Load();
            var token = destroyCancellationToken;
            int lastFrame = -1;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var frame = frontCamera.Frame;
                    if (frame == null || frontCamera.FrameCount == lastFrame)
                    {
                        await Awaitable.NextFrameAsync(token);
                        continue;
                    }
                    lastFrame = frontCamera.FrameCount;
                    float t0 = Time.realtimeSinceStartup;
                    await Track(frame);
                    LatencyMs = (Time.realtimeSinceStartup - t0) * 1000f;
                    Version++;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                m_Detector?.Dispose();
                m_Landmarker?.Dispose();
                m_DetectorInput?.Dispose();
                m_LandmarkerInput?.Dispose();
            }
        }

        void Load()
        {
            m_Anchors = ParseAnchors(anchorsCsv.text);
            m_Kernel = imageToTensor.FindKernel("CSMain");

            // Add the "pick the best detection" step to the detector graph, so only one box is read back.
            var detector = ModelLoader.Load(detectorModel);
            var graph = new FunctionalGraph();
            var input = graph.AddInput(detector, 0);
            var outputs = Functional.Forward(detector, input);
            var rawBoxes = outputs[0];   // (1, 2254, 12): box (4) + 4 keypoints (8), anchor-relative
            var rawScores = outputs[1];  // (1, 2254, 1): logits
            var best = Functional.ArgMax(rawScores, 1).Squeeze();
            var bestScore = Functional.IndexSelect(Functional.Sigmoid(Functional.Clamp(rawScores, -100f, 100f)), 1, best).Unsqueeze(0);
            var bestBox = Functional.IndexSelect(rawBoxes, 1, best).Unsqueeze(0);
            m_Detector = new Worker(graph.Compile(best, bestScore, bestBox), BackendType.GPUCompute);

            m_Landmarker = new Worker(ModelLoader.Load(landmarkerModel), BackendType.GPUCompute);

            m_DetectorInput = new Tensor<float>(new TensorShape(1, DetectorSize, DetectorSize, 3));
            m_LandmarkerInput = new Tensor<float>(new TensorShape(1, LandmarkerSize, LandmarkerSize, 3));
        }

        async Awaitable Track(Texture frame)
        {
            // Stage 1: detector on the whole frame, letterboxed into a square.
            var frameSize = new Vector2(frame.width, frame.height);
            float scale = Mathf.Max(frameSize.x, frameSize.y) / DetectorSize;
            // Tensor pixels (y down) -> frame pixels (y up), centred.
            var toFrame = Affine2.Translate(frameSize * 0.5f)
                          * Affine2.Scale(scale, -scale)
                          * Affine2.Translate(new Vector2(-DetectorSize, -DetectorSize) * 0.5f);
            Sample(frame, m_DetectorInput, toFrame);
            m_Detector.Schedule(m_DetectorInput);

            var idxTask = (m_Detector.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
            var scoreTask = (m_Detector.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
            var boxTask = (m_Detector.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();
            using var idx = await idxTask;
            using var score = await scoreTask;
            using var box = await boxTask;

            DetectionScore = score[0];
            if (DetectionScore < detectionThreshold)
            {
                HasPose = false;
                return;
            }

            var anchor = m_Anchors[idx[0]] * DetectorSize;
            Vector2 Keypoint(int k) => toFrame.Apply(anchor + new Vector2(box[0, 0, 4 + 2 * k], box[0, 0, 5 + 2 * k]));

            // Stage 2: crop around the hips, rotated so the body points up, and run the landmark model.
            var hips = Keypoint(0);
            var toScalePoint = Keypoint(1) - hips;
            float radius = CropScale * toScalePoint.magnitude;
            float tilt = Mathf.Atan2(toScalePoint.y, toScalePoint.x);
            float cropScale = radius / (0.5f * LandmarkerSize);
            var cropToFrame = Affine2.Translate(hips)
                              * Affine2.Scale(cropScale, -cropScale)
                              * Affine2.Rotate(0.5f * Mathf.PI - tilt)
                              * Affine2.Translate(new Vector2(-LandmarkerSize, -LandmarkerSize) * 0.5f);
            Sample(frame, m_LandmarkerInput, cropToFrame);
            m_Landmarker.Schedule(m_LandmarkerInput);

            using var raw = await (m_Landmarker.PeekOutput("Identity") as Tensor<float>).ReadbackAndCloneAsync(); // (1, 195): 39 x (x, y, z, visibility, presence)
            for (int i = 0; i < LandmarkCount; i++)
            {
                Landmarks[i] = new Landmark
                {
                    position = cropToFrame.Apply(new Vector2(raw[5 * i], raw[5 * i + 1])),
                    depth = raw[5 * i + 2] * cropScale,
                    visibility = Sigmoid(raw[5 * i + 3]),
                    presence = Sigmoid(raw[5 * i + 4]),
                };
            }
            HasPose = true;
        }

        void Sample(Texture source, Tensor<float> target, Affine2 tensorToSource)
        {
            var data = ComputeTensorData.Pin(target);
            int w = target.shape[2], h = target.shape[1];
            imageToTensor.SetTexture(m_Kernel, s_Source, source);
            imageToTensor.SetBuffer(m_Kernel, s_Output, data.buffer);
            imageToTensor.SetInt(s_OutWidth, w);
            imageToTensor.SetInt(s_OutHeight, h);
            imageToTensor.SetVector(s_SourceSize, new Vector4(source.width, source.height));
            imageToTensor.SetVector(s_Row0, tensorToSource.Row0);
            imageToTensor.SetVector(s_Row1, tensorToSource.Row1);
            imageToTensor.Dispatch(m_Kernel, (w + 7) / 8, (h + 7) / 8, 1);
        }

        static float Sigmoid(float x) => 1f / (1f + Mathf.Exp(-x));

        static Vector2[] ParseAnchors(string csv)
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < AnchorCount) throw new FormatException($"anchors.csv has {lines.Length} rows, expected {AnchorCount}");
            var anchors = new Vector2[AnchorCount];
            for (int i = 0; i < AnchorCount; i++)
            {
                var v = lines[i].Split(',');
                anchors[i] = new Vector2(float.Parse(v[0], CultureInfo.InvariantCulture),
                                         float.Parse(v[1], CultureInfo.InvariantCulture));
            }
            return anchors;
        }
    }
}
