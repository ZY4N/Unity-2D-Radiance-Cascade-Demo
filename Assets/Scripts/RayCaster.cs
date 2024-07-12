using System;
using UnityEngine;

public class RayCaster : MonoBehaviour {

    private struct RadianceMap {
        public RenderTexture texture;
        public Vector3Int threadGroupCount;
        public Vector2 scale;
        public float rayOffset;
        public float stepSize;
        public int quarterSampleCount;

        public RadianceMap(
            RenderTexture texture,
            Vector3Int threadGroupCount,
            Vector2 scale,
            float rayOffset,
            float stepSize,
            int quarterSampleCount
        ) {
            this.texture = texture;
            this.threadGroupCount = threadGroupCount;
            this.scale = scale;
            this.rayOffset = rayOffset;
            this.stepSize = stepSize;
            this.quarterSampleCount = quarterSampleCount;
        }
    };

    public int resolutionReduction = 2;

    public ComputeShader creationShader;
    public ComputeShader mergingShader;
    public ComputeShader finalizationShader;
    public Material compositingMaterial;

    private Camera m_camera;
    private Vector2Int m_emitterSceneRes;
    private Vector2Int m_litSceneRes;
    private Vector2 m_renderingScale;
    private Vector3Int m_litSceneThreadGroupCount;

    private RadianceMap[] m_radianceMaps;
    private RenderTexture m_litScene;

    private int m_creationKernel;
    private int m_emitterSceneID;
    private int m_radianceMapID;
    private int m_radianceScaleID;
    private int m_rayOffsetID;
    private int m_stepSizeID;
    private int m_samplesCountID;

    private int m_mergingKernel;
    private int m_farRadianceMapID;
    private int m_nearRadianceMapID;

    private int m_finalizationKernel;

    private int m_litSceneScaleID;

    void Awake() {
        m_camera = GetComponent<Camera>();
        SetupRenderTextures();
        SetupCreationShader();
        SetupRadianceMaps();
        SetupMergingShader();
        SetupFinilizationShader();
        SetupCompositingMaterial();
    }


    void SetupRenderTextures() {

        m_emitterSceneRes = new(m_camera.pixelWidth, m_camera.pixelHeight);
        m_litSceneRes = m_emitterSceneRes / resolutionReduction;
        m_renderingScale = (Vector2)m_emitterSceneRes / (Vector2)m_litSceneRes;

        m_litScene = CreateRenderTexture2D(m_litSceneRes.x, m_litSceneRes.y);
    }

    void SetupRadianceMaps() {

        var diagonalLength = m_litSceneRes.magnitude;
        var layerCount = (int)Math.Ceiling(Math.Log(diagonalLength, 4)) + 1;

        Debug.Log($"radiance cascade size: {layerCount}");

        m_radianceMaps = new RadianceMap[layerCount];

        var linearResolution = m_litSceneRes;
        var angularResolution = 4;
        var radianceScale = m_renderingScale;

        var rayOffset = 0.5f;
        var rayLength = 1.0f;

        uint kernelGroupsX, kernelGroupsY, kernelGroupsZ;
        creationShader.GetKernelThreadGroupSizes(
            m_creationKernel,
            out kernelGroupsX,
            out kernelGroupsY,
            out kernelGroupsZ
        );

        for (int i = 0; i != m_radianceMaps.Length; ++i) {
           
            var threadGroupCount = new Vector3Int(
                (linearResolution.x + (int)kernelGroupsX - 1) / (int)kernelGroupsX,
                (linearResolution.y + (int)kernelGroupsY - 1) / (int)kernelGroupsY,
                (angularResolution + (int)kernelGroupsZ - 1) / (int)kernelGroupsZ
            );

            var quarterSampleCount = Math.Min((int)Math.Ceiling(rayLength / 4), 64);
            var stepSize = rayLength / (quarterSampleCount * 4);

            m_radianceMaps[i] = new(
                CreateRenderTexture3D(linearResolution.x, linearResolution.y, angularResolution),
                threadGroupCount,
                radianceScale,
                rayOffset,
                stepSize,
                quarterSampleCount
            );

            Debug.Log($"Layer {i}: w: {linearResolution.x} h: {linearResolution.y} d: {angularResolution} threadCount: {threadGroupCount} radianceScale: {radianceScale} rayOffset: {rayOffset} rayLength: {rayLength} ");

            linearResolution = (linearResolution + new Vector2Int(1, 1)) / 2;
            angularResolution *= 4;
            radianceScale *= 2;
            rayOffset += rayLength;
            rayLength *= 4;
        }
    }
    private RenderTexture CreateRenderTexture2D(int width, int height) {

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

        rt.enableRandomWrite = true;
        rt.Create();

        return rt;
    }

     private RenderTexture CreateRenderTexture3D(int width, int height, int depth) {

        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

        rt.enableRandomWrite = true;
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rt.volumeDepth = depth;
        rt.Create();

        return rt;
    }

    void SetupCreationShader() {
        m_creationKernel = creationShader.FindKernel("RadianceMapRayTracer");

        m_emitterSceneID = Shader.PropertyToID("emitterScene");
        m_radianceMapID = Shader.PropertyToID("radianceMap");
        m_radianceScaleID = Shader.PropertyToID("radianceScale");
        m_rayOffsetID = Shader.PropertyToID("rayOffset");
        m_stepSizeID = Shader.PropertyToID("stepSize");
        m_samplesCountID = Shader.PropertyToID("quarterSampleCount");
    }

    void SetupMergingShader() {
        m_mergingKernel = mergingShader.FindKernel("RadianceMapMergeKernel");

        m_farRadianceMapID = Shader.PropertyToID("farRadianceMap");
        m_nearRadianceMapID = Shader.PropertyToID("nearRadianceMap");
    }

    void SetupFinilizationShader() {
        m_finalizationKernel = finalizationShader.FindKernel("RadianceMapFinalizationKernel4x4");

        uint kernelGroupsX, kernelGroupsY, kernelGroupsZ;
        finalizationShader.GetKernelThreadGroupSizes(
            m_creationKernel,
            out kernelGroupsX,
            out kernelGroupsY,
            out kernelGroupsZ
        );

        m_litSceneThreadGroupCount = new Vector3Int(
            (m_litSceneRes.x + (int)kernelGroupsX - 1) / (int)kernelGroupsX,
            (m_litSceneRes.y + (int)kernelGroupsY - 1) / (int)kernelGroupsY,
            1
        );
        
        finalizationShader.SetTexture(m_finalizationKernel, "radianceMap", m_radianceMaps[0].texture);
        finalizationShader.SetTexture(m_finalizationKernel, "litScene", m_litScene);
    }

    void SetupCompositingMaterial() {
        m_litSceneScaleID = Shader.PropertyToID("_ForegroundScale");
        compositingMaterial.SetTexture("_ForegroundTex", m_litScene);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst) {

        creationShader.SetTexture(m_creationKernel, m_emitterSceneID, src);

        CreateRadianceCascade();
        MergeRadianceCascade();
        FinalizeRadiance();

        /*compositingMaterial.SetVector(m_litSceneScaleID, new Vector4(
            (float)m_litSceneRes.x / Screen.width,
            (float)m_litSceneRes.y / Screen.height
        ));

        Graphics.Blit(src, dst, compositingMaterial); */

        Graphics.Blit(m_litScene, dst);
    }

    private void CreateRadianceCascade() {
        for (int i = 0; i != m_radianceMaps.Length; ++i) {
            ref var radianceMap = ref m_radianceMaps[i];
            creationShader.SetTexture(m_creationKernel, m_radianceMapID, radianceMap.texture);
            creationShader.SetVector(m_radianceScaleID, new(radianceMap.scale.x, radianceMap.scale.y, 1.0f, 1.0f));
            creationShader.SetFloat(m_rayOffsetID, radianceMap.rayOffset);
            creationShader.SetFloat(m_stepSizeID, radianceMap.stepSize);
            creationShader.SetFloat(m_samplesCountID, radianceMap.quarterSampleCount);
            creationShader.Dispatch(
                m_creationKernel,
                radianceMap.threadGroupCount.x,
                radianceMap.threadGroupCount.y,
                radianceMap.threadGroupCount.z
            );
        }
    }

    private void MergeRadianceCascade() {
        for (int i = m_radianceMaps.Length - 1; i > 0; --i) {
            ref var farRadianceMap = ref m_radianceMaps[i];
            ref var nearRadianceMap = ref m_radianceMaps[i - 1];
            mergingShader.SetTexture(m_mergingKernel, m_farRadianceMapID, farRadianceMap.texture);
            mergingShader.SetTexture(m_mergingKernel, m_nearRadianceMapID, nearRadianceMap.texture);
            mergingShader.Dispatch(
                m_mergingKernel,
                nearRadianceMap.threadGroupCount.x,
                nearRadianceMap.threadGroupCount.y,
                nearRadianceMap.threadGroupCount.z
            );
        }
    }

    private void FinalizeRadiance() {
        finalizationShader.Dispatch(
            m_finalizationKernel, 
            m_litSceneThreadGroupCount.x,
            m_litSceneThreadGroupCount.y,
            m_litSceneThreadGroupCount.z
        );
    }
}
