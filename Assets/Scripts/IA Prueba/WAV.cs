using System;
using UnityEngine;

public class WAV
{
    public float[] LeftChannel { get; private set; }
    public int SampleCount { get; private set; }
    public int Frequency { get; private set; }

    public WAV(byte[] wav)
    {
        // Extract frequency and samples
        Frequency = BitConverter.ToInt32(wav, 24);
        int pos = 44;
        int samples = (wav.Length - pos) / 2;
        SampleCount = samples;
        LeftChannel = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            short sample = BitConverter.ToInt16(wav, pos);
            LeftChannel[i] = sample / 32768.0f;
            pos += 2;
        }
    }
}
