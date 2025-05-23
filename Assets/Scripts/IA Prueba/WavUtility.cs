using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * short.MaxValue);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        using var stream = new MemoryStream();
        BinaryWriter writer = new(stream);

        // WAV header
        writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + bytesData.Length);
        writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
        writer.Write(new char[4] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)clip.channels);
        writer.Write(clip.frequency);
        writer.Write(clip.frequency * clip.channels * 2);
        writer.Write((short)(clip.channels * 2));
        writer.Write((short)16);
        writer.Write(new char[4] { 'd', 'a', 't', 'a' });
        writer.Write(bytesData.Length);
        writer.Write(bytesData);

        return stream.ToArray();
    }

    public static AudioClip ChunkToClip(byte[] wavData)
    {
        using var stream = new MemoryStream(wavData);
        using var reader = new BinaryReader(stream);

        reader.ReadBytes(22);
        ushort channels = reader.ReadUInt16();
        int sampleRate = reader.ReadInt32();

        reader.ReadBytes(6);
        ushort bitsPerSample = reader.ReadUInt16();
        reader.ReadBytes(4);
        int dataSize = reader.ReadInt32();

        byte[] pcm = reader.ReadBytes(dataSize);
        float[] samples = new float[dataSize / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;

        AudioClip clip = AudioClip.Create("RealtimeClip", samples.Length / channels, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
