using UnityEngine;

namespace Overture.Export
{
    public class AudioSaveListener : MonoBehaviour
    {
        public bool IsAwaiting { get; set; }
        public string UploadResultJson { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void OnPlatformUploadResult(string uploadResultJson)
        {
            UploadResultJson = uploadResultJson;
            Debug.Log("Received upload result: " + uploadResultJson);
            IsAwaiting = false;
        }
    }
}
