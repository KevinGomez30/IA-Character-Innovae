#if UNITY_ANDROID || UNITY_IOS
using UnityEngine;

public class MicrophonePermissions : MonoBehaviour
{
    void Start()
    {
        // Verificar si el dispositivo tiene permisos para usar el micr�fono
        if (!Microphone.IsRecording(null))
        {
            // El permiso no ha sido concedido a�n, pedirlo
            StartCoroutine(RequestMicrophonePermission());
        }
    }

    // M�todo para solicitar permiso en Android/iOS
    private IEnumerator RequestMicrophonePermission()
    {
        // Solicitar permiso al usuario en plataformas m�viles
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.Log("Permiso concedido para usar el micr�fono");
        }
        else
        {
            Debug.LogError("No se concedi� permiso para usar el micr�fono");
        }
    }
}
#endif
