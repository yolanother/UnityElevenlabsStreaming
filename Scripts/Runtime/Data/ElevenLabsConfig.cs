#if VOICESDK
using Meta.WitAi.Attributes;
#endif
using System;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming.Data
{
    [CreateAssetMenu(fileName = "ElevenLabsConfig", menuName = "Eleven Labs/Config", order = 0)]
    public class ElevenLabsConfig : ScriptableObject
    {
        [Header("API Key")]
#if VOICESDK
        [HiddenText]
#endif
        [SerializeField] public string apiKey;
        
        [Header("Model Configuration")]
        [SerializeField] public string model = "eleven_flash_v2_5";
        [SerializeField] public string voice = "21m00Tcm4TlvDq8ikWAM";
        
        [Header("Request Configuration")]
        [SerializeField] public int optimizeStreamingLatency = 4;
        [SerializeField] public bool enableSsml = false;
        [SerializeField] public bool syncAlignment = false;
        
        [Header("Response Configuration")]
        [SerializeField] public AudioEncoding outputFormat = AudioEncoding.pcm_24000;
        
        [Header("Endpoint Configuration")]
        [SerializeField] private string host = "api.elevenlabs.io";
        [SerializeField] private int port = 443;
        [SerializeField] private string schema = "wss";

        
        public string Url
        {
            get
            {
                UriBuilder uriBuilder = new UriBuilder(schema, host);
                uriBuilder.Path = $"v1/text-to-speech/{voice}/stream-input";
                
                // Query Params:
                // 
                // model_id: string
                // optimize_streaming_latency: int
                // output_format: (string) AudioEncoding
                // enable_ssml: bool
                // sync_alignment: bool
                
                uriBuilder.Query = $"model_id={model}&optimize_streaming_latency={optimizeStreamingLatency}&output_format={outputFormat}";
                if (enableSsml)
                {
                    uriBuilder.Query += "&enable_ssml_parsing=true";
                }
                if (syncAlignment)
                {
                    uriBuilder.Query += "&sync_alignment=true";
                }

                return uriBuilder.ToString();
            }
        }
    }
}