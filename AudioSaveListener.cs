using UnityEngine;

namespace Overture.Export
{
    public class AudioSaveListener : MonoBehaviour
    {
        public bool IsAwaiting { get; set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void OnAchievementsReceived(string uploadResultJson)
        {
            Debug.Log("Received upload result: " + uploadResultJson);
            IsAwaiting = false;
        }
    }
}
