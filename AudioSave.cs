// #if UNITY_WEBGL && !UNITY_EDITOR
#define CAN_EXPORT
// #endif

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
        private const int TimeoutMs = 15000;

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

        public class Config
        {
            public string Title { get; set; }
            public string GameId { get; set; }
            public int Bpm { get; set; }
            public string[] Tags { get; set; }
            public string Description { get; set; }

            public Config(string title, string gameId, int bpm, string[] tags = default, string description = default)
            {
                Title = title;
                GameId = gameId;
                Bpm = bpm;
                Tags = tags == default ? new string[] { } : tags;
                Description = description ?? "An original student composition.";
            }
        }

        /// <summary>
        /// Fired during upload with progress (0-1) and stage description.
        /// Only fires when using Overture Bridge protocol.
        /// </summary>
        public static event Action<float, string> OnProgress;

        public static bool IsInitialized { get; private set; }
        public static bool? BridgeAvailable { get; private set; }

        private static AudioSaveListener _listener;
        private static int _requestCounter;

        // Bridge JS functions
        [DllImport("__Internal")]
        private static extern void OvertureBridge_Init(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void OvertureBridge_Handshake(string requestId);

        [DllImport("__Internal")]
        private static extern void OvertureBridge_SaveSong(string requestId, string songDataJson);

        // Legacy JS function
        [DllImport("__Internal")]
        private static extern void SaveSong(string songDataJson, string gameObjectName);

        private static void Initialize()
        {
            if (IsInitialized) return;

            _listener = new GameObject("Audio Save Listener").AddComponent<AudioSaveListener>();
            _listener.OnProgressReceived += (percent, stage) => OnProgress?.Invoke(percent, stage);

#if CAN_EXPORT
            OvertureBridge_Init(_listener.name);
#endif

            IsInitialized = true;
            Debug.Log("[AudioSave] Initialized");
        }

        private static string GenerateRequestId()
        {
            return $"req_{++_requestCounter}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        public static async Awaitable<PlatformUploadResult> HandleFileAsync(string path, Config config, Action<PlatformUploadResult> callback, string overrideFileName = null)
        {
            var result = await HandleFileAsync(path, config, overrideFileName);
            callback?.Invoke(result);
            return result;
        }

        public static async Awaitable<PlatformUploadResult> HandleFileAsync(string path, Config config, string overrideFileName = null)
        {
            if (!IsInitialized)
                Initialize();

#if CAN_EXPORT
            Debug.Log($"[AudioSave] Uploading: {path}");

            if (!File.Exists(path))
            {
                Debug.LogError($"[AudioSave] File not found: {path}");
                return new PlatformUploadResult { Success = false, Message = "File not found" };
            }

            byte[] fileData = File.ReadAllBytes(path);
            string base64Audio = Convert.ToBase64String(fileData);
            Debug.Log($"[AudioSave] File size: {fileData.Length} bytes, Base64 length: {base64Audio.Length}");

            var songData = new Req_SaveData()
            {
                title = overrideFileName ?? GenerateFileName(config.Title),
                gameId = config.GameId,
                tags = config.Tags.Concat(new[] { config.GameId }).ToArray(),
                bpm = config.Bpm,
                description = config.Description,
                audioData = base64Audio,
                format = "wav",
                duration = GetWavDuration(fileData),
                fileSize = fileData.Length,
                sampleRate = 44100,
                channels = 2,
                isPublic = false,
            };

            // Try Bridge first (with handshake if needed)
            if (BridgeAvailable == null)
            {
                Debug.Log("[AudioSave] Bridge status unknown, attempting handshake...");
                BridgeAvailable = await TryHandshakeAsync();
            }

            PlatformUploadResult result = null;

            if (BridgeAvailable == true)
            {
                result = await TrySaveViaBridgeAsync(songData);

                if (result == null)
                {
                    Debug.LogWarning("[AudioSave] Bridge save failed, falling back to legacy");
                    BridgeAvailable = false;
                }
            }

            // Fallback to legacy if Bridge unavailable or failed
            if (result == null)
            {
                result = await SaveViaLegacyAsync(songData);
            }

            // Clean up local file
            try
            {
                File.Delete(path);
                Debug.Log($"[AudioSave] Cleaned up local file: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioSave] Could not delete local file: {e.Message}");
            }

            OnPlatformUploadResult(result);
            return result;
#else
            Debug.Log($"[AudioSave] EDITOR MODE: File saved at: {path}");
            EditorUtility.RevealInFinder(path);

            var result = new PlatformUploadResult
            {
                Success = true,
                Message = $"File saved locally to {path}",
                SongId = "local-save-editor-id"
            };
            OnPlatformUploadResult(result);

            await Awaitable.EndOfFrameAsync();
            return result;
#endif
        }

        private static async Awaitable<bool> TryHandshakeAsync()
        {
#if CAN_EXPORT
            _listener.ResetHandshakeState();

            var requestId = GenerateRequestId();
            Debug.Log($"[AudioSave] Starting Bridge handshake: {requestId}");

            OvertureBridge_Handshake(requestId);

            var startTime = Time.realtimeSinceStartup;
            while (!_listener.HandshakeReceived)
            {
                if ((Time.realtimeSinceStartup - startTime) * 1000 > TimeoutMs)
                {
                    Debug.LogWarning("[AudioSave] Bridge handshake timed out");
                    return false;
                }
                await Awaitable.NextFrameAsync();
            }

            if (_listener.HandshakeSupported)
            {
                Debug.Log("[AudioSave] Bridge handshake successful - Bridge is available");
                return true;
            }
            else
            {
                Debug.Log("[AudioSave] Bridge handshake responded but not supported");
                return false;
            }
#else
            await Awaitable.EndOfFrameAsync();
            return false;
#endif
        }

        private static async Awaitable<PlatformUploadResult> TrySaveViaBridgeAsync(Req_SaveData songData)
        {
#if CAN_EXPORT
            _listener.ResetSaveState();

            var requestId = GenerateRequestId();
            var songDataJson = JsonConvert.SerializeObject(songData);

            Debug.Log($"[AudioSave] Saving via Bridge: {requestId}");

            OvertureBridge_SaveSong(requestId, songDataJson);

            var startTime = Time.realtimeSinceStartup;

            // Wait for result (with timeout)
            while (!_listener.SaveResultReceived)
            {
                if ((Time.realtimeSinceStartup - startTime) * 1000 > TimeoutMs)
                {
                    Debug.LogWarning("[AudioSave] Bridge save timed out");
                    return null; // Return null to trigger fallback
                }
                await Awaitable.NextFrameAsync();
            }

            if (_listener.SaveResultSuccess)
            {
                return new PlatformUploadResult
                {
                    Success = true,
                    Message = "Song saved via Bridge",
                    SongId = _listener.SaveResultSongId
                };
            }
            else
            {
                Debug.LogError($"[AudioSave] Bridge save failed: {_listener.SaveResultError}");
                return new PlatformUploadResult
                {
                    Success = false,
                    Message = _listener.SaveResultError ?? "Bridge save failed"
                };
            }
#else
            await Awaitable.EndOfFrameAsync();
            return null;
#endif
        }

        private static async Awaitable<PlatformUploadResult> SaveViaLegacyAsync(Req_SaveData songData)
        {
#if CAN_EXPORT
            Debug.Log("[AudioSave] Saving via Legacy API");

            var songDataJson = JsonConvert.SerializeObject(songData);

            await Awaitable.WaitForSecondsAsync(1);

            _listener.IsAwaiting = true;
            SaveSong(songDataJson, _listener.name);

            var startTime = Time.realtimeSinceStartup;
            while (_listener.IsAwaiting)
            {
                if ((Time.realtimeSinceStartup - startTime) * 1000 > TimeoutMs)
                {
                    Debug.LogWarning("[AudioSave] Legacy save timed out");
                    return new PlatformUploadResult
                    {
                        Success = false,
                        Message = "Legacy save timed out"
                    };
                }
                await Awaitable.NextFrameAsync();
            }

            try
            {
                return JsonConvert.DeserializeObject<PlatformUploadResult>(_listener.UploadResultJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioSave] Error deserializing legacy result: {e.Message}");
                return new PlatformUploadResult
                {
                    Success = false,
                    Message = "Error deserializing upload result"
                };
            }
#else
            await Awaitable.EndOfFrameAsync();
            return new PlatformUploadResult
            {
                Success = false,
                Message = "Legacy save not available in editor"
            };
#endif
        }

        public static void OnPlatformUploadResult(PlatformUploadResult result)
        {
            if (result.Success)
                Debug.Log($"[AudioSave] SUCCESS: {result.Message} | Song ID: {result.SongId}");
            else
                Debug.LogError($"[AudioSave] FAILED: {result.Message}");
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

        /// <summary>
        /// Resets Bridge availability state, forcing a new handshake on next save.
        /// Useful for testing or recovery scenarios.
        /// </summary>
        public static void ResetBridgeState()
        {
            BridgeAvailable = null;
            Debug.Log("[AudioSave] Bridge state reset");
        }
    }
}
