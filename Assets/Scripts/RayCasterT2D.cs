using System;
using UnityEngine;

public class RayCasterT2D : RayCaster {

    private const string m_ShaderPath = "Shaders/RadianceCascade/T2D/";
	private static readonly int m_probeSpacingID = Shader.PropertyToID("probeSpacing");
	private static readonly int m_angularResID = Shader.PropertyToID("angularRes");
	private static readonly int m_nearProbeSpacingID = Shader.PropertyToID("nearProbeSpacing");
	private static readonly int m_nearAngularResID = Shader.PropertyToID("nearAngularRes");
	private static readonly int m_farProbeSpacingID = Shader.PropertyToID("farProbeSpacing");
	private static readonly int m_farAngularResID = Shader.PropertyToID("farAngularRes");

    private struct RadianceMap {
		public RenderTexture texture;
        public int probeSpacing;
        public int angularRes;
		public float rayOffset;
		public float stepSize;
		public int quarterSampleCount;

		public RadianceMap(
			RenderTexture texture,
			int probeSpacing,
			int angularRes,
			float rayOffset,
			float stepSize,
			int quarterSampleCount
		) {
			this.texture = texture;
			this.probeSpacing = probeSpacing;
			this.angularRes = angularRes;
			this.rayOffset = rayOffset;
			this.stepSize = stepSize;
			this.quarterSampleCount = quarterSampleCount;
		}
	};

    private Vector3Int m_radianceGroupCount;
	private RadianceMap[] m_radianceMaps;

    protected override string shaderPath {
		get {
            return m_ShaderPath;
        }
	}

    protected override void InitializeRadianceMaps(RadianceLayerInfo[] layerInfos) {

		m_radianceMaps = new RadianceMap[layerInfos.Length];

        var maxProbeSpacing = 1 << layerInfos.Length;

        var radianceMapRes = new Vector2Int(
			(m_litScene.width * 2 + maxProbeSpacing - 1) / maxProbeSpacing,
			(m_litScene.height * 2 + maxProbeSpacing - 1) / maxProbeSpacing
		) * maxProbeSpacing;

        m_radianceGroupCount = CalcWorkingGroupSize(m_creationShader, m_creationKernel, radianceMapRes.x, radianceMapRes.y, 1);

		for (int i = 0; i != layerInfos.Length; ++i) {
            ref var info = ref layerInfos[i];
			m_radianceMaps[i] = new(
				CreateRenderTexture2D(radianceMapRes.x, radianceMapRes.y),
				info.probeSpacing,
				info.resolution.z,
				info.rayOffset,
				info.stepSize,
				info.quarterSampleCount
			);
		}
		
		m_creationShader.SetInt(m_renderScaleID, renderScale / 2);
		m_finalizationShader.SetTexture(m_finalizationKernel, "radianceMap", m_radianceMaps[0].texture);

	}

    static RenderTexture CreateRenderTexture2D(int width, int height) {

		var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

		rt.enableRandomWrite = true;
		rt.Create();

		return rt;
	}

	protected override void CreateRadianceCascade() {
		for (int i = 0; i != m_radianceMaps.Length; ++i) {
			ref var radianceMap = ref m_radianceMaps[i];
			m_creationShader.SetTexture(m_creationKernel, m_radianceMapID, radianceMap.texture);
            m_creationShader.SetInt(m_probeSpacingID, radianceMap.probeSpacing);
			m_creationShader.SetInt(m_angularResID, radianceMap.angularRes);
            m_creationShader.SetFloat(m_rayOffsetID, radianceMap.rayOffset);
			m_creationShader.SetFloat(m_stepSizeID, radianceMap.stepSize);
			m_creationShader.SetInt(m_samplesCountID, radianceMap.quarterSampleCount);
			m_creationShader.Dispatch(m_creationKernel, m_radianceGroupCount.x, m_radianceGroupCount.y, 1);
		}
	}

	protected override void MergeRadianceCascade() {
		for (int i = m_radianceMaps.Length - 1; i > 0; --i) {
			var farRadianceMap = m_radianceMaps[i];
			var nearRadianceMap = m_radianceMaps[i - 1];
			m_mergingShader.SetTexture(m_mergingKernel, m_farRadianceMapID, farRadianceMap.texture);
			m_mergingShader.SetTexture(m_mergingKernel, m_nearRadianceMapID, nearRadianceMap.texture);
            m_mergingShader.SetInt(m_farProbeSpacingID, farRadianceMap.probeSpacing);
			m_mergingShader.SetInt(m_nearProbeSpacingID, nearRadianceMap.probeSpacing);
            m_mergingShader.SetInt(m_farAngularResID, farRadianceMap.angularRes);
			m_mergingShader.SetInt(m_nearAngularResID, nearRadianceMap.angularRes);
			m_mergingShader.Dispatch(m_mergingKernel, m_radianceGroupCount.x, m_radianceGroupCount.y, m_radianceGroupCount.z);
		}
	}

	protected override void FinalizeRadiance() {
		m_finalizationShader.Dispatch(
			m_finalizationKernel, 
			m_finalizationThreadGroupCount.x,
			m_finalizationThreadGroupCount.y,
			m_finalizationThreadGroupCount.z
		);
	}
}
