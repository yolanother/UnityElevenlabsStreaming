using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DoubTech.ElevenLabs.Streaming
{
    [System.Serializable]
    public class Model
    {
        public string model_id;
        public string name;
        public string description;
        public bool can_be_finetuned;
        public bool can_do_text_to_speech;
        public bool can_do_voice_conversion;
        public bool can_use_style;
        public bool can_use_speaker_boost;
        public bool serves_pro_voices;
        public float token_cost_factor;
        public bool requires_alpha_access;
        public int max_characters_request_free_user;
        public int max_characters_request_subscribed_user;
        public List<Language> languages;
    }

    [System.Serializable]
    public class Language
    {
        public string language_id;
        public string name;
    }

    [System.Serializable]
    public class ModelResponse
    {
        public List<Model> Models;

        public static async Task<ModelResponse> FetchModelsAsync(string apiKey)
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("xi-api-key", apiKey);

            var response = await httpClient.GetStringAsync("https://api.elevenlabs.io/v1/models");
            return JsonUtility.FromJson<ModelResponse>($"{{\"Models\": {response}}}");
        }
    }
}