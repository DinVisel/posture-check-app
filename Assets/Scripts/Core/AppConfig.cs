using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PostureCheck
{
    /// <summary>
    /// Settings from StreamingAssets/config.json, so they can change without touching code.
    /// Step 1.4 reads only the thresholds; step 1.8 adds the names, colours, assets and lines.
    /// </summary>
    [Serializable]
    public class AppConfig
    {
        [Serializable]
        public class ThresholdSettings
        {
            public float shoulderTiltDeg = 3f;
            public float shoulderTiltMarginDeg = 0.5f;
            public float smoothingSeconds = 0.5f;
            public float minVisibility = 0.6f;
        }

        public ThresholdSettings thresholds = new();

        static AppConfig s_Current;

        /// <summary>The loaded config; built-in defaults if the file is missing or broken.</summary>
        public static AppConfig Current => s_Current ??= Load();

        static AppConfig Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "config.json");
            try
            {
                string json = ReadText(path);
                var config = new AppConfig();
                JsonUtility.FromJsonOverwrite(json, config); // keys missing from the file keep their defaults
                return config;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not read {path}, using defaults: {e.Message}");
                return new AppConfig();
            }
        }

        static string ReadText(string path)
        {
            // On Android StreamingAssets sits inside the APK, so it has to be read as a web request.
            if (!path.Contains("://")) return File.ReadAllText(path);
            using var request = UnityWebRequest.Get(path);
            var op = request.SendWebRequest();
            while (!op.isDone) { } // small local file, done within milliseconds
            if (request.result != UnityWebRequest.Result.Success) throw new Exception(request.error);
            return request.downloadHandler.text;
        }
    }
}
