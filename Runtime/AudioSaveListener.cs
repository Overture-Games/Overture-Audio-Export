using System;
using UnityEngine;

namespace Overture.Export
{
    public class AudioSaveListener : MonoBehaviour
    {
        // Legacy upload result
        public bool IsAwaiting { get; set; }
        public string UploadResultJson { get; private set; }

        // Bridge state
        public bool HandshakeReceived { get; private set; }
        public bool HandshakeSupported { get; private set; }
        public string HandshakeRequestId { get; private set; }

        public bool SaveAckReceived { get; private set; }
        public string SaveAckRequestId { get; private set; }

        public string LastProgressRequestId { get; private set; }
        public float LastProgressPercent { get; private set; }
        public string LastProgressStage { get; private set; }

        public bool SaveResultReceived { get; private set; }
        public string SaveResultRequestId { get; private set; }
        public bool SaveResultSuccess { get; private set; }
        public string SaveResultSongId { get; private set; }
        public string SaveResultError { get; private set; }

        // Events for progress updates
        public event Action<float, string> OnProgressReceived;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void ResetHandshakeState()
        {
            HandshakeReceived = false;
            HandshakeSupported = false;
            HandshakeRequestId = null;
        }

        public void ResetSaveState()
        {
            SaveAckReceived = false;
            SaveAckRequestId = null;
            LastProgressRequestId = null;
            LastProgressPercent = 0;
            LastProgressStage = null;
            SaveResultReceived = false;
            SaveResultRequestId = null;
            SaveResultSuccess = false;
            SaveResultSongId = null;
            SaveResultError = null;
        }

        // Called from JS: Legacy platform upload result
        public void OnPlatformUploadResult(string uploadResultJson)
        {
            UploadResultJson = uploadResultJson;
            Debug.Log("[AudioSaveListener] Legacy upload result: " + uploadResultJson);
            IsAwaiting = false;
        }

        // Called from JS: Bridge handshake response
        public void OnBridgeHandshakeResult(string json)
        {
            Debug.Log("[AudioSaveListener] Bridge handshake result: " + json);
            try
            {
                var result = JsonUtility.FromJson<HandshakeResult>(json);
                HandshakeReceived = true;
                HandshakeSupported = result.supported;
                HandshakeRequestId = result.requestId;
            }
            catch (Exception e)
            {
                Debug.LogError("[AudioSaveListener] Failed to parse handshake result: " + e.Message);
                HandshakeReceived = true;
                HandshakeSupported = false;
            }
        }

        // Called from JS: Bridge save acknowledgment
        public void OnBridgeSaveAck(string requestId)
        {
            Debug.Log("[AudioSaveListener] Bridge save acknowledged: " + requestId);
            SaveAckReceived = true;
            SaveAckRequestId = requestId;
        }

        // Called from JS: Bridge save progress
        public void OnBridgeSaveProgress(string json)
        {
            Debug.Log("[AudioSaveListener] Bridge save progress: " + json);
            try
            {
                var progress = JsonUtility.FromJson<ProgressResult>(json);
                LastProgressRequestId = progress.requestId;
                LastProgressPercent = progress.percent;
                LastProgressStage = progress.stage;

                // Fire event for external listeners
                OnProgressReceived?.Invoke(progress.percent / 100f, progress.stage);
            }
            catch (Exception e)
            {
                Debug.LogError("[AudioSaveListener] Failed to parse progress: " + e.Message);
            }
        }

        // Called from JS: Bridge save final result
        public void OnBridgeSaveResult(string json)
        {
            Debug.Log("[AudioSaveListener] Bridge save result: " + json);
            try
            {
                var result = JsonUtility.FromJson<SaveResult>(json);
                SaveResultReceived = true;
                SaveResultRequestId = result.requestId;
                SaveResultSuccess = result.success;
                SaveResultSongId = result.songId;
                SaveResultError = result.error;
            }
            catch (Exception e)
            {
                Debug.LogError("[AudioSaveListener] Failed to parse save result: " + e.Message);
                SaveResultReceived = true;
                SaveResultSuccess = false;
                SaveResultError = "Failed to parse result: " + e.Message;
            }
        }

        [Serializable]
        private class HandshakeResult
        {
            public bool supported;
            public string[] capabilities;
            public string version;
            public string requestId;
        }

        [Serializable]
        private class ProgressResult
        {
            public string requestId;
            public float percent;
            public string stage;
        }

        [Serializable]
        private class SaveResult
        {
            public string requestId;
            public bool success;
            public string songId;
            public string error;
        }
    }
}
