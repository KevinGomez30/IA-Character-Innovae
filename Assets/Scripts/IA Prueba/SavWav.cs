using System.IO;
using UnityEngine;

public static class SavWav
{
    public static void Save(string filename, AudioClip clip, Stream stream)
    {
        int frequency = clip.frequency;
        int channels = clip.channels;
        float[] samples = new float[clip.samples * channels];
        clip.GetData(samples, 0);

        byte[] wav = ConvertAudioClipToWav(samples, clip.samples, channels, frequency);
        stream.Write(wav, 0, wav.Length);
    }

    private static byte[] ConvertAudioClipToWav(float[] samples, int sampleCount, int channels, int frequency)
    {
        MemoryStream stream = new MemoryStream();

        // WAV header
        stream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
        stream.Write(System.BitConverter.GetBytes(36 + sampleCount * 2), 0, 4);
        stream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);
        stream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
        stream.Write(System.BitConverter.GetBytes(16), 0, 4); // Subchunk1Size
        stream.Write(System.BitConverter.GetBytes((ushort)1), 0, 2); // PCM format
        stream.Write(System.BitConverter.GetBytes((ushort)channels), 0, 2);
        stream.Write(System.BitConverter.GetBytes(frequency), 0, 4);
        stream.Write(System.BitConverter.GetBytes(frequency * channels * 2), 0, 4);
        stream.Write(System.BitConverter.GetBytes((ushort)(channels * 2)), 0, 2);
        stream.Write(System.BitConverter.GetBytes((ushort)16), 0, 2); // bits per sample

        // data subchunk
        stream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
        stream.Write(System.BitConverter.GetBytes(sampleCount * 2), 0, 4);

        // Convert float samples to 16-bit PCM
        for (int i = 0; i < samples.Length; i++)
        {
            short intData = (short)(samples[i] * short.MaxValue);
            byte[] bytesData = System.BitConverter.GetBytes(intData);
            stream.Write(bytesData, 0, 2);
        }

        return stream.ToArray();
    }
}