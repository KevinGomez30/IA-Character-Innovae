using Newtonsoft.Json;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class GPT4oVoiceAndText : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public TMP_Text chatDisplay;
    public Button botonTexto;
    public Button botonAudio;

    [Header("Audio")]
    public AudioSource audioSource;
    private AudioClip recordedClip;
    private bool isRecording = false;

    private string apiKey;
    private const string whisperEndpoint = "https://api.openai.com/v1/audio/transcriptions";
    private const string gptEndpoint = "https://api.openai.com/v1/chat/completions";
    private const string ttsEndpoint = "https://api.openai.com/v1/audio/speech";

    private void Start()
    {
        apiKey = SecretsLoader.GetSecrets().OPENAI_API_KEY;

        botonTexto.onClick.RemoveAllListeners(); // Limpia por si acaso
        botonTexto.onClick.AddListener(() =>
        {
            string text = inputField.text;
            if (!string.IsNullOrEmpty(text))
                StartCoroutine(SendTextToGPT(text, false));
        });

        botonAudio.onClick.RemoveAllListeners(); // Limpia por si acaso
        botonAudio.onClick.AddListener(() =>
        {
            if (!isRecording)
            {
                StartCoroutine(StartVoiceRecording());
            }
        });
    }


    // 1. Inicia la grabación de voz desde el micrófono
    private IEnumerator StartVoiceRecording()
    {
        isRecording = true;
        recordedClip = Microphone.Start(null, false, 10, 44100);
        chatDisplay.text += "\n Grabando...";

        yield return new WaitForSeconds(3); // Duración de la grabación

        Microphone.End(null);
        isRecording = false;
        //chatDisplay.text += "\n Enviando audio...";

        yield return StartCoroutine(ProcessVoiceInput(recordedClip));
    }

    // 2. Convierte el clip de audio a WAV y lo envía a Whisper
    private IEnumerator ProcessVoiceInput(AudioClip clip)
    {
        byte[] wavData = SaveWavToMemory(clip);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-1");

        UnityWebRequest request = UnityWebRequest.Post(whisperEndpoint, form);
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Whisper Error: " + request.error);
            yield break;
        }

        string transcription = ExtractWhisperTranscription(request.downloadHandler.text);
        if (!string.IsNullOrEmpty(transcription) && transcription != "No se pudo extraer la transcripción.")
        {
            chatDisplay.text += $"\n Tú: {transcription}";
            StartCoroutine(SendTextToGPT(transcription, true));
        }
        else
        {
            chatDisplay.text += "\n Transcripción vacía o inválida. No se envía a GPT.";
        }

        // Enviar texto transcrito a GPT
        //StartCoroutine(SendTextToGPT(transcription, true));
    }

    // 3. Envia texto al modelo GPT-4o
    private IEnumerator SendTextToGPT(string userInput, bool fromVoice = false)
    {
        string apiUrl = gptEndpoint;

        var requestBody = new
        {
            model = "gpt-4o",
            messages = new[]
            {
            new { role = "user", content = userInput }
        }
        };

        string jsonData = JsonConvert.SerializeObject(requestBody);
        Debug.Log("JSON Enviado: " + jsonData);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("GPT Error: " + request.responseCode + " - " + request.error);
            yield break;
        }

        string responseJson = request.downloadHandler.text;
        string reply = ExtractTextFromJson(responseJson);

        chatDisplay.text += $"\n Aloe: {reply}";

        // Si la entrada fue por voz, además reproducimos la respuesta con TTS
        if (fromVoice)
        {
            StartCoroutine(RequestTTS(reply));
        }
    }




    // 4. Solicita el audio de respuesta con TTS
    private IEnumerator RequestTTS(string text)
    {
        SecretData secrets = SecretsLoader.GetSecrets();
        string apiKey = secrets.OPENAI_API_KEY;
        string url = "https://api.openai.com/v1/audio/speech";

        // Crear JSON body
        var bodyJson = new
        {
            model = "tts-1",
            input = text,
            voice = "nova", // Puedes cambiar a shimmer, echo, nova etc.
            response_format = "mp3"
        };


        string body = JsonConvert.SerializeObject(bodyJson); // <--- SERIALIZACIÓN CORRECTA

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS Error: " + request.responseCode + " - " + request.downloadHandler.text);
            }
            else
            {
                byte[] audioData = request.downloadHandler.data;
                PlayResponseAudio(audioData);
            }
        }
    }

    private void PlayResponseAudio(byte[] audioData)
    {
        // Ruta absoluta personalizada (¡asegúrate de que exista!)
        string customPath = Path.Combine(Application.streamingAssetsPath, "Audio");


        // Asegurarse de que la carpeta exista
        if (!Directory.Exists(customPath))
        {
            Directory.CreateDirectory(customPath);
        }

        string filePath = Path.Combine(customPath, "response.mp3");

        File.WriteAllBytes(filePath, audioData);
        Debug.Log("Audio guardado en: " + filePath + " - Tamaño: " + audioData.Length + " bytes");

        StartCoroutine(PlayAudioFromFile(filePath));
    }

    private IEnumerator PlayAudioFromFile(string filePath)
    {
        Debug.Log("Intentando cargar audio desde: " + filePath);
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Audio Load Error: " + www.error);
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip == null)
                {
                    Debug.LogError("No se pudo cargar el clip de audio.");
                }
                else
                {
                    Debug.Log("Audio cargado correctamente. Duración: " + clip.length + " segundos.");
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log("Reproduciendo audio... isPlaying: " + audioSource.isPlaying);
                }
            }
        }
    }


    // 6. Extrae texto desde respuesta JSON simple
    private string ExtractTextFromJson(string json)
    {
        try
        {
            var parsed = JsonConvert.DeserializeObject<GPTResponse>(json);
            return parsed?.choices?[0]?.message?.content ?? "No se pudo extraer la respuesta del modelo.";
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al parsear JSON: " + e.Message);
            return "Error al interpretar la respuesta.";
        }
    }

    // Extrae la transcripción desde la respuesta JSON de Whisper
    private string ExtractWhisperTranscription(string json)
    {
        try
        {
            var parsed = JsonConvert.DeserializeObject<WhisperResponse>(json);
            return parsed?.text ?? "No se pudo extraer la transcripción.";
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al parsear la transcripción de Whisper: " + e.Message);
            return "Error al interpretar la transcripción.";
        }
    }

    // Clase para respuesta de Whisper
    [System.Serializable]
    public class WhisperResponse
    {
        public string text;
    }

    // Clases de ayuda para parsear la respuesta
    [System.Serializable]
    public class OpenAIChatResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    public class Choice
    {
        public Message message;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    public class GPTResponse
    {
        public Choice[] choices;
    }


    // 7. Guarda el clip de audio como WAV en memoria
    private byte[] SaveWavToMemory(AudioClip clip)
    {
        using MemoryStream stream = new MemoryStream();
        SavWav.Save("audio", clip, stream);
        return stream.ToArray();
    }
}