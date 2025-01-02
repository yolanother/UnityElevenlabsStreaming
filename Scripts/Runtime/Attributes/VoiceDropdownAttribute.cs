using System;

namespace DoubTech.ElevenLabs.Streaming
{
    /// <summary>
    /// Attribute to display the voice field as a dropdown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class VoiceDropdownAttribute : Attribute
    {
    }
}