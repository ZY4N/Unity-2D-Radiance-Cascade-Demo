using System;
using UnityEngine;

public class RayCasterT3D : RayCaster {

	private const string m_ShaderPath = "Shaders/RadianceCascade/T3D/";

	private struct RadianceMap {
		public RenderTexture texture;
		public Vector3Int threadGroupCount;
		public int scale;
		public float rayOffset;
		public float stepSize;
		public int quarterSampleCount;

		public RadianceMap(
			RenderTexture texture,
			Vector3Int threadGroupCount,
			int scale,
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

	private Vector3Int m_litSceneThreadGroupCount;
	private RadianceMap[] m_radianceMaps;

	protected override string shaderPath {
		get {
            return m_ShaderPath;
        }
	}

    protected override void InitializeRadianceMaps(RadianceLayerInfo[] layerInfos) {

		var layerCount = determineMaxLayerCount(layerInfos);
		m_radianceMaps = new RadianceMap[layerCount];

		var radianceScale = renderScale;

		for (int i = 0; i != m_radianceMaps.Length; ++i) {
            ref var info = ref layerInfos[i];	
			m_radianceMaps[i] = new(
				CreateRenderTexture3D(info.resolution.x, info.resolution.y, info.resolution.z),
				CalcWorkingGroupSize(m_creationShader, m_creationKernel, info.resolution.x, info.resolution.y, info.resolution.z),
				radianceScale,
				info.rayOffset,
				info.stepSize,
				info.quarterSampleCount
			);
			radianceScale *= 2;
		}
		
		m_finalizationShader.SetTexture(m_finalizationKernel, "radianceMap", m_radianceMaps[0].texture);
	}

	private int determineMaxLayerCount(RadianceLayerInfo[] layerInfos) {
		int maxTexture3DSize = SystemInfo.maxTexture3DSize;

		int layerCount = 0;
		while (layerCount != layerInfos.Length) {
			ref var info = ref layerInfos[layerCount];
			if (
				info.resolution.x > maxTexture3DSize ||
				info.resolution.y > maxTexture3DSize ||
				info.resolution.z > maxTexture3DSize
			) {
				Debug.Log($"Removing layer {layerCount}: the layers resolution ({info.resolution}), axceedds the maximum allowed resolution of {maxTexture3DSize}.");
				break;
			}
			++layerCount;
		}
		
		return layerCount;
	}

	private RenderTexture CreateRenderTexture3D(int width, int height, int depth) {

		var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

		rt.enableRandomWrite = true;
		rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		rt.volumeDepth = depth;
		rt.Create();

		return rt;
	}

	protected override void CreateRadianceCascade() {
		for (int i = 0; i != m_radianceMaps.Length; ++i) {
			ref var radianceMap = ref m_radianceMaps[i];
			m_creationShader.SetTexture(m_creationKernel, m_radianceMapID, radianceMap.texture);
			m_creationShader.SetFloat(m_renderScaleID, radianceMap.scale);
			m_creationShader.SetFloat(m_rayOffsetID, radianceMap.rayOffset);
			m_creationShader.SetFloat(m_stepSizeID, radianceMap.stepSize);
			m_creationShader.SetInt(m_samplesCountID, radianceMap.quarterSampleCount);
			m_creationShader.Dispatch(
				m_creationKernel,
				radianceMap.threadGroupCount.x,
				radianceMap.threadGroupCount.y,
				radianceMap.threadGroupCount.z
			);
		}
	}

	protected override void MergeRadianceCascade() {
		for (int i = m_radianceMaps.Length - 1; i > 0; --i) {
			ref var farRadianceMap = ref m_radianceMaps[i];
			ref var nearRadianceMap = ref m_radianceMaps[i - 1];
			m_mergingShader.SetTexture(m_mergingKernel, m_farRadianceMapID, farRadianceMap.texture);
			m_mergingShader.SetTexture(m_mergingKernel, m_nearRadianceMapID, nearRadianceMap.texture);
			m_mergingShader.Dispatch(
				m_mergingKernel,
				nearRadianceMap.threadGroupCount.x,
				nearRadianceMap.threadGroupCount.y,
				nearRadianceMap.threadGroupCount.z
			);
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
