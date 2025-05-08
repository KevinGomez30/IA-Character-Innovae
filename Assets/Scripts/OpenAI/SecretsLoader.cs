using System.IO;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SecretData
{
    public string OPENAI_API_KEY;
}

public static class SecretsLoader
{
    private static SecretData _secrets;

    public static SecretData Secrets
    {
        get
        {
            if (_secrets == null)
                LoadSecrets();

            return _secrets;
        }
    }

    private static void LoadSecrets()
    {
#if UNITY_EDITOR
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "secrets.json");
#else
        string path = Path.Combine(Application.streamingAssetsPath, "secrets.json");
#endif

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            _secrets = JsonUtility.FromJson<SecretData>(json);
        }
        else
        {
            Debug.LogError("Secrets.json file not found at: " + path);
        }
    }
}
