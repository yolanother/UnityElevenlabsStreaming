using System.Linq;
using DoubTech.ElevenLabs.Streaming.Data;
using UnityEditor;
using UnityEngine;

namespace DoubTech.ElevenLabs.Streaming
{
    [CustomEditor(typeof(ElevenLabsConfig))]
    public class ElevenLabsConfigEditor : Editor
    {
        private int selectedModelIndex = 0;
        private bool showApiKey = false; // Toggles visibility of the API key
        private bool showEndpointConfig = false; // Toggles visibility of the endpoint configuration section

        public override void OnInspectorGUI()
        {
            ElevenLabsConfig config = (ElevenLabsConfig)target;

            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            bool endpointFoldoutDrawn = false;

            while (property.NextVisible(enterChildren))
            {
                if (property.name == "apiKey")
                {
                    DrawApiKeyField(config);
                }
                else if (property.name == "model")
                {
                    DrawModelDropdown(config);
                }
                else if (property.name == "voice")
                {
                    DrawVoiceDropdown(config);
                }
                else if (property.name == "host" || property.name == "port" || property.name == "schema")
                {
                    if (!endpointFoldoutDrawn)
                    {
                        showEndpointConfig = EditorGUILayout.Foldout(showEndpointConfig, "Endpoint Configuration");
                        endpointFoldoutDrawn = true;
                    }

                    if (showEndpointConfig)
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Fetch Models"))
            {
                FetchModels(config);
            }

            if (GUILayout.Button("Fetch Voices"))
            {
                FetchVoices(config);
            }
        }

        /// <summary>
        /// Draws the API key as a password field with a "View" button.
        /// </summary>
        private void DrawApiKeyField(ElevenLabsConfig config)
        {
            EditorGUILayout.BeginHorizontal();
            if (showApiKey)
            {
                config.apiKey = EditorGUILayout.TextField("API Key", config.apiKey);
            }
            else
            {
                config.apiKey = EditorGUILayout.PasswordField("API Key", config.apiKey);
            }

            if (GUILayout.Button(showApiKey ? "Hide" : "View", GUILayout.Width(50)))
            {
                showApiKey = !showApiKey;
            }

            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }

        private void DrawModelDropdown(ElevenLabsConfig config)
        {
            if (config.models != null && config.models.Models != null && config.models.Models.Any())
            {
                var modelNames = config.models.Models.Select(m => m.name).ToArray();
                var modelIds = config.models.Models.Select(m => m.model_id).ToArray();

                int currentIndex = System.Array.IndexOf(modelIds, config.Model);
                if (currentIndex == -1) currentIndex = 0;

                selectedModelIndex = EditorGUILayout.Popup("Model", currentIndex, modelNames);

                if (selectedModelIndex >= 0 && selectedModelIndex < modelIds.Length)
                {
                    config.model = modelIds[selectedModelIndex];
                    EditorUtility.SetDirty(config);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No models available. Click 'Fetch Models' to load models.", MessageType.Info);
            }
        }

        private void DrawVoiceDropdown(ElevenLabsConfig config)
        {
            if (config.voices != null && config.voices.Voices != null && config.voices.Voices.Any())
            {
                var voiceNames = config.voices.Voices.Select(v => v.Name).ToArray();
                var voiceIds = config.voices.Voices.Select(v => v.VoiceId).ToArray();

                int currentIndex = System.Array.IndexOf(voiceIds, config.Voice);
                if (currentIndex == -1) currentIndex = 0;

                selectedModelIndex = EditorGUILayout.Popup("Voice", currentIndex, voiceNames);

                if (selectedModelIndex >= 0 && selectedModelIndex < voiceIds.Length)
                {
                    config.voice = voiceIds[selectedModelIndex];
                    EditorUtility.SetDirty(config);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No voices available. Click 'Fetch Voices' to load voices.", MessageType.Info);
            }
        }

        private async void FetchModels(ElevenLabsConfig config)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                Debug.LogError("API key is missing. Please set the API key in the ElevenLabsConfig.");
                return;
            }

            try
            {
                var models = await ModelResponse.FetchModelsAsync(config.ApiKey);
                config.models = models;
                selectedModelIndex = 0;
                EditorUtility.SetDirty(config);
                Debug.Log("Models fetched successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to fetch models: {ex.Message}");
            }
        }

        private async void FetchVoices(ElevenLabsConfig config)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                Debug.LogError("API key is missing. Please set the API key in the ElevenLabsConfig.");
                return;
            }

            try
            {
                var voices = await VoiceResponse.FetchVoicesAsync(config.ApiKey);
                config.voices = voices;
                selectedModelIndex = 0;
                EditorUtility.SetDirty(config);
                Debug.Log("Voices fetched successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to fetch voices: {ex.Message}");
            }
        }
    }
}
