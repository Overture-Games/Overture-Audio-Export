using UnityEngine;
using System;
using System.IO;

namespace Overture.Export
{
    /// <summary>
    /// Drop this on a GameObject to test the Overture Bridge from Unity.
    /// Press keys 1-4 to run different tests. Check console for results.
    /// </summary>
    public class BridgeTest : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private string testGameId = "bridge-test";
        [SerializeField] private bool createDummyWavFile = true;

        private void Start()
        {
            Debug.Log("=== OVERTURE BRIDGE TEST ===");
            Debug.Log("Press 1: Test Handshake");
            Debug.Log("Press 2: Test Save (creates dummy WAV)");
            Debug.Log("Press 3: Test Progress Events");
            Debug.Log("Press 4: Reset Bridge State");
            Debug.Log("Press 5: Check Bridge Status");
            Debug.Log("============================");

            // Subscribe to progress events
            AudioSave.OnProgress += OnProgress;
        }

        private void OnDestroy()
        {
            AudioSave.OnProgress -= OnProgress;
        }

        private void OnProgress(float percent, string stage)
        {
            Debug.Log($"[BridgeTest] PROGRESS: {percent:P0} - {stage}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) TestHandshake();
            if (Input.GetKeyDown(KeyCode.Alpha2)) TestSave();
            if (Input.GetKeyDown(KeyCode.Alpha3)) TestProgressEvents();
            if (Input.GetKeyDown(KeyCode.Alpha4)) ResetState();
            if (Input.GetKeyDown(KeyCode.Alpha5)) CheckStatus();
        }

        private async void TestHandshake()
        {
            Debug.Log("\n[BridgeTest] === TESTING HANDSHAKE ===");
            Debug.Log($"[BridgeTest] Current Bridge status: {(AudioSave.BridgeAvailable?.ToString() ?? "null")}");

            // Reset to force new handshake
            AudioSave.ResetBridgeState();

            // Trigger a save which will do handshake first
            var dummyPath = CreateDummyWavFile();
            if (dummyPath == null)
            {
                Debug.LogError("[BridgeTest] Failed to create dummy file for handshake test");
                return;
            }

            var config = new AudioSave.Config("Handshake Test", testGameId, 120);

            Debug.Log("[BridgeTest] Initiating save (will trigger handshake)...");
            var startTime = Time.realtimeSinceStartup;

            var result = await AudioSave.HandleFileAsync(dummyPath, config);

            var elapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log($"[BridgeTest] Completed in {elapsed:F2}s");
            Debug.Log($"[BridgeTest] Bridge available: {AudioSave.BridgeAvailable}");
            Debug.Log($"[BridgeTest] Result: {(result.Success ? "SUCCESS" : "FAILED")} - {result.Message}");
            if (result.Success)
                Debug.Log($"[BridgeTest] Song ID: {result.SongId}");
        }

        private async void TestSave()
        {
            Debug.Log("\n[BridgeTest] === TESTING SAVE ===");

            var dummyPath = CreateDummyWavFile();
            if (dummyPath == null)
            {
                Debug.LogError("[BridgeTest] Failed to create dummy file");
                return;
            }

            var config = new AudioSave.Config(
                "Bridge Test Song",
                testGameId,
                120,
                new[] { "test", "automated" },
                "Automated test song from BridgeTest.cs"
            );

            Debug.Log($"[BridgeTest] Saving file: {dummyPath}");
            var startTime = Time.realtimeSinceStartup;

            var result = await AudioSave.HandleFileAsync(dummyPath, config);

            var elapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log($"[BridgeTest] === SAVE COMPLETE ===");
            Debug.Log($"[BridgeTest] Time: {elapsed:F2}s");
            Debug.Log($"[BridgeTest] Success: {result.Success}");
            Debug.Log($"[BridgeTest] Message: {result.Message}");
            Debug.Log($"[BridgeTest] Song ID: {result.SongId ?? "(none)"}");
            Debug.Log($"[BridgeTest] Used Bridge: {AudioSave.BridgeAvailable == true}");
        }

        private void TestProgressEvents()
        {
            Debug.Log("\n[BridgeTest] === TESTING PROGRESS EVENTS ===");
            Debug.Log("[BridgeTest] Progress events are subscribed. Run a save (key 2) to see them fire.");
            Debug.Log("[BridgeTest] Note: Progress events only fire when using Bridge protocol, not legacy.");
        }

        private void ResetState()
        {
            Debug.Log("\n[BridgeTest] === RESETTING BRIDGE STATE ===");
            AudioSave.ResetBridgeState();
            Debug.Log("[BridgeTest] Bridge state reset. Next save will re-attempt handshake.");
        }

        private void CheckStatus()
        {
            Debug.Log("\n[BridgeTest] === CURRENT STATUS ===");
            Debug.Log($"[BridgeTest] IsInitialized: {AudioSave.IsInitialized}");
            Debug.Log($"[BridgeTest] BridgeAvailable: {(AudioSave.BridgeAvailable?.ToString() ?? "null (not yet tested)")}");

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[BridgeTest] Platform: WebGL Build");
#elif UNITY_EDITOR
            Debug.Log("[BridgeTest] Platform: Unity Editor (Bridge disabled)");
#else
            Debug.Log("[BridgeTest] Platform: Standalone (Bridge disabled)");
#endif
        }

        private string CreateDummyWavFile()
        {
            if (!createDummyWavFile)
            {
                Debug.LogWarning("[BridgeTest] Dummy WAV creation disabled. Provide a real file.");
                return null;
            }

            try
            {
                // Create a minimal valid WAV file (1 second of silence)
                var sampleRate = 44100;
                var channels = 2;
                var bitsPerSample = 16;
                var duration = 1.0f; // 1 second
                var numSamples = (int)(sampleRate * duration);

                var dataSize = numSamples * channels * (bitsPerSample / 8);
                var fileSize = 44 + dataSize;

                var wav = new byte[fileSize];

                // RIFF header
                WriteString(wav, 0, "RIFF");
                WriteInt32(wav, 4, fileSize - 8);
                WriteString(wav, 8, "WAVE");

                // fmt chunk
                WriteString(wav, 12, "fmt ");
                WriteInt32(wav, 16, 16); // chunk size
                WriteInt16(wav, 20, 1);  // PCM format
                WriteInt16(wav, 22, (short)channels);
                WriteInt32(wav, 24, sampleRate);
                WriteInt32(wav, 28, sampleRate * channels * (bitsPerSample / 8)); // byte rate
                WriteInt16(wav, 32, (short)(channels * (bitsPerSample / 8))); // block align
                WriteInt16(wav, 34, (short)bitsPerSample);

                // data chunk
                WriteString(wav, 36, "data");
                WriteInt32(wav, 40, dataSize);
                // Audio data is all zeros (silence) - already initialized

                var path = Path.Combine(Application.temporaryCachePath, $"bridge_test_{DateTime.Now:HHmmss}.wav");
                File.WriteAllBytes(path, wav);

                Debug.Log($"[BridgeTest] Created dummy WAV: {path} ({wav.Length} bytes)");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BridgeTest] Failed to create dummy WAV: {e.Message}");
                return null;
            }
        }

        private static void WriteString(byte[] buffer, int offset, string value)
        {
            for (int i = 0; i < value.Length; i++)
                buffer[offset + i] = (byte)value[i];
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt16(byte[] buffer, int offset, short value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
