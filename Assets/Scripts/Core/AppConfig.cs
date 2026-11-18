using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace PostureCheck
{
    /// <summary>
    /// Everything the design team can change without touching code:
    /// StreamingAssets/config.json (names, colours, text lines, thresholds, camera field of view,
    /// image file names) and the images in StreamingAssets/Content.
    /// Missing keys or files fall back to the built-in defaults below.
    /// </summary>
    [Serializable]
    public class AppConfig
    {
        [Serializable]
        public class ColorSettings
        {
            public string ui = "#9E9E9E";
            public string postureLine = "#FFFFFF";
            public string postureLineOutOfRange = "#FF8C00";
        }

        [Serializable]
        public class AssetSettings
        {
            public string mascotIdle = "mascot_idle.png";
            public string floorMarker = "floor_marker.png";
        }

        [Serializable]
        public class ThresholdSettings
        {
            public float shoulderTiltDeg = 3f;
            public float shoulderTiltMarginDeg = 0.5f;
            public float hipTiltDeg = 3f;
            public float hipTiltMarginDeg = 0.5f;
            /// <summary>Knee bend range in the front view (positive = knock-knee, negative = bow-leg); outside it is flagged.</summary>
            public float kneeMinDeg = -8f;
            public float kneeMaxDeg = 8f;
            public float smoothingSeconds = 0.5f;
            public float minVisibility = 0.6f;
        }

        [Serializable]
        public class MeasurementSettings
        {
            /// <summary>A value is the median over this many seconds of standing still.</summary>
            public float stillWindowSeconds = 2f;
            /// <summary>Still = no watched point moves more than this fraction of the torso length.</summary>
            public float stillTolerance = 0.06f;
            /// <summary>Active seconds one try may take before it counts as failed.</summary>
            public float tryTimeoutSeconds = 10f;
            /// <summary>After this many failed tries the missing values become "could not measure".</summary>
            public int maxTries = 2;
            /// <summary>Share of frames in the window where a value must be measurable.</summary>
            public float minValidFraction = 0.6f;
        }

        [Serializable]
        public class CameraSettings
        {
            /// <summary>Front camera vertical field of view (degrees) of the upright portrait image; tune per phone.</summary>
            public float frontVerticalFovDeg = 60f;
        }

        public string appName = "App";
        public string mascotName = "Mascot";
        public ColorSettings colors = new();
        public AssetSettings assets = new();
        public ThresholdSettings thresholds = new();
        public MeasurementSettings measurement = new();
        public CameraSettings camera = new();

        // JsonUtility cannot read a dictionary, so "lines" is parsed separately.
        [NonSerialized] Dictionary<string, string> m_Lines = new();

        static readonly Dictionary<string, string> DefaultLines = new()
        {
            ["start.greeting"] = "Hi! I'm {mascot}. Let's check how you stand!",
            ["start.button"] = "Start check",
            ["scan.pointAtFloor"] = "Point the camera at the floor where your child will stand.",
            ["scan.noFloor"] = "Point at the floor in a lit area.",
            ["scan.found"] = "Floor found!",
            ["scan.useSpot"] = "Use this spot",
            ["scan.skip"] = "Skip",
            ["scan.unavailable"] = "Floor scan is not available on this phone. Skipping it.",
            ["turn.instruction"] = "Turn the phone around and put it here, screen facing the room.",
            ["turn.continue"] = "Continue",
            ["setup.stepIn"] = "Step in front of the camera.",
            ["setup.stepBack"] = "Step back",
            ["setup.moveLeft"] = "Move left",
            ["setup.moveRight"] = "Move right",
            ["setup.standOnStar"] = "Stand on the star!",
            ["front.holdStill"] = "Stand still like a statue!",
            ["front.tryAgain"] = "Oops, you moved! Let's try once more.",
            ["front.done"] = "Great job!",
        };

        static AppConfig s_Current;

        /// <summary>The loaded config; built-in defaults if the file is missing or broken.</summary>
        public static AppConfig Current => s_Current ??= Load();

        /// <summary>A text line by key, with {app} and {mascot} filled in.</summary>
        public string Line(string key)
        {
            if (!m_Lines.TryGetValue(key, out var text) && !DefaultLines.TryGetValue(key, out text)) text = key;
            return text.Replace("{app}", appName).Replace("{mascot}", mascotName);
        }

        public static Color ParseColor(string hex, Color fallback) =>
            ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        public Color UiColor => ParseColor(colors.ui, new Color(0.62f, 0.62f, 0.62f));
        public Color PostureLineColor => ParseColor(colors.postureLine, Color.white);
        public Color PostureLineOutOfRangeColor => ParseColor(colors.postureLineOutOfRange, new Color(1f, 0.55f, 0f));

        /// <summary>Loads an image from StreamingAssets/Content; null if missing.</summary>
        public static Texture2D LoadContentImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string path = Path.Combine(Application.streamingAssetsPath, "Content", fileName);
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = fileName };
                if (tex.LoadImage(ReadBytes(path))) return tex;
                UnityEngine.Object.Destroy(tex);
                Debug.LogWarning($"Not an image: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not load {path}: {e.Message}");
            }
            return null;
        }

        static AppConfig Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "config.json");
            var config = new AppConfig();
            try
            {
                string json = Encoding.UTF8.GetString(ReadBytes(path));
                JsonUtility.FromJsonOverwrite(json, config); // keys missing from the file keep their defaults
                config.m_Lines = ParseLines(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not read {path}, using defaults: {e.Message}");
            }
            return config;
        }

        /// <summary>Reads the flat "lines": { "key": "text", ... } object.</summary>
        static Dictionary<string, string> ParseLines(string json)
        {
            var lines = new Dictionary<string, string>();
            var block = Regex.Match(json, "\"lines\"\\s*:\\s*\\{(?<body>[^{}]*)\\}");
            if (!block.Success) return lines;
            foreach (Match m in Regex.Matches(block.Groups["body"].Value,
                         "\"(?<key>(?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\""))
                lines[Unescape(m.Groups["key"].Value)] = Unescape(m.Groups["value"].Value);
            return lines;
        }

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u' when i + 4 < s.Length:
                        sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(n); break; // \" \\ \/
                }
            }
            return sb.ToString();
        }

        static byte[] ReadBytes(string path)
        {
            // On Android StreamingAssets sits inside the APK, so it has to be read as a web request.
            if (!path.Contains("://")) return File.ReadAllBytes(path);
            using var request = UnityWebRequest.Get(path);
            var op = request.SendWebRequest();
            while (!op.isDone) { } // small local file, done within milliseconds
            if (request.result != UnityWebRequest.Result.Success) throw new Exception(request.error);
            return request.downloadHandler.data;
        }
    }
}
