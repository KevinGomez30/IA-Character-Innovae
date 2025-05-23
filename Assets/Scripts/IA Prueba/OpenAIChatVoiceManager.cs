using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class OpenAIChatVoiceManager : MonoBehaviour
{
    [Header("OpenAI Settings")]
    private string apiKey;
    public string whisperEndpoint = "https://api.openai.com/v1/audio/transcriptions";
    public string chatEndpoint = "https://api.openai.com/v1/chat/completions";
    public string gptRealtimeWsUrl = "wss://api.openai.com/v1/realtime";

    [Header("UI")]
    public TMP_InputField userInputField;
    public TMP_Text chatDisplay;

    [Header("Audio")]
    public AudioSource playbackSource;
    public AudioClip recordedClip;
    private string microphoneDevice;

    private ClientWebSocket realtimeSocket;
    private CancellationTokenSource realtimeCts;

    void Start()
    {
        apiKey = SecretsLoader.GetSecrets().OPENAI_API_KEY;

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("OpenAI API Key no esta en secrets.json.");
        }

        microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (microphoneDevice == null)
            Debug.LogWarning("No microphone found.");
    }

    #region CHAT

    public void OnSendTextToGPT()
    {
        string userText = userInputField.text;
        if (!string.IsNullOrEmpty(userText))
        {
            AppendToChat("Tú", userText);
            userInputField.text = "";
            _ = SendTextToGPT(userText);
        }
    }

    private async Task SendTextToGPT(string userText)
    {
        var messageData = new
        {
            model = "gpt-4o",
            messages = new object[]
            {
                new { role = "user", content = userText }
            }
        };

        string jsonData = JsonUtility.ToJson(messageData);
        using var request = new UnityWebRequest(chatEndpoint, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            string content = ExtractContentFromJson(result);
            AppendToChat("Bot", content);
        }
        else
        {
            AppendToChat("Error", request.error);
        }
    }

    private string ExtractContentFromJson(string json)
    {
        int index = json.IndexOf("\"content\":\"");
        if (index == -1) return "Respuesta no encontrada";
        int start = index + 10;
        int end = json.IndexOf("\"", start);
        return json.Substring(start, end - start).Replace("\\n", "\n");
    }

    #endregion

    #region VOZ A TEXTO + GPT

    public void StartRecording()
    {
        recordedClip = Microphone.Start(microphoneDevice, false, 10, 44100);
    }

    public void StopRecordingAndSend()
    {
        Microphone.End(microphoneDevice);
        string path = Path.Combine(Application.persistentDataPath, "grabacion.wav");
        SaveWav(path, recordedClip);
        _ = SendAudioToWhisper(path);
    }

    private async Task SendAudioToWhisper(string filePath)
    {
        byte[] audioBytes = File.ReadAllBytes(filePath);
        WWWForm form = new();
        form.AddBinaryData("file", audioBytes, "grabacion.wav", "audio/wav");
        form.AddField("model", "whisper-1");

        UnityWebRequest request = UnityWebRequest.Post(whisperEndpoint, form);
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            string transcription = ExtractContentFromJson(result);
            AppendToChat("Tú [voz]", transcription);
            await SendTextToGPT(transcription);
        }
        else
        {
            AppendToChat("Error", request.error);
        }
    }

    #endregion

    #region REALTIME AUDIO + GPT-4o

    public async void StartRealtimeSession()
    {
        realtimeCts = new CancellationTokenSource();
        realtimeSocket = new ClientWebSocket();
        realtimeSocket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        await realtimeSocket.ConnectAsync(new Uri(gptRealtimeWsUrl), realtimeCts.Token);

        var startPayload = new
        {
            type = "start",
            model = "gpt-4o"
        };
        string json = JsonUtility.ToJson(startPayload);
        ArraySegment<byte> startBytes = new(Encoding.UTF8.GetBytes(json));
        await realtimeSocket.SendAsync(startBytes, WebSocketMessageType.Text, true, realtimeCts.Token);

        AppendToChat("Sistema", "[Conectado a GPT-4o Realtime]");
        _ = ReceiveRealtimeAudio(realtimeCts.Token);
    }

    private async Task ReceiveRealtimeAudio(CancellationToken token)
    {
        var buffer = new byte[8192];
        var textBuilder = new StringBuilder();

        while (!token.IsCancellationRequested && realtimeSocket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await realtimeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            byte[] data = ms.ToArray();

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string msg = Encoding.UTF8.GetString(data);
                textBuilder.Append(msg);
                if (msg.Contains("final") || msg.Contains("stop"))
                {
                    AppendToChat("Bot [texto]", textBuilder.ToString());
                    textBuilder.Clear();
                }
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                var clip = WavUtility.ChunkToClip(data);
                if (clip != null)
                {
                    playbackSource.clip = clip;
                    playbackSource.Play();
                }
            }
        }
    }

    public void StopRealtimeSession()
    {
        if (realtimeSocket != null && realtimeSocket.State == WebSocketState.Open)
        {
            realtimeCts.Cancel();
            _ = realtimeSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            AppendToChat("Sistema", "[Sesión realtime finalizada]");
        }
    }

    #endregion

    #region UTILS

    private void AppendToChat(string sender, string text)
    {
        chatDisplay.text += $"\n<b>{sender}:</b> {text}";
    }

    private void SaveWav(string path, AudioClip clip)
    {
        var bytes = WavUtility.FromAudioClip(clip);
        File.WriteAllBytes(path, bytes);
    }

    #endregion
}
