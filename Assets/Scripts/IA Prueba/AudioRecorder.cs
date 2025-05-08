using UnityEngine;
using UnityEngine.EventSystems;
using System.IO;
using System;

public class AudioRecorder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private string microphoneName;
    private AudioClip recording;
    private bool isRecording = false;
    public int sampleRate = 44100;  // Frecuencia de muestreo

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (Microphone.devices.Length > 0)
        {
            microphoneName = Microphone.devices[0];
            Debug.Log("Micrófono encontrado: " + microphoneName);
        }
        else
        {
            Debug.LogError("No se encontró un micrófono.");
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StartRecording();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopRecording();
        PlayAudio();
        SaveRecordingToFile();
    }

    void StartRecording()
    {
        if (isRecording) return;

        isRecording = true;
        recording = Microphone.Start(microphoneName, true, 10, sampleRate);
        Debug.Log("Grabación iniciada.");
    }

    void StopRecording()
    {
        if (!isRecording) return;

        int position = Microphone.GetPosition(microphoneName);
        Microphone.End(microphoneName);
        isRecording = false;

        if (position > 0)
        {
            float[] samples = new float[recording.samples * recording.channels];
            recording.GetData(samples, 0);

            AudioClip newClip = AudioClip.Create("RecordedAudio", position, recording.channels, recording.frequency, false);
            newClip.SetData(samples, 0);

            audioSource.clip = newClip;
            Debug.Log("Grabación detenida. Duración: " + newClip.length + " segundos.");
        }
        else
        {
            Debug.LogWarning("No se grabó audio válido.");
        }
    }

    void PlayAudio()
    {
        if (audioSource.clip != null)
        {
            audioSource.Play();
            Debug.Log("Reproduciendo audio grabado.");
        }
        else
        {
            Debug.LogWarning("No se ha grabado audio.");
        }
    }

    void SaveRecordingToFile()
    {
        if (audioSource.clip == null)
        {
            Debug.LogWarning("No hay audio para guardar.");
            return;
        }

        // Ruta personalizada (ajustala según tu necesidad)
        string directoryPath = "D:/Unity Proyects/Proyecto Innovae/AudioCharacter";  // Cambia esta ruta a donde quieras guardar el archivo
        string filePath = Path.Combine(directoryPath, "grabacion_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav");


        // Si el directorio no existe, créalo
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        byte[] wavData = ConvertAudioClipToWAV(audioSource.clip);
        File.WriteAllBytes(filePath, wavData);

        Debug.Log("Audio guardado en: " + filePath);
    }


    byte[] ConvertAudioClipToWAV(AudioClip clip)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            int sampleCount = clip.samples * clip.channels;
            int frequency = clip.frequency;
            float[] samples = new float[sampleCount];
            clip.GetData(samples, 0);

            byte[] wavData = new byte[sampleCount * 2];
            int index = 0;
            foreach (float sample in samples)
            {
                short sampleInt = (short)(sample * short.MaxValue);
                byte[] bytes = BitConverter.GetBytes(sampleInt);
                wavData[index++] = bytes[0];
                wavData[index++] = bytes[1];
            }

            WriteWAVHeader(stream, wavData.Length, sampleCount, clip.channels, frequency);
            stream.Write(wavData, 0, wavData.Length);

            return stream.ToArray();
        }
    }

    void WriteWAVHeader(Stream stream, int audioDataSize, int sampleCount, int channels, int frequency)
    {
        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + audioDataSize);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(audioDataSize);
        }
    }

    void OnApplicationQuit()
    {
        if (isRecording)
        {
            StopRecording();
        }
    }
}
