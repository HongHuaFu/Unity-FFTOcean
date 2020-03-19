using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FinalOcean : MonoBehaviour
{
    public ComputeShader csFFT;

    
    public int N = 512;
    public float Amp = 1.0f;
    public Vector4 Windxz;
    public float Len = 512;
    public float TimeScale = 1.0f;

    [SerializeField]
    private RenderTexture rt_spectrumInit;
    [SerializeField]
    private RenderTexture rt_dxdzSpectrum;
    [SerializeField]
    private RenderTexture rt_heightSpectrum;

    [SerializeField]
    private RenderTexture rt_Tmp;

    private float m_time = 0.0f;
    private void Start()
    {
        CreateAllRT();
        InitCSParams();
        InitSpectrum();
    }

    private void InitSpectrum()
    {
        csFFT.SetTexture(csFFT.FindKernel("SpectrumInit"), "SpectrumInitOut", rt_spectrumInit);
        csFFT.Dispatch(csFFT.FindKernel("SpectrumInit"), N / 8, N / 8, 1);
    }

    private void CreateAllRT()
    {
        rt_spectrumInit = CreateRT(N);
        rt_dxdzSpectrum = CreateRT(N);
        rt_heightSpectrum = CreateRT(N);
        rt_Tmp = CreateRT(N);
    }

    private void Update()
    {
        m_time += Time.deltaTime* TimeScale;
        csFFT.SetFloat("Time", m_time);
        csFFT.SetTexture(csFFT.FindKernel("SpectrumUpdate"), "HeightSpectrumUpdateOut", rt_heightSpectrum);
        csFFT.SetTexture(csFFT.FindKernel("SpectrumUpdate"), "DxDzSpectrumUpdateOut", rt_dxdzSpectrum);
        csFFT.SetTexture(csFFT.FindKernel("SpectrumUpdate"), "SpectrumInitOut", rt_spectrumInit);
        csFFT.Dispatch(csFFT.FindKernel("SpectrumUpdate"), N / 8, N / 8, 1);

        

        for (int i = 1; i <= N; i++)
        {
            int ns = (int)Mathf.Pow(2, i - 1);
            csFFT.SetInt("ns", ns);
            if (i != N)
            {
                int t = csFFT.FindKernel("FFTH");
                RunFFT(t, ref rt_heightSpectrum);
                RunFFT(t, ref rt_dxdzSpectrum);
            }
            else
            {
                int t = csFFT.FindKernel("FFTHE");
                RunFFT(t, ref rt_heightSpectrum);
                RunFFT(t, ref rt_dxdzSpectrum);
            }
        }

        for (int i = 1; i <= N; i++)
        {
            int ns = (int)Mathf.Pow(2, i - 1);
            csFFT.SetInt("ns", ns);
            if (i != N)
            {
                int t = csFFT.FindKernel("FFTV");
                RunFFT(t, ref rt_heightSpectrum);
                RunFFT(t, ref rt_dxdzSpectrum);
            }
            else
            {
                int t = csFFT.FindKernel("FFTVE");
                RunFFT(t, ref rt_heightSpectrum);
                RunFFT(t, ref rt_dxdzSpectrum);
            }
        }
    }

    private void RunFFT(int kernel, ref RenderTexture input)
    {
        csFFT.SetTexture(kernel, "Input", input);
        csFFT.SetTexture(kernel, "Result", rt_Tmp);
        csFFT.Dispatch(kernel, N / 8, N / 8, 1);

        RenderTexture rt = input;
        input = rt_Tmp;
        rt_Tmp = rt;
    }

    private void InitCSParams()
    {
        csFFT.SetInt("N", N);
        csFFT.SetFloat("Amp", Amp);
        csFFT.SetFloat("Len", Len);
        csFFT.SetVector("Wind", Windxz);
    }


    private RenderTexture CreateRT(int size)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }
}
