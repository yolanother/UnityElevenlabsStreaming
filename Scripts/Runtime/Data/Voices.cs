using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DoubTech.ElevenLabs.Streaming.Data
{
    [Serializable]
    public class VoiceResponse
    {
        [JsonProperty("voices")] public List<Voices> Voices { get; set; }
        
        /// <summary>
        /// Fetches voice data from the Eleven Labs API.
        /// </summary>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <returns>A task that returns a VoiceResponse object containing the voice data.</returns>
        public static async Task<VoiceResponse> FetchVoicesAsync(string apiKey)
        {
            var httpClient = new HttpClient();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));
            }

            // Set up the request
            var requestUri = "https://api.elevenlabs.io/v1/voices";
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("xi-api-key", apiKey);

            // Send the request
            var response = await httpClient.GetAsync(requestUri);
            response.EnsureSuccessStatusCode();

            // Read and deserialize the response
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<VoiceResponse>(content);
        }
    }

    [Serializable]
    public class Voices
    {
        [JsonProperty("voice_id")] public string VoiceId { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("category")] public string Category { get; set; }

        [JsonProperty("fine_tuning")] public FineTuning FineTuning { get; set; }

        [JsonProperty("labels")] public Labels Labels { get; set; }

        [JsonProperty("preview_url")] public string PreviewUrl { get; set; }

        [JsonProperty("available_for_tiers")] public List<string> AvailableForTiers { get; set; }

        [JsonProperty("high_quality_base_model_ids")]
        public List<string> HighQualityBaseModelIds { get; set; }

        [JsonProperty("voice_verification")] public VoiceVerification VoiceVerification { get; set; }
    }

    [Serializable]
    public class FineTuning
    {
        [JsonProperty("is_allowed_to_fine_tune")]
        public bool IsAllowedToFineTune { get; set; }

        [JsonProperty("verification_failures")]
        public List<string> VerificationFailures { get; set; }

        [JsonProperty("verification_attempts_count")]
        public int VerificationAttemptsCount { get; set; }

        [JsonProperty("manual_verification_requested")]
        public bool ManualVerificationRequested { get; set; }

        [JsonProperty("finetuning_state")] public string FinetuningState { get; set; }
    }

    [Serializable]
    public class Labels
    {
        [JsonProperty("accent")] public string Accent { get; set; }

        [JsonProperty("description")] public string Description { get; set; }

        [JsonProperty("age")] public string Age { get; set; }

        [JsonProperty("gender")] public string Gender { get; set; }

        [JsonProperty("use_case")] public string UseCase { get; set; }
    }

    [Serializable]
    public class VoiceVerification
    {
        [JsonProperty("requires_verification")]
        public bool RequiresVerification { get; set; }

        [JsonProperty("is_verified")] public bool IsVerified { get; set; }

        [JsonProperty("verification_failures")]
        public List<string> VerificationFailures { get; set; }

        [JsonProperty("verification_attempts_count")]
        public int VerificationAttemptsCount { get; set; }
    }
}