using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        using (MemoryStream stream = new MemoryStream()) // Asegurar que es escribible
        {
            WriteWavHeader(stream, clip);
            WriteWavData(stream, clip);
            return stream.ToArray();
        }
    }

    private static void WriteWavHeader(Stream stream, AudioClip clip)
    {
        int sampleCount = clip.samples * clip.channels;
        int fileSize = 44 + sampleCount * 2;

        BinaryWriter writer = new BinaryWriter(stream); // Eliminar `leaveOpen`
        writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
        writer.Write(fileSize - 8);
        writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)clip.channels);
        writer.Write(clip.frequency);
        writer.Write(clip.frequency * clip.channels * 2);
        writer.Write((ushort)(clip.channels * 2));
        writer.Write((ushort)16);
        writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
        writer.Write(sampleCount * 2);
    }

    private static void WriteWavData(Stream stream, AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) // Evitar cerrar el stream antes de tiempo
        {
            foreach (float sample in samples)
            {
                short sampleInt = (short)(sample * short.MaxValue);
                writer.Write(sampleInt);
            }
        }
    }

    internal static AudioClip ToAudioClip(byte[] wavData)
    {
        throw new NotImplementedException();
    }
}