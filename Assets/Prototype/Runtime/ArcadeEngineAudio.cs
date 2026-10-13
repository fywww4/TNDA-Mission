using UnityEngine;

namespace TNDA.Prototype
{
    // Looping engine sound for the human-driven cars, built from a recorded rev clip.
    public class ArcadeEngineAudio
    {
        private AudioSource[] sources;
        private AudioClip loop;

        public void Update(MonoBehaviour host, AudioClip rev, ArcadeVehicle[] cars, bool running, float maxSpeed)
        {
            if (rev == null || !Application.isPlaying) return;
            if (sources == null) Create(host, rev, cars.Length);
            for (int i = 0; i < sources.Length; i++)
            {
                var car = cars[i];
                if (!running || !car.alive || car.computer || !car.gameObject.activeInHierarchy)
                {
                    if (sources[i].isPlaying) sources[i].Stop();
                    continue;
                }
                if (!sources[i].isPlaying) sources[i].Play();
                // Idle burble when stopped, rising pitch and volume with speed.
                float speed = Mathf.Clamp01(Mathf.Abs(car.speed) / maxSpeed);
                sources[i].pitch = .75f + speed * .55f;
                sources[i].volume = .12f + speed * .23f;
            }
        }

        public void Dispose()
        {
            if (loop != null) Object.Destroy(loop);
        }

        private void Create(MonoBehaviour host, AudioClip rev, int carCount)
        {
            // Loop the sustained part of the rev (skipping its attack and fade) with a short crossfade at the seam.
            int rate = rev.frequency, channels = rev.channels;
            var source = new float[rev.samples * channels];
            rev.GetData(source, 0);
            int start = Mathf.Min((int)(.25f * rate), rev.samples / 4), end = Mathf.Max(start + rate / 4, (int)(rev.samples * .8f));
            int fade = Mathf.Min(rate / 20, (end - start) / 4), length = end - start - fade;
            var samples = new float[length * channels];
            for (int s = 0; s < length; s++)
                for (int c = 0; c < channels; c++)
                {
                    float sample = source[(start + s) * channels + c];
                    if (s < fade) sample = Mathf.Lerp(source[(end - fade + s) * channels + c], sample, s / (float)fade);
                    samples[s * channels + c] = sample;
                }
            loop = AudioClip.Create("Engine loop", length, channels, rate, false);
            loop.SetData(samples, 0);
            sources = new AudioSource[Mathf.Min(2, carCount)];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = host.gameObject.AddComponent<AudioSource>();
                sources[i].clip = loop;
                sources[i].loop = true;
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0;
            }
        }
    }
}
