using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;

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

public class OpenAIChat : MonoBehaviour
{
    [Header("Configuración de OpenAI")]
    private string apiKey = SecretsLoader.Secrets.OPENAI_API_KEY; // Reemplaza con tu clave de API
    [SerializeField] private string apiUrl = "https://api.openai.com/v1/chat/completions";
    [SerializeField] private string model = "gpt-4o"; // O "4o-realtime"
    [SerializeField] private string projectID = "TU_PROJECT_ID"; // Nuevo campo para el ID de proyecto

    [Header("Referencias de UI")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text chatText;

    private bool canSendMessage = true;
    private float requestCooldown = 5f;
    private int requestLimit = 10;
    private int requestCount = 0;
    private int totalTokensConsumed = 0;

    private List<ChatMessage> chatHistory = new List<ChatMessage>(); // Historial de mensajes

    /// <summary>
    /// Método para enviar un mensaje a OpenAI.
    /// </summary>
    public void SendMessageToAI()
    {
        if (!canSendMessage)
        {
            Debug.LogWarning("Espera antes de enviar otro mensaje.");
            return;
        }

        if (requestCount >= requestLimit)
        {
            Debug.LogError("Límite de solicitudes alcanzado. Espera o revisa tu cuota en OpenAI.");
            chatText.text += "\nHas alcanzado el límite de solicitudes.";
            return;
        }

        string userMessage = inputField.text;
        if (string.IsNullOrEmpty(userMessage)) return;

        inputField.text = "";
        requestCount++;
        StartCoroutine(RequestCooldown());
        StartCoroutine(SendRequest(userMessage));
    }

    /// <summary>
    /// Enviar solicitud a OpenAI.
    /// </summary>
    private IEnumerator SendRequest(string userMessage)
    {
        // Agregar mensaje del usuario al historial completo
        chatHistory.Add(new ChatMessage("user", userMessage));

        // Determinar cuántos mensajes recientes queremos enviar (por ejemplo, los últimos 6)
        int maxMessagesToSend = 2;
        List<ChatMessage> recentMessages = new List<ChatMessage>();

        if (chatHistory.Count > maxMessagesToSend)
        {
            recentMessages = chatHistory.GetRange(chatHistory.Count - maxMessagesToSend, maxMessagesToSend);
        }
        else
        {
            recentMessages = new List<ChatMessage>(chatHistory);
        }

        ChatRequest requestBody = new ChatRequest(model, recentMessages);

        string jsonRequestBody = JsonUtility.ToJson(requestBody);
        Debug.Log("JSON enviado: " + jsonRequestBody);

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            if (!string.IsNullOrEmpty(projectID))
            {
                request.SetRequestHeader("OpenAI-Project", projectID);
            }

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {request.error}\nRespuesta: {request.downloadHandler.text}");

                if (request.downloadHandler.text.Contains("insufficient_quota"))
                {
                    Debug.LogError("Se ha agotado la cuota de OpenAI.");
                    chatText.text += "\nSe ha agotado la cuota de OpenAI.";
                }
            }
            else
            {
                string responseJson = request.downloadHandler.text;
                Debug.Log("Respuesta recibida: " + responseJson);
                OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseJson);

                if (response.choices != null && response.choices.Length > 0)
                {
                    string aiMessage = response.choices[0].message.content;

                    // Agregar respuesta de IA al historial
                    chatHistory.Add(new ChatMessage("assistant", aiMessage));

                    // Estimar tokens consumidos
                    int userTokens = EstimateTokenCount(userMessage);
                    int aiTokens = EstimateTokenCount(aiMessage);
                    int tokensThisRequest = userTokens + aiTokens;
                    totalTokensConsumed += tokensThisRequest;

                    // Mostrar mensajes en UI
                    chatText.text = $"\n{aiMessage}";

                    // Mostrar tokens en la consola
                    Debug.Log($"Tokens usados en esta solicitud: Usuario {userTokens} + AI {aiTokens} = {tokensThisRequest}");
                    Debug.Log($"Total de tokens consumidos: {totalTokensConsumed}");
                }
                else
                {
                    Debug.LogError("No se recibieron respuestas válidas de OpenAI.");
                }
            }
        }
    }


    /// <summary>
    /// Control de cooldown para evitar spam.
    /// </summary>
    private IEnumerator RequestCooldown()
    {
        canSendMessage = false;
        yield return new WaitForSeconds(requestCooldown);
        canSendMessage = true;
    }

    /// <summary>
    /// Estimación de tokens.
    /// </summary>
    private int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return Mathf.CeilToInt(text.Length / 4f);
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