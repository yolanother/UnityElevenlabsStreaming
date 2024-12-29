using System.Collections.Generic;

/// <summary>
/// Represents the normalized alignment data for characters in audio transcription.
/// </summary>
namespace Doubtech.ElevenLabs.Streaming.Data
{
    public class NormalizedAlignment
    {
        /// <summary>
        /// List of characters from the audio transcription.
        /// </summary>
        public List<string> Chars { get; set; } = new List<string>();

        /// <summary>
        /// List of start times (in milliseconds) for each character.
        /// </summary>
        public List<int> CharStartTimesMs { get; set; } = new List<int>();

        /// <summary>
        /// List of durations (in milliseconds) for each character.
        /// </summary>
        public List<int> CharDurationsMs { get; set; } = new List<int>();
    }

    /// <summary>
    /// Represents alignment data for characters in audio transcription.
    /// </summary>
    public class Alignment
    {
        /// <summary>
        /// List of characters from the audio transcription.
        /// </summary>
        public List<string> Chars { get; set; } = new List<string>();

        /// <summary>
        /// List of start times (in milliseconds) for each character.
        /// </summary>
        public List<int> CharStartTimesMs { get; set; } = new List<int>();

        /// <summary>
        /// List of durations (in milliseconds) for each character.
        /// </summary>
        public List<int> CharDurationsMs { get; set; } = new List<int>();
    }

    /// <summary>
    /// Represents the audio data and associated alignment information.
    /// </summary>
    public class AudioData
    {
        /// <summary>
        /// Encoded audio data as a base64 string.
        /// </summary>
        public string Audio { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the audio data is finalized.
        /// </summary>
        public bool? IsFinal { get; set; }

        /// <summary>
        /// Normalized alignment data for the audio transcription.
        /// </summary>
        public NormalizedAlignment NormalizedAlignment { get; set; } = new NormalizedAlignment();

        /// <summary>
        /// Alignment data for the audio transcription.
        /// </summary>
        public Alignment Alignment { get; set; } = new Alignment();
        
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
