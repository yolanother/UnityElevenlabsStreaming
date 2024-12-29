#if VOICESDK
using Meta.WitAi.Attributes;
#endif
using System;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming.Data
{
    /// <summary>
    /// Configuration for Eleven Labs integration, including API key, model, and request settings.
    /// </summary>
    [CreateAssetMenu(fileName = "ElevenLabsConfig", menuName = "Eleven Labs/Config", order = 0)]
    public class ElevenLabsConfig : ScriptableObject
    {
        [Header("API Key")]
        [Tooltip("The API key for accessing Eleven Labs services.")]
#if VOICESDK
        [HiddenText]
#endif
        [SerializeField] public string apiKey;

        [Header("Model Configuration")]
        [Tooltip("The model to use for text-to-speech.")]
        [SerializeField] private string model = "eleven_flash_v2_5";
        
        [Tooltip("The voice identifier for text-to-speech.")]
        [SerializeField] private string voice = "21m00Tcm4TlvDq8ikWAM";

        [Header("Request Configuration")]
        [Tooltip("Optimize latency for streaming (in milliseconds).")]
        [SerializeField] private int optimizeStreamingLatency = 4;
        
        [Tooltip("Enable SSML (Speech Synthesis Markup Language) parsing.")]
        [SerializeField] private bool enableSsml = false;
        
        [Tooltip("Synchronize alignment for the speech output.")]
        [SerializeField] private bool syncAlignment = false;

        [Header("Response Configuration")]
        [Tooltip("The output audio encoding format.")]
        [SerializeField] private AudioEncoding outputFormat = AudioEncoding.pcm_24000;

        [Header("Endpoint Configuration")]
        [Tooltip("The host URL for the Eleven Labs API.")]
        [SerializeField] private string host = "api.elevenlabs.io";
        
        [Tooltip("The port number for the Eleven Labs API.")]
        [SerializeField] private int port = 443;
        
        [Tooltip("The schema (protocol) used for the API connection.")]
        [SerializeField] private string schema = "wss";

        /// <summary>
        /// Constructs the full URL for API requests based on the configuration settings.
        /// </summary>
        public string Url
        {
            get
            {
                UriBuilder uriBuilder = new UriBuilder(schema, host)
                {
                    Path = $"v1/text-to-speech/{voice}/stream-input"
                };

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

        /// <summary>
        /// Public accessor for the API key.
        /// </summary>
        public string ApiKey => apiKey;

        /// <summary>
        /// Public accessor for the model ID.
        /// </summary>
        public string Model => model;

        /// <summary>
        /// Public accessor for the voice ID.
        /// </summary>
        public string Voice => voice;
    }
}
