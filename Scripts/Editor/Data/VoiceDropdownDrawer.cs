using System.Linq;
using Doubtech.ElevenLabs.Streaming.Data;
using UnityEditor;
using UnityEngine;

namespace DoubTech.ElevenLabs.Streaming
{
    /// <summary>
    /// Custom property drawer for the VoiceDropdownAttribute.
    /// </summary>
    [CustomPropertyDrawer(typeof(VoiceDropdownAttribute))]
    public class VoiceDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Get the target ScriptableObject
            var targetObject = property.serializedObject.targetObject as ElevenLabsConfig;

            if (targetObject == null)
            {
                EditorGUI.LabelField(position, label.text, "Target object not found.");
                return;
            }

            // Check if voices are available
            if (targetObject.voices != null && targetObject.voices.Voices != null && targetObject.voices.Voices.Any())
            {
                // Get voice names and IDs
                var voices = targetObject.voices.Voices;
                var voiceNames = voices.Select(v => v.Name).ToArray();
                var voiceIds = voices.Select(v => v.VoiceId).ToArray();

                // Find the current selection index
                int currentIndex = System.Array.IndexOf(voiceIds, property.stringValue);
                if (currentIndex == -1) currentIndex = 0;

                // Draw the dropdown
                int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, voiceNames);

                // Update the property if selection changes
                if (selectedIndex >= 0 && selectedIndex < voiceIds.Length)
                {
                    property.stringValue = voiceIds[selectedIndex];
                }
            }
            else
            {
                // Show a message if no voices are available
                EditorGUI.LabelField(position, label.text, "No voices available. Fetch voices first.");
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label);
        }
    }
}
