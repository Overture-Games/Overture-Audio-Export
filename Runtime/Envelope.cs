namespace Overture.Export
{
    public class Envelope
    {
        public float Volume { get; set; } = 1f;
        public float Duration { get; set; } = -1f;
        public float Release { get; set; } = 0f;

        public static Envelope Full(float volume = 1f)
        {
            return new()
            {
                Volume = volume,
                Duration = -1f,
                Release = 0f
            };
        }

        public static Envelope Stop(float duration, float volume = 1f)
        {
            return new()
            {
                Volume = volume,
                Duration = duration,
                Release = 0f
            };
        }

        public static Envelope GentleStop(float duration, float volume = 1f)
        {
            return new()
            {
                Volume = volume,
                Duration = duration,
                Release = 0.05f
            };
        }
    }
}