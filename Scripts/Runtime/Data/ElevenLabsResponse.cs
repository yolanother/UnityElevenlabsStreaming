using System.Collections.Generic;

namespace Doubtech.ElevenLabs.Streaming.Data
{
    public class NormalizedAlignment
    {
        public List<string> Chars { get; set; }
        public List<int> CharStartTimesMs { get; set; }
        public List<int> CharDurationsMs { get; set; }
    }

    public class Alignment
    {
        public List<string> Chars { get; set; }
        public List<int> CharStartTimesMs { get; set; }
        public List<int> CharDurationsMs { get; set; }
    }

    public class AudioData
    {
        public string Audio { get; set; }
        public bool? IsFinal { get; set; }
        public NormalizedAlignment NormalizedAlignment { get; set; }
        public Alignment Alignment { get; set; }
    }
}