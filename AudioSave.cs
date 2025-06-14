#if UNITY_WEBGL && !UNITY_EDITOR
#define CAN_EXPORT
#endif

using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.IO;

#if CAN_EXPORT
using System.Linq;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Overture.Export
{
    public static class AudioSave
    {
        [Serializable]
        public class Req_SaveData
        {
            public string audioData;
            public int bpm;
            public int channels;
            public string description;
            public float duration;
            public int fileSize;
            public string format;
            public string gameId;
            public bool isPublic;
            public int sampleRate;
            public string[] tags;
            public string title;
        }

        [Serializable]
        public class PlatformUploadResult
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("message")] public string Message { get; set; }
            [JsonProperty("songId")] public string SongId { get; set; }
        }

        public struct Options
        {
            public string Title { get; set; }
            public string GameId { get; set; }
            public int Bpm { get; set; }
            public string[] Tags { get; set; }
            public string Description { get; set; }

            public Options(string title, string gameId, int bpm, string[] tags = default, string description = default)
            {
                Title = title;
                GameId = gameId;
                Bpm = bpm;
                Tags = tags == default ? new string[] { } : tags;
                Description = description ?? "An original student composition.";
            }
        }

        public static bool IsInitialized { get; private set; }
        private static AudioSaveListener _listener;

        [DllImport("__Internal")]
        private static extern void SaveSong(string songDataJson, string gameObjectName);

        private static void Initialize()
        {
            _listener = new GameObject("Audio Save Listener").AddComponent<AudioSaveListener>();
        }

        public static async Awaitable HandleFileAsync(string path, Options options)
        {
            if (!IsInitialized)
                Initialize();

#if CAN_EXPORT
            Debug.Log($"Uploading DAW export to platform: {path}");

            if (!File.Exists(path))
            {
                Debug.LogError($"File not found for platform upload: {path}");
                return;
            }

            byte[] fileData = File.ReadAllBytes(path);
            string base64Audio = Convert.ToBase64String(fileData);
            Debug.Log($"File size: {fileData.Length} bytes, Base64 length: {base64Audio.Length}");
            Debug.Log($"Raw data: {base64Audio}");

            var songData = new Req_SaveData()
            {
                title = GenerateFileName(options.Title),
                gameId = options.GameId,
                tags = options.Tags.Concat(new[] { options.GameId }).ToArray(),
                bpm = options.Bpm,
                description = options.Description,

                audioData = base64Audio,
                format = "wav",
                duration = GetWavDuration(fileData),
                fileSize = fileData.Length,
                sampleRate = 44100,
                channels = 2,
                isPublic = false,
            };

            var songDataJson = JsonConvert.SerializeObject(songData);

            await Awaitable.WaitForSecondsAsync(1);
            
            _listener.IsAwaiting = true;
            SaveSong(songDataJson, _listener.name);

            try
            {
                File.Delete(path);
                Debug.Log($"Cleaned up local file: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not delete local file: {e.Message}");
            }

#else
            Debug.Log($"DAW EXPORT SAVED (Editor/Standalone): File is at: {path}");
            EditorUtility.RevealInFinder(path);

            byte[] fileData = File.ReadAllBytes(path);
            string base64Audio = Convert.ToBase64String(fileData);
            Debug.Log($"File size: {fileData.Length} bytes, Base64 length: {base64Audio.Length}");
            Debug.Log($"Raw data: {base64Audio}");

            OnPlatformUploadResult(JsonConvert.SerializeObject(new PlatformUploadResult
            {
                Success = true,
                Message = $"File saved locally to {path}",
                SongId = "local-save-editor-id"
            }));

            await Awaitable.EndOfFrameAsync();
#endif
        }

        public static void OnPlatformUploadResult(string resultJson)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<PlatformUploadResult>(resultJson);
                if (result.Success)
                    Debug.Log($"PLATFORM RESULT: {result.Message} | Song ID: {result.SongId}");
                else
                    Debug.LogError($"PLATFORM RESULT: {result.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing platform upload result: {e.Message}");
            }
        }

        public static string GenerateFileName(string prefix)
        {
            var timestamp = DateTime.Now.ToString("MMdd_HHmm");
            return $"{prefix} - {timestamp}";
        }

        private static float GetWavDuration(byte[] wavData)
        {
            if (wavData.Length < 44) return 0f;
            try
            {
                int byteRate = BitConverter.ToInt32(wavData, 28);
                if (byteRate == 0) return 0f;
                int dataSize = wavData.Length - 44;
                return (float)dataSize / byteRate;
            }
            catch { return 0f; }
        }
    }
}
