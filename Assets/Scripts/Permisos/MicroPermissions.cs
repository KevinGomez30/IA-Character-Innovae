#if UNITY_ANDROID || UNITY_IOS
using UnityEngine;

public class MicrophonePermissions : MonoBehaviour
{
    void Start()
    {
        // Verificar si el dispositivo tiene permisos para usar el micrófono
        if (!Microphone.IsRecording(null))
        {
            // El permiso no ha sido concedido aún, pedirlo
            StartCoroutine(RequestMicrophonePermission());
        }
    }

    // Método para solicitar permiso en Android/iOS
    private IEnumerator RequestMicrophonePermission()
    {
        // Solicitar permiso al usuario en plataformas móviles
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.Log("Permiso concedido para usar el micrófono");
        }
        else
        {
            Debug.LogError("No se concedió permiso para usar el micrófono");
        }
    }
}
#endif
