using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAITTSHandler : MonoBehaviour
{
    [SerializeField] private string apiKey = "TU_API_KEY"; // Asigna la API key en el Inspector
    private AudioSource audioSource;

    private void Awake()
    {
        // Usamos o agregamos un AudioSource único en este GameObject
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // Método público para generar y reproducir el audio a partir de un texto
    public async Task GenerateAndPlaySpeechAsync(string text)
    {
        try
        {
            // Generamos el audio desde OpenAI (usando UnityWebRequest)
            string audioUrl = await GenerateAudioFromText(text);

            // Cargamos el audio generado usando UnityWebRequest
            using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG))
            {
                await audioRequest.SendWebRequest();

                if (audioRequest.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(audioRequest);
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log("Reproduciendo audio TTS.");
                }
                else
                {
                    Debug.LogError("Error al cargar el audio TTS: " + audioRequest.error);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al generar o reproducir el audio TTS: " + ex.Message);
        }
    }

    private async Task<string> GenerateAudioFromText(string text)
    {
        string ttsApiUrl = "https://api.openai.com/v1/audio/speech";
        string jsonBody = $"{{\"model\":\"gpt-4o-mini-tts\",\"input\":\"{text}\",\"voice\":\"alloy\"}}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        UnityWebRequest request = new UnityWebRequest(ttsApiUrl, "POST");
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Audio recibido de OpenAI TTS");
            return "file://" + Application.persistentDataPath + "/audio.mp3";  // Guarda el archivo temporalmente
        }
        else
        {
            throw new Exception("Error al generar el audio: " + request.error);
        }
    }
}
