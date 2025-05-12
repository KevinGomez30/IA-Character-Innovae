using UnityEngine;
using Unity.WebRTC;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Net.WebSockets;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using System.IO;
using System.Net;

public class WebRTCVoiceChat : MonoBehaviour
{
    private RTCPeerConnection peerConnection;
    private AudioStreamTrack audioTrack;
    private RTCDataChannel dataChannel;
    private AudioSource audioSource;
    private ClientWebSocket ws;
    private bool isConnected = false;

    [Header("Configuración de OpenAI")]
    string apiKey = SecretsLoader.Secrets.OPENAI_API_KEY;
    //private string apiKey = "API-KEY";
    [SerializeField] private string whisperApiUrl = "https://api.openai.com/v1/audio/transcriptions";
    [SerializeField] private string gptApiUrl = "https://api.openai.com/v1/chat/completions"; // URL para GPT-4o

    [Header("UI")]
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private Button sendAudioButton;

    private AudioClip recordedAudio;

    private async void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        await ConnectWebSocket();
        SetupPeerConnection();
        sendAudioButton.onClick.AddListener(OnSendAudioButtonClick);
    }

    private async Task ConnectWebSocket()
    {
        string serverUrl = "ws://localhost:8080";
        ws = new ClientWebSocket();

        try
        {
            await ws.ConnectAsync(new System.Uri(serverUrl), System.Threading.CancellationToken.None);
            Debug.Log("Conectado al WebSocket.");
            isConnected = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error al conectar: {ex.Message}");
        }
    }

    private void SetupPeerConnection()
    {
        RTCConfiguration config = new RTCConfiguration
        {
            iceServers = new RTCIceServer[] { new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } } }
        };

        peerConnection = new RTCPeerConnection(ref config);
        peerConnection.OnTrack += OnTrackReceived;

        dataChannel = peerConnection.CreateDataChannel("chat");
        dataChannel.OnMessage += OnDataMessageReceived;

        StartCoroutine(CaptureMicrophoneAudio());
    }

    private IEnumerator CaptureMicrophoneAudio()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.LogError("No se concedió el permiso para el micrófono");
            yield break;
        }

        string microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (microphoneDevice == null)
        {
            Debug.LogError("No se encontró un dispositivo de micrófono");
            yield break;
        }

        recordedAudio = Microphone.Start(microphoneDevice, true, 10, 48000);
        yield return new WaitUntil(() => Microphone.GetPosition(microphoneDevice) > 0);
        audioSource.clip = recordedAudio;
        audioSource.loop = true;
        audioSource.Play();

        audioTrack = new AudioStreamTrack(audioSource);
        peerConnection.AddTrack(audioTrack);
    }

    private void OnSendAudioButtonClick()
    {
        if (recordedAudio != null)
        {
            byte[] audioData = WavUtility.FromAudioClip(recordedAudio);
            StartCoroutine(SendAudioToWhisperAPI(audioData));
        }
        else
        {
            Debug.LogError("No hay audio grabado para enviar.");
        }
    }

    private byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            byte[] wavData = WavUtility.FromAudioClip(clip);
            stream.Write(wavData, 0, wavData.Length);
            return stream.ToArray();
        }
    }

    private IEnumerator SendAudioToWhisperAPI(byte[] audioData)
    {
        // Usa la apiKey y url correspondientes
        string apiUrl = whisperApiUrl;

        // Crear la solicitud HTTP para Whisper
        WWWForm form = new WWWForm();
        form.AddField("model", "whisper-1");

        // Agregar el archivo de audio como WAV
        form.AddBinaryData("file", audioData, "audio.wav", "audio/wav");

        UnityWebRequest request = UnityWebRequest.Post(apiUrl, form);
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Transcripción recibida: " + request.downloadHandler.text);
            // Parseamos la transcripción
            string transcription = ParseTranscriptionFromWhisperResponse(request.downloadHandler.text);
            // Enviamos la transcripción a GPT-4o
            StartCoroutine(SendToGPT4o(transcription));
        }
        else
        {
            Debug.LogError("Error al enviar el audio a Whisper: " + request.responseCode + " " + request.error);
        }
    }

    private string ParseTranscriptionFromWhisperResponse(string jsonResponse)
    {
        var json = JsonUtility.FromJson<WhisperResponse>(jsonResponse);
        return json.text;
    }

    private IEnumerator SendToGPT4o(string transcription)
    {
        // Construimos el cuerpo de la solicitud con la transcripción recibida
        ChatRequest chatRequest = new ChatRequest("gpt-4o", new List<ChatMessage> { new ChatMessage("user", transcription) });
        string jsonBody = JsonUtility.ToJson(chatRequest);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        UnityWebRequest request = new UnityWebRequest(gptApiUrl, "POST");
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Respuesta de GPT-4o: " + request.downloadHandler.text);
            OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(request.downloadHandler.text);

            if (response.choices != null && response.choices.Length > 0)
            {
                // Actualizamos la UI con la respuesta del modelo
                chatText.text = response.choices[0].message.content;
            }
            else
            {
                Debug.LogError("No se recibió respuesta válida de GPT-4o.");
            }
        }
        else
        {
            Debug.LogError("Error al enviar solicitud a GPT-4o: " + request.responseCode + " " + request.error);
        }
    }

    private void OnTrackReceived(RTCTrackEvent e)
    {
        if (e.Track.Kind == TrackKind.Audio)
        {
            Debug.Log("Track recibido: " + e.Track.Id);
        }
    }

    private void OnDataMessageReceived(byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        Debug.Log("Mensaje de datos recibido: " + message);
    }

    [System.Serializable]
    public class WhisperResponse
    {
        public string text;
    }

    // Clases de soporte para la comunicación con GPT-4o
    [System.Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [System.Serializable]
    public class ChatRequest
    {
        public string model;
        public List<ChatMessage> messages;

        public ChatRequest(string model, List<ChatMessage> messages)
        {
            this.model = model;
            this.messages = messages;
        }
    }

    [System.Serializable]
    private class OpenAIResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public Message message;
    }

    [System.Serializable]
    private class Message
    {
        public string role;
        public string content;
    }
}