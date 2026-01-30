using UnityEngine;
using System.Collections.Generic;

namespace Overture.Export
{
    public partial class AudioExport
    {
        private class Event
        {
            public AudioClip Clip { get; set; }
            public float StartTime { get; set; }
            public Envelope Envelope { get; set; }
        }

        private List<Event> _audioEvents = new();
        private Options _options;

        public AudioExport(Options options = null) => _options = options ?? Options.Infer;

        public void AddClip(AudioClip clip, float timeSeconds, Envelope envelope = null)
        {
            envelope ??= Envelope.Full();

            if (clip == null)
                return;

            if (_options.TargetSampleRate <= 0)
                _options.TargetSampleRate = clip.frequency;

            var effectiveDuration = (envelope.Duration < 0) ? clip.length : envelope.Duration;
            envelope.Duration = Mathf.Max(0f, effectiveDuration);
            envelope.Volume = Mathf.Clamp01(envelope.Volume);
            envelope.Release = Mathf.Max(0f, envelope.Release);

            _audioEvents.Add(new Event
            {
                Clip = clip,
                StartTime = timeSeconds,
                Envelope = envelope
            });
        }

        public float[] GetMixedAudioData(int targetSampleRate, int targetChannels)
        {
            if (_audioEvents.Count == 0)
                return new float[0];

            var maxEndTime = 0f;
            foreach (var audioEvent in _audioEvents)
            {
                if (audioEvent.Clip.frequency != targetSampleRate || audioEvent.Clip.channels != targetChannels)
                    Debug.LogWarning($"Clip '{audioEvent.Clip.name}' has different sample rate ({audioEvent.Clip.frequency}Hz) or channels ({audioEvent.Clip.channels}) than target ({targetSampleRate}Hz, {targetChannels} channels). This might cause issues. For a real DAW, implement resampling/channel conversion.");

                var naturalClipEndTime = audioEvent.StartTime + audioEvent.Clip.length;
                var cutOrFadeEndTime = audioEvent.StartTime + audioEvent.Envelope.Duration + audioEvent.Envelope.Release;
                maxEndTime = Mathf.Max(maxEndTime, naturalClipEndTime, cutOrFadeEndTime);
            }

            if (maxEndTime <= 0f)
                return new float[0];

            var totalSamples = Mathf.CeilToInt(maxEndTime * targetSampleRate) * targetChannels;
            var buffer = new float[totalSamples];

            foreach (var audioEvent in _audioEvents)
            {
                var clipData = new float[audioEvent.Clip.samples * audioEvent.Clip.channels];
                audioEvent.Clip.GetData(clipData, 0);

                var startSampleIndex = (int)(audioEvent.StartTime * targetSampleRate) * targetChannels;
                var fadeStartTime = audioEvent.StartTime + audioEvent.Envelope.Duration;

                for (var i = 0; i < clipData.Length; i++)
                {
                    var bufferIndex = startSampleIndex + i;
                    if (bufferIndex >= buffer.Length) break;

                    var timeInMix = bufferIndex / (float)targetChannels / targetSampleRate;
                    var volume = audioEvent.Envelope.Volume;

                    if (timeInMix >= fadeStartTime)
                    {
                        if (audioEvent.Envelope.Release > 0)
                        {
                            var timeIntoRelease = timeInMix - fadeStartTime;
                            var fadeFactor = 1f - (timeIntoRelease / audioEvent.Envelope.Release);
                            volume *= Mathf.Clamp01(fadeFactor);
                        }
                        else
                            volume = 0f;
                    }

                    // Exit early if fully faded out
                    if (volume <= 0f && timeInMix >= fadeStartTime)
                        break;

                    buffer[bufferIndex] += clipData[i] * volume;
                }
            }

            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = Mathf.Clamp(buffer[i], -1f, 1f);

            return buffer;
        }
    }
}