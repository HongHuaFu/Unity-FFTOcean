using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class DebugTest : MonoBehaviour
{
    public Material mat_gaussianNoise;
    public Material mat_phillips;
    public Material mat_h0k;
    public Material mat_heightSpec;
    public Material mat_dxSpec;
    public Material mat_dySpec;
    public Material mat_fftHeight;
    public Material mat_fftDx;
    public Material mat_fftDy;
    public Material mat_displace;
    public Material mat_normalBubble;
    public Material mat_Ocean;

    public ComputeShader csGaussian;
    public ComputeShader csPhillips;
    public ComputeShader csH0k;
    public ComputeShader csComputeSpectrum;
    public ComputeShader csFFT;
    public ComputeShader csDisplace;
    public ComputeShader csNormalBubble;
    
    [Range(6, 14)]
    public int fftSampleTimes = 10;
    public float timeScale = 1;   
    public Vector2 wind;
    public float amp;
    public float oceanLenght = 10;
    public int oceanMeshSize = 200;
    public float bubblesThreshold = 1;
    public float bubblesScale = 1;
    public Vector3 displaceScale;


    private RenderTexture m_GaussianNoiseRT;
    private RenderTexture m_PhillipsRT;
    private RenderTexture m_H0kRT;
    private RenderTexture m_HeightSpectrumRT;
    private RenderTexture m_DxSpectrumRT;
    private RenderTexture m_DySpectrumRT;
    private RenderTexture m_FFTHeightRT;
    private RenderTexture m_FFTDxRT;
    private RenderTexture m_FFTDyRT;
    private RenderTexture m_pingpong;
    private RenderTexture m_displace;
    private RenderTexture m_normalBubbles;

    private int rtSize;
    private float time = 0.0f;

  

    private void Start()
    {
        rtSize =(int)Mathf.Pow(2, fftSampleTimes);
        CreateAllRenderTexture();
        BindAllRenderTextureToQaud();
        CreateGaussianNoise();
        CreateOceanMesh();
    }

    struct OceanMesh
    {
        public Mesh mesh;
        public MeshFilter filetr;
        public MeshRenderer render;
        public Vector3[] vertexs;
        public Vector2[] uvs;
        public int[] vertIndexs;
    }

    private GameObject[] oceanMeshs;
    private OceanMesh oceanMesh;
    private void CreateOceanMesh()
    {
        oceanMeshs = new GameObject[4];
        oceanMesh = new OceanMesh();
        oceanMesh.filetr = gameObject.AddComponent<MeshFilter>();
        oceanMesh.render = gameObject.AddComponent<MeshRenderer>();
        oceanMesh.mesh = new Mesh();
        oceanMesh.filetr.mesh = oceanMesh.mesh;
        oceanMesh.render.material = mat_Ocean;

        oceanMesh.vertIndexs = new int[(oceanMeshSize - 1) * (oceanMeshSize - 1) * 6];
        oceanMesh.vertexs = new Vector3[oceanMeshSize * oceanMeshSize];
        oceanMesh.uvs = new Vector2[oceanMeshSize * oceanMeshSize];

        int inx = 0;
        for (int i = 0; i < oceanMeshSize; i++)
        {
            for (int j = 0; j < oceanMeshSize; j++)
            {
                int index = i * oceanMeshSize + j;
                oceanMesh.vertexs[index] = new Vector3((j - oceanMeshSize / 2.0f) * oceanLenght / oceanMeshSize, 0, (i - oceanMeshSize / 2.0f) * oceanLenght / oceanMeshSize);
                oceanMesh.uvs[index] = new Vector2(j / (oceanMeshSize - 1.0f), i / (oceanMeshSize - 1.0f));

                if (i != oceanMeshSize - 1 && j != oceanMeshSize - 1)
                {
                    oceanMesh.vertIndexs[inx++] = index;
                    oceanMesh.vertIndexs[inx++] = index + oceanMeshSize;
                    oceanMesh.vertIndexs[inx++] = index + oceanMeshSize + 1;

                    oceanMesh.vertIndexs[inx++] = index;
                    oceanMesh.vertIndexs[inx++] = index + oceanMeshSize + 1;
                    oceanMesh.vertIndexs[inx++] = index + 1;
                }
            }
        }
        oceanMesh.mesh.vertices = oceanMesh.vertexs;
        oceanMesh.mesh.SetIndices(oceanMesh.vertIndexs, MeshTopology.Triangles, 0);
        oceanMesh.mesh.uv = oceanMesh.uvs;

        mat_Ocean.SetTexture("_Displace", m_displace);
        mat_Ocean.SetTexture("_NormalBubbles", m_normalBubbles);
    }

    private void Update()
    {
        time += Time.deltaTime * timeScale;
        ComputeAllUpdateCS();
    }

    private void ComputeAllUpdateCS()
    {
        ComputePhillips();
        ComputeH0k();
        ComputeSpectrum();
        IDFT();
        ComputeDisplace();
        ComputeNormalBubble();
    }

    private void ComputeNormalBubble()
    {
        csNormalBubble.SetInt("N", rtSize);
        csNormalBubble.SetFloat("OceanLen", oceanLenght);
        csNormalBubble.SetFloat("BubblesThreshold", bubblesThreshold);
        csNormalBubble.SetFloat("BubblesScale", bubblesScale);
        csNormalBubble.SetTexture(0, "Displace", m_displace);
        csNormalBubble.SetTexture(0, "NormalBubbles", m_normalBubbles);
        csNormalBubble.Dispatch(0, rtSize / 8, rtSize / 8, 1);
    }

    private void ComputeDisplace()
    {
        csDisplace.SetInt("N", rtSize);
        csDisplace.SetVector("scale", displaceScale);
        csDisplace.SetTexture(0, "FFTHeight", m_FFTHeightRT);
        csDisplace.SetTexture(0, "FFTDx", m_FFTDxRT);
        csDisplace.SetTexture(0, "FFTDy", m_FFTDyRT);
        csDisplace.SetTexture(0, "Result", m_displace);
        csDisplace.Dispatch(0, rtSize / 8, rtSize / 8, 1);
    }

    private void IDFT()
    {
        Graphics.CopyTexture(m_HeightSpectrumRT, m_FFTHeightRT);
        Graphics.CopyTexture(m_DxSpectrumRT, m_FFTDxRT);
        Graphics.CopyTexture(m_DySpectrumRT, m_FFTDyRT);

        csFFT.SetInt("N", rtSize);
        for (int i = 1; i <= fftSampleTimes; i++)
        {
            int ns = (int)Mathf.Pow(2, i - 1);
            csFFT.SetInt("ns", ns);
            if(i!= fftSampleTimes)
            {
                int t = csFFT.FindKernel("FFTH");
                RunFFT(t,ref m_FFTHeightRT);
                RunFFT(t, ref m_FFTDxRT);
                RunFFT(t, ref m_FFTDyRT);
            }
            else
            {
                int t = csFFT.FindKernel("FFTHE");
                RunFFT(t, ref m_FFTHeightRT);
                RunFFT(t, ref m_FFTDxRT);
                RunFFT(t, ref m_FFTDyRT);
            }
        }

        for (int i = 1; i <= fftSampleTimes; i++)
        {
            int ns = (int)Mathf.Pow(2, i - 1);
            csFFT.SetInt("ns", ns);
            if (i != fftSampleTimes)
            {
                int t = csFFT.FindKernel("FFTV");
                RunFFT(t, ref m_FFTHeightRT);
                RunFFT(t, ref m_FFTDxRT);
                RunFFT(t, ref m_FFTDyRT);
            }
            else
            {
                int t = csFFT.FindKernel("FFTVE");
                RunFFT(t, ref m_FFTHeightRT);
                RunFFT(t, ref m_FFTDxRT);
                RunFFT(t, ref m_FFTDyRT);
            }
        }
    }

    private void RunFFT(int kernel, ref RenderTexture input)
    {
        csFFT.SetTexture(kernel, "Input", input);
        csFFT.SetTexture(kernel, "Result", m_pingpong);
        csFFT.Dispatch(kernel, rtSize / 8, rtSize / 8, 1);

        RenderTexture rt = input;
        input = m_pingpong;
        m_pingpong = rt;
    }

    private void ComputeSpectrum()
    {
        csComputeSpectrum.SetInt("N", rtSize);
        csComputeSpectrum.SetFloat("Time", time);
        csComputeSpectrum.SetTexture(0, "H0k", m_H0kRT);
        csComputeSpectrum.SetTexture(0, "hResult", m_HeightSpectrumRT);
        csComputeSpectrum.SetTexture(0, "dxResult", m_DxSpectrumRT);
        csComputeSpectrum.SetTexture(0, "dyResult", m_DySpectrumRT);
        csComputeSpectrum.Dispatch(0, rtSize / 8, rtSize / 8, 1);
    }

    private void ComputeH0k()
    {
        csH0k.SetInt("N", rtSize);
        csH0k.SetTexture(0, "GaussianNoise", m_GaussianNoiseRT);
        csH0k.SetTexture(0, "Phillips", m_PhillipsRT);
        csH0k.SetTexture(0, "Result", m_H0kRT);
        csH0k.Dispatch(0, rtSize / 8, rtSize / 8, 1);
    }

    private void ComputePhillips()
    {
        Vector4 windd = new Vector4(wind.x, wind.y, 0, 0);
        csPhillips.SetInt("N", rtSize);
        csPhillips.SetVector("Wind", windd);
        csPhillips.SetFloat("Amp", amp);
        csPhillips.SetTexture(0,"Result", m_PhillipsRT);
        csPhillips.Dispatch(0, rtSize / 8, rtSize / 8, 1);
    }

    private void BindAllRenderTextureToQaud()
    {
        mat_gaussianNoise.SetTexture("_MainTex", m_GaussianNoiseRT);
        mat_phillips.SetTexture("_MainTex", m_PhillipsRT);
        mat_h0k.SetTexture("_MainTex", m_H0kRT);
        mat_dxSpec.SetTexture("_MainTex", m_DxSpectrumRT);
        mat_dySpec.SetTexture("_MainTex", m_DySpectrumRT);
        mat_heightSpec.SetTexture("_MainTex", m_HeightSpectrumRT);
        mat_fftHeight.SetTexture("_MainTex", m_FFTHeightRT);
        mat_fftDx.SetTexture("_MainTex", m_FFTDxRT);
        mat_fftDy.SetTexture("_MainTex", m_FFTDyRT);
        mat_displace.SetTexture("_MainTex", m_displace);
        mat_normalBubble.SetTexture("_MainTex", m_normalBubbles);
    }

    private void CreateGaussianNoise()
    {
        csGaussian.SetTexture(0, "Result", m_GaussianNoiseRT);
        csGaussian.SetInt("N", rtSize);
        csGaussian.Dispatch(0, rtSize / 8, rtSize / 8, 1);
    }

    private void CreateAllRenderTexture()
    {
        m_GaussianNoiseRT = CreateRT(rtSize);
        m_PhillipsRT = CreateRT(rtSize);
        m_H0kRT = CreateRT(rtSize);
        m_HeightSpectrumRT = CreateRT(rtSize);
        m_DxSpectrumRT = CreateRT(rtSize);
        m_DySpectrumRT = CreateRT(rtSize);
        m_FFTHeightRT = CreateRT(rtSize);
        m_FFTDyRT = CreateRT(rtSize); 
        m_FFTDxRT = CreateRT(rtSize); 
        m_pingpong = CreateRT(rtSize);
        m_displace = CreateRT(rtSize);
        m_normalBubbles = CreateRT(rtSize);
    }

    private RenderTexture CreateRT(int size)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }


}
