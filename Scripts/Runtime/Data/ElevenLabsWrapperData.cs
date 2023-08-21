#if VOICESDK
using Meta.WitAi.Attributes;
#endif
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming.Data
{
    [CreateAssetMenu(fileName = "WrapperConfig", menuName = "Eleven Labs/Wrapper Config", order = 0)]
    public class ElevenLabsWrapperData : ScriptableObject
    {
        [SerializeField] private string _host;
        [SerializeField] private int _port;
#if VOICESDK
        [HiddenText]
#endif
        [SerializeField] private string _apiKey;
        
        private string _url = "ws://{0}:{1}/ws/synthesize?apikey={2}&voice={3}";

        public string Url(string _voiceId) => string.Format(_url, _host, _port, _apiKey, _voiceId);
    }
}