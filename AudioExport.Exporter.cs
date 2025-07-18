using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace Overture.Export
{
    public partial class AudioExport
    {
        public class Options
        {
            public int TargetSampleRate { get; set; }
            public int TargetChannels { get; set; }
            public int BitsPerSample { get; set; }

            public Options(int targetSampleRate = 0, int targetChannels = 2, int bitsPerSample = 16)
            {
                TargetSampleRate = targetSampleRate;
                TargetChannels = targetChannels;
                BitsPerSample = bitsPerSample;
            }

            public static Options Default => new(AudioSettings.outputSampleRate, 2, 16);
            public static Options Infer => new(0, 2, 16);
        }

        [Serializable]
        public class ExportResult
        {
            public bool Success { get; set; }
            public string PathOrError { get; set; }
        }

        public static async Awaitable<ExportResult> ToFileAsync(AudioExport export, Action<ExportResult> callback)
        {
            var result = await ToFileAsync(export);
            callback?.Invoke(result);
            return result;
        }

        public static async Awaitable<ExportResult> ToFileAsync(AudioExport export)
        {
            if (export == null)
            {
                Debug.LogError("AudioFileExporter: AudioExport data is null. Cannot export.");
                return new ExportResult { Success = false, PathOrError = "AudioExport data was null." };
            }

            Debug.Log("AudioFileExporter: Starting audio export...");

            // Mix down all recorded clips into a float[] buffer
            var masterBuffer = export.GetMixedAudioData(export._options.TargetSampleRate, export._options.TargetChannels);
            Debug.Log($"AudioFileExporter: Mixed buffer length = {masterBuffer?.Length}");

            if (masterBuffer == null || masterBuffer.Length == 0)
            {
                Debug.LogWarning("AudioFileExporter: Master buffer is empty. Nothing to export.");
                return new ExportResult { Success = false, PathOrError = "The mixed audio buffer was empty." };
            }

            // Build the file path
            var filePath = Path.Combine(Application.persistentDataPath, GenerateTempFileName());
            Debug.Log($"AudioFileExporter: Preparing to save WAV to: {filePath}");

            // Write header + samples in a coroutine
            return await WriteWavFileAsync(export, filePath, masterBuffer);
        }

        private static async Awaitable<ExportResult> WriteWavFileAsync(AudioExport export, string filePath, float[] masterBuffer)
        {
            FileStream fileStream = null;
            BinaryWriter writer = null;
            string headerError = null;

            try
            {
                fileStream = new FileStream(filePath, FileMode.Create);
                writer = new BinaryWriter(fileStream);

                int byteRate = export._options.TargetSampleRate * export._options.TargetChannels * (export._options.BitsPerSample / 8);
                short blockAlign = (short)(export._options.TargetChannels * (export._options.BitsPerSample / 8));
                int dataSize = masterBuffer.Length * (export._options.BitsPerSample / 8);
                int riffHeaderSize = 36 + dataSize;

                // RIFF chunk
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(riffHeaderSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt subchunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size = 16 for PCM
                writer.Write((short)1); // AudioFormat = PCM (1)
                writer.Write((short)export._options.TargetChannels);
                writer.Write(export._options.TargetSampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write((short)export._options.BitsPerSample);

                // data subchunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
            }
            catch (Exception ex)
            {
                headerError = ex.Message;
            }

            if (headerError != null)
            {
                writer?.Dispose();
                fileStream?.Dispose();
                return new ExportResult { Success = false, PathOrError = $"Failed to write WAV header: {headerError}" };
            }

            for (int i = 0; i < masterBuffer.Length; i++)
            {
                try
                {
                    short sample16 = (short)(masterBuffer[i] * 32767f);
                    writer.Write(sample16);
                }
                catch (Exception ex)
                {
                    writer?.Dispose();
                    fileStream?.Dispose();
                    return new ExportResult { Success = false, PathOrError = $"Error writing sample #{i}: {ex.Message}" };
                }

                // Yield periodically to prevent the application from freezing
                if (i > 0 && i % (export._options.TargetSampleRate * export._options.TargetChannels) == 0)
                    await Awaitable.NextFrameAsync();
            }

            writer.Dispose();
            fileStream.Dispose();
            Debug.Log("AudioFileExporter: WAV file written successfully.");

            return new ExportResult { Success = true, PathOrError = filePath };
        }

        private static string GenerateTempFileName()
        {
            var timestamp = DateTime.Now.ToString("MMdd_HHmm");
            return $"audio_export_{timestamp}.wav";
        }
    }
}
