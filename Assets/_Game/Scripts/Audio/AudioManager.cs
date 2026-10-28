using System;
using UnityEngine;

namespace DashMasters
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Settings")]
        [SerializeField] private bool musicEnabled = true;
        [SerializeField] private bool sfxEnabled = true;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

        private AudioSource musicSource;
        private AudioSource sfxSource;

        private AudioClip coinClip;
        private AudioClip jumpClip;
        private AudioClip boostClip;
        private AudioClip hitClip;
        private AudioClip countdownTickClip;
        private AudioClip countdownGoClip;
        private AudioClip victoryClip;
        private AudioClip defeatClip;
        private AudioClip splashClip;
        private AudioClip bgmClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.playOnAwake = false;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.volume = sfxVolume;
            sfxSource.playOnAwake = false;

            GenerateProceduralAudio();
        }

        private void Start()
        {
            if (musicEnabled && bgmClip != null)
            {
                PlayMusic();
            }
        }

        public void PlayMusic()
        {
            if (!musicEnabled || bgmClip == null) return;
            musicSource.clip = bgmClip;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }

        public void StopMusic()
        {
            musicSource.Stop();
        }

        public void PlayCoinSound() => PlayClip(coinClip, 1.0f);
        public void PlayJumpSound() => PlayClip(jumpClip, 0.9f);
        public void PlayBoostSound() => PlayClip(boostClip, 1.0f);
        public void PlayHitSound() => PlayClip(hitClip, 1.0f);
        public void PlayCountdownTick() => PlayClip(countdownTickClip, 0.8f);
        public void PlayCountdownGo() => PlayClip(countdownGoClip, 1.0f);
        public void PlayVictorySound() => PlayClip(victoryClip, 1.0f);
        public void PlayDefeatSound() => PlayClip(defeatClip, 0.9f);
        public void PlaySplashSound() => PlayClip(splashClip, 1.0f);

        private void PlayClip(AudioClip clip, float volumeScale)
        {
            if (!sfxEnabled || clip == null) return;
            sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        private void GenerateProceduralAudio()
        {
            coinClip = CreateCoinSound();
            jumpClip = CreateJumpSound();
            boostClip = CreateBoostSound();
            hitClip = CreateHitSound();
            countdownTickClip = CreateCountdownTickSound();
            countdownGoClip = CreateCountdownGoSound();
            victoryClip = CreateVictorySound();
            defeatClip = CreateDefeatSound();
            splashClip = CreateSplashSound();
            bgmClip = CreateBackgroundTrack();
        }

        private AudioClip CreateCoinSound()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.1f ? 987.77f : 1318.51f; // B5 then E6
                float env = 1f - (t / duration);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                wave += 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t);
                data[i] = wave * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("Coin_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateJumpSound()
        {
            int sampleRate = 44100;
            float duration = 0.2f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float freq = Mathf.Lerp(260f, 620f, progress);
                float env = Mathf.Sin(progress * Mathf.PI);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = wave * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("Jump_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateBoostSound()
        {
            int sampleRate = 44100;
            float duration = 0.4f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float freq = Mathf.Lerp(400f, 1200f, Mathf.Pow(progress, 0.7f));
                float vibrato = Mathf.Sin(2f * Mathf.PI * 30f * t) * 40f;
                float env = 1f - progress;
                float wave = Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * t);
                data[i] = wave * env * 0.35f;
            }

            AudioClip clip = AudioClip.Create("Boost_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateHitSound()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rnd = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float lowSine = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 50f, progress) * t);
                float env = Mathf.Pow(1f - progress, 2.5f);
                data[i] = (lowSine * 0.6f + noise * 0.4f) * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Hit_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateCountdownTickSound()
        {
            int sampleRate = 44100;
            float duration = 0.12f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                float wave = Mathf.Sin(2f * Mathf.PI * 520f * t);
                data[i] = wave * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("Tick_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateCountdownGoSound()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                float wave = Mathf.Sin(2f * Mathf.PI * 1046.5f * t); // C6
                wave += 0.25f * Mathf.Sin(2f * Mathf.PI * 2093f * t);
                data[i] = wave * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Go_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateVictorySound()
        {
            int sampleRate = 44100;
            float duration = 0.9f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
            float noteDur = duration / notes.Length;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Clamp((int)(t / noteDur), 0, notes.Length - 1);
                float noteT = t - (noteIndex * noteDur);
                float freq = notes[noteIndex];
                float env = 1f - (noteT / noteDur);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = wave * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("Victory_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateDefeatSound()
        {
            int sampleRate = 44100;
            float duration = 0.8f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            float[] notes = { 440f, 370f, 311.13f, 261.63f }; // A4, F#4, Eb4, C4
            float noteDur = duration / notes.Length;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Clamp((int)(t / noteDur), 0, notes.Length - 1);
                float noteT = t - (noteIndex * noteDur);
                float freq = notes[noteIndex];
                float env = 1f - (noteT / noteDur);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = wave * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("Defeat_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateSplashSound()
        {
            int sampleRate = 44100;
            float duration = 0.55f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rnd = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-5.5f * t);
                // Filtered noise + low frequency thud
                float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.45f;
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 60f, t / duration) * t) * 0.55f;
                data[i] = (noise + thud) * env;
            }

            AudioClip clip = AudioClip.Create("Water_Splash_SFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateBackgroundTrack()
        {
            int sampleRate = 22050;
            float loopDuration = 6.0f; // 6-second upbeat loop
            int samples = Mathf.CeilToInt(sampleRate * loopDuration);
            float[] data = new float[samples];
            float[] bassPattern = { 130.81f, 130.81f, 164.81f, 146.83f, 174.61f, 164.81f, 146.83f, 130.81f }; // C3, E3, D3, F3...

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float step = (t % 0.375f) / 0.375f;
                int patternIdx = (int)((t / 0.375f) % bassPattern.Length);
                float freq = bassPattern[patternIdx];

                // Bass synth
                float bassEnv = Mathf.Pow(1f - step, 2.0f);
                float bass = Mathf.Sin(2f * Mathf.PI * freq * t) * bassEnv * 0.25f;

                // High hi-hat click
                float hat = 0f;
                if ((t % 0.1875f) < 0.04f)
                {
                    hat = (Mathf.Sin(2f * Mathf.PI * 3500f * t) * (1f - (t % 0.1875f) / 0.04f)) * 0.08f;
                }

                data[i] = Mathf.Clamp(bass + hat, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Race_BGM_Loop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
