#if VOICESDK
using Meta.WitAi.Attributes;
#endif
using System;
using Doubtech.ElevenLabs.Streaming.Data;
using DoubTech.ElevenLabs.Streaming.Data;
using UnityEngine;

namespace DoubTech.ElevenLabs.Streaming
{
    /// <summary>
    /// Configuration for Eleven Labs integration, including API key, model, and request settings.
    /// </summary>
    [CreateAssetMenu(fileName = "ElevenLabsConfig", menuName = "Eleven Labs/Config", order = 0)]
    public partial class ElevenLabsConfig : ScriptableObject
    {
        [Header("API Key")]
        [Tooltip("The API key for accessing Eleven Labs services.")]
#if VOICESDK
        [HiddenText]
#endif
        [SerializeField] internal string apiKey;

        [Header("Model Configuration")]
        [Tooltip("The model to use for text-to-speech.")]
        [SerializeField] internal string model = "eleven_flash_v2_5";
        
        [Tooltip("The voice identifier for text-to-speech.")]
        [VoiceDropdown]
        [SerializeField] internal string voice = "21m00Tcm4TlvDq8ikWAM";

        [Header("Request Configuration")]
        [Tooltip("Optimize latency for streaming (in milliseconds).")]
        [SerializeField] internal int optimizeStreamingLatency = 4;
        
        [Tooltip("Enable SSML (Speech Synthesis Markup Language) parsing.")]
        [SerializeField] internal bool enableSsml = false;
        
        [Tooltip("Synchronize alignment for the speech output.")]
        [SerializeField] internal bool syncAlignment = false;

        [Header("Response Configuration")]
        [Tooltip("The output audio encoding format.")]
        [SerializeField] internal AudioEncoding outputFormat = AudioEncoding.pcm_24000;

        [Header("Endpoint Configuration")]
        [Tooltip("The host URL for the Eleven Labs API.")]
        [SerializeField] internal string host = "api.elevenlabs.io";
        
        [Tooltip("The port number for the Eleven Labs API.")]
        [SerializeField] internal int port = 443;
        
        [Tooltip("The schema (protocol) used for the API connection.")]
        [SerializeField] internal string schema = "wss";
        
        [Header("Voice Data")]
        [Tooltip("The voices available for text-to-speech.")]
        [HideInInspector]
        [SerializeField] internal VoiceResponse voices;
        
        [Header("Model Data")]
        [Tooltip("The models available for text-to-speech.")]
        [HideInInspector]
        [SerializeField] internal ModelResponse models;

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
