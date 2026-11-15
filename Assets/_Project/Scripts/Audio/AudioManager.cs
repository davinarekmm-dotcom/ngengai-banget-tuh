using System;
using System.Collections;
using UnityEngine;

namespace Room0317.Audio
{
    public enum FootstepSurface
    {
        Carpet,
        Wood,
        Tile
    }

    /// <summary>
    /// Master audio manager for ROOM 03:17.
    /// Manages atmospheric horror ambient drones, dynamic heartbeat modulation,
    /// clock ticking, door creaks, clue pickups, and psychological stingers.
    /// Features built-in procedural audio waveform synthesis so the game possesses
    /// rich, immersive audio out-of-the-box with ZERO external audio assets required.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Custom Audio Overrides (Optional)")]
        [Tooltip("Assign your own .wav/.mp3 clips here. If left empty, procedural horror audio will be generated automatically.")]
        [SerializeField] private AudioClip customAmbientDrone;
        [SerializeField] private AudioClip customHeartbeat;
        [SerializeField] private AudioClip customDoorTransition;
        [SerializeField] private AudioClip customKeyPickup;
        [SerializeField] private AudioClip customNotePickup;
        [SerializeField] private AudioClip customHorrorStinger;
        [SerializeField] private AudioClip customClockTick;
        [SerializeField] private AudioClip customObjectiveChime;
        [SerializeField] private AudioClip customFootstepCarpet;
        [SerializeField] private AudioClip customFootstepWood;
        [SerializeField] private AudioClip customFootstepTile;

        [Header("Volume Balances")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.85f;

        private AudioSource ambientSource;
        private AudioSource heartbeatSource;
        private AudioSource sfxSource;
        private AudioSource stingerSource;
        private AudioSource clockSource;
        private AudioSource footstepSource;
        private AudioSource uiSource;

        private AudioClip activeDroneClip;
        private AudioClip activeHeartbeatClip;
        private AudioClip activeDoorClip;
        private AudioClip activeKeyClip;
        private AudioClip activeNoteClip;
        private AudioClip activeStingerClip;
        private AudioClip activeClockClip;
        private AudioClip activeChimeClip;
        private AudioClip activeFootstepCarpetClip;
        private AudioClip activeFootstepWoodClip;
        private AudioClip activeFootstepTileClip;
        private AudioClip activeInspectOpenClip;
        private AudioClip activeInspectCloseClip;
        private AudioClip activeUiConfirmClip;
        private AudioClip activeAdrianBlipClip;
        private AudioClip activeUnknownBlipClip;
        private AudioClip activeSubtleCreakClip;
        private AudioClip activeRoomShiftSwellClip;
        private AudioClip activeEndingResonanceClip;

        private float targetHeartbeatVolume = 0f;
        private float currentHeartbeatVolume = 0f;
        private float currentAmbientDuck = 1f;
        private float targetAmbientDuck = 1f;
        private float ambientDuckSpeed = 2.5f;
        private float lastFootstepTime = -10f;
        private float lastDialogueBlipTime = -10f;
        private bool ambientPlaybackRequested = false;

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                if (ambientSource != null) ambientSource.volume = ambientVolume * currentAmbientDuck * masterVolume;
                if (sfxSource != null) sfxSource.volume = sfxVolume * masterVolume;
                if (footstepSource != null) footstepSource.volume = sfxVolume * masterVolume;
                if (uiSource != null) uiSource.volume = sfxVolume * masterVolume;
                if (stingerSource != null) stingerSource.volume = 0.9f * masterVolume;
            }
        }

        public float AmbientVolume
        {
            get => ambientVolume;
            set
            {
                ambientVolume = Mathf.Clamp01(value);
                if (ambientSource != null) ambientSource.volume = ambientVolume * currentAmbientDuck * masterVolume;
            }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                if (sfxSource != null) sfxSource.volume = sfxVolume * masterVolume;
                if (footstepSource != null) footstepSource.volume = sfxVolume * masterVolume;
                if (uiSource != null) uiSource.volume = sfxVolume * masterVolume;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioListener();
            SetupAudioSources();
            PrepareClips();
        }

        private void Start()
        {
            EnsureAudioListener();
            StartAmbientPlayback();
        }

        private void Update()
        {
            // Retry ambient start automatically once async AudioClip data finishes loading
            if (ambientPlaybackRequested && ambientSource != null && activeDroneClip != null &&
                !ambientSource.isPlaying && activeDroneClip.loadState == AudioDataLoadState.Loaded)
            {
                ambientSource.Play();
            }

            // Smoothly modulate ambient ducking (SMS alerts / Ending fade) without altering user volume setting
            if (ambientSource != null)
            {
                if (!Mathf.Approximately(currentAmbientDuck, targetAmbientDuck))
                {
                    currentAmbientDuck = Mathf.MoveTowards(currentAmbientDuck, targetAmbientDuck, Time.unscaledDeltaTime * ambientDuckSpeed);
                }
                ambientSource.volume = ambientVolume * currentAmbientDuck * masterVolume;
            }

            // Smoothly modulate heartbeat volume and pitch based on tension
            if (heartbeatSource != null)
            {
                currentHeartbeatVolume = Mathf.MoveTowards(currentHeartbeatVolume, targetHeartbeatVolume, Time.deltaTime * 0.8f);
                heartbeatSource.volume = currentHeartbeatVolume * masterVolume;
                if (currentHeartbeatVolume > 0.01f && !heartbeatSource.isPlaying)
                {
                    heartbeatSource.Play();
                }
                else if (currentHeartbeatVolume <= 0.001f && heartbeatSource.isPlaying)
                {
                    heartbeatSource.Stop();
                }
            }
        }

        private void EnsureAudioListener()
        {
            AudioListener.pause = false;
            var existing = FindFirstObjectByType<AudioListener>();
            if (existing != null)
            {
                existing.enabled = true;
                return;
            }

            Camera targetCam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            GameObject host = targetCam != null ? targetCam.gameObject : gameObject;
            if (host.GetComponent<AudioListener>() == null)
            {
                host.AddComponent<AudioListener>();
            }
        }

        private AudioSource Create2DAudioSource(bool loop, float volume, int priority = 128)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = loop;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.priority = priority;
            src.volume = volume;
            return src;
        }

        private void SetupAudioSources()
        {
            ambientSource = Create2DAudioSource(true, ambientVolume * masterVolume, 32);
            heartbeatSource = Create2DAudioSource(true, 0f, 64);
            sfxSource = Create2DAudioSource(false, sfxVolume * masterVolume, 96);
            stingerSource = Create2DAudioSource(false, 0.9f * masterVolume, 64);
            clockSource = Create2DAudioSource(false, 0.5f * masterVolume, 128);
            footstepSource = Create2DAudioSource(false, sfxVolume * masterVolume, 110);
            uiSource = Create2DAudioSource(false, sfxVolume * masterVolume, 90);
        }

        private void PrepareClips()
        {
            if (customAmbientDrone == null)
            {
                customAmbientDrone = LoadDefaultAmbientClip();
            }

            activeDroneClip = customAmbientDrone != null ? customAmbientDrone : GenerateSubBassDrone();
            activeHeartbeatClip = customHeartbeat != null ? customHeartbeat : GenerateHeartbeat();
            activeDoorClip = customDoorTransition != null ? customDoorTransition : GenerateDoorCreak();
            activeKeyClip = customKeyPickup != null ? customKeyPickup : GenerateKeyClink();
            activeNoteClip = customNotePickup != null ? customNotePickup : GeneratePaperRustle();
            activeStingerClip = customHorrorStinger != null ? customHorrorStinger : GenerateHorrorStinger();
            activeClockClip = customClockTick != null ? customClockTick : GenerateClockTick();
            activeChimeClip = customObjectiveChime != null ? customObjectiveChime : GenerateObjectiveChime();

            activeFootstepCarpetClip = customFootstepCarpet != null ? customFootstepCarpet : GenerateFootstepCarpet();
            activeFootstepWoodClip = customFootstepWood != null ? customFootstepWood : GenerateFootstepWood();
            activeFootstepTileClip = customFootstepTile != null ? customFootstepTile : GenerateFootstepTile();
            activeInspectOpenClip = GenerateInspectTransition(true);
            activeInspectCloseClip = GenerateInspectTransition(false);
            activeUiConfirmClip = GenerateUiConfirm();
            activeAdrianBlipClip = GenerateDialogueBlip(false);
            activeUnknownBlipClip = GenerateDialogueBlip(true);
            activeSubtleCreakClip = GenerateSubtleCreak();
            activeRoomShiftSwellClip = GenerateRoomShiftSwell();
            activeEndingResonanceClip = GenerateEndingResonance();

            if (activeDroneClip != null && activeDroneClip.loadState == AudioDataLoadState.Unloaded)
            {
                activeDroneClip.LoadAudioData();
            }

            if (ambientSource != null) ambientSource.clip = activeDroneClip;
            if (heartbeatSource != null) heartbeatSource.clip = activeHeartbeatClip;
        }

        private AudioClip LoadDefaultAmbientClip()
        {
#if UNITY_EDITOR
            try
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/Ingame Noise.wav");
                if (clip != null) return clip;
            }
            catch { }
#endif
            try
            {
                var res = Resources.Load<AudioClip>("SFX/Ingame Noise");
                if (res != null) return res;
            }
            catch { }

            return null;
        }

        public void SetAmbientBacksound(AudioClip clip)
        {
            if (clip == null) return;
            customAmbientDrone = clip;
            activeDroneClip = clip;
            if (activeDroneClip.loadState == AudioDataLoadState.Unloaded)
            {
                activeDroneClip.LoadAudioData();
            }
            if (ambientSource != null)
            {
                bool wasPlaying = ambientSource.isPlaying;
                ambientSource.clip = activeDroneClip;
                ambientSource.loop = true;
                ambientSource.spatialBlend = 0f;
                ambientSource.volume = ambientVolume * masterVolume;
                if (wasPlaying || ambientPlaybackRequested)
                {
                    ambientSource.Play();
                }
            }
        }

        private void StartAmbientPlayback()
        {
            if (ambientSource != null && activeDroneClip != null)
            {
                ambientPlaybackRequested = true;
                if (activeDroneClip.loadState == AudioDataLoadState.Unloaded)
                {
                    activeDroneClip.LoadAudioData();
                }
                ambientSource.clip = activeDroneClip;
                ambientSource.loop = true;
                ambientSource.spatialBlend = 0f;
                ambientSource.volume = ambientVolume * masterVolume;
                ambientSource.Play();
                Debug.Log($"[AudioManager] Ambient backsound: '{activeDroneClip.name}' len={activeDroneClip.length:F1}s state={activeDroneClip.loadState} vol={ambientSource.volume:F2} listener={(FindFirstObjectByType<AudioListener>() != null)}");
            }
        }

        public void PlayDoorTransition()
        {
            if (sfxSource != null && activeDoorClip != null)
            {
                sfxSource.PlayOneShot(activeDoorClip, 0.75f * sfxVolume);
            }
        }

        public void PlayKeyPickup()
        {
            if (sfxSource != null && activeKeyClip != null)
            {
                sfxSource.PlayOneShot(activeKeyClip, 0.9f * sfxVolume);
            }
        }

        public void PlayNotePickup()
        {
            if (sfxSource != null && activeNoteClip != null)
            {
                sfxSource.PlayOneShot(activeNoteClip, 0.8f * sfxVolume);
            }
        }

        public void PlayHorrorStinger()
        {
            if (stingerSource != null && activeStingerClip != null)
            {
                stingerSource.PlayOneShot(activeStingerClip, 1.0f);
            }
        }

        public void PlayClockTick()
        {
            if (clockSource != null && activeClockClip != null)
            {
                clockSource.PlayOneShot(activeClockClip, 0.6f * sfxVolume);
            }
        }

        public void PlayObjectiveChime()
        {
            if (sfxSource != null && activeChimeClip != null)
            {
                sfxSource.PlayOneShot(activeChimeClip, 0.85f * sfxVolume);
            }
        }

        private AudioClip phoneVibrateClip;

        public void PlayPhoneVibration()
        {
            if (phoneVibrateClip == null)
            {
                phoneVibrateClip = GeneratePhoneVibrate();
            }

            if (sfxSource != null && phoneVibrateClip != null)
            {
                sfxSource.PlayOneShot(phoneVibrateClip, 0.9f * sfxVolume);
            }
            else
            {
                PlayObjectiveChime();
            }
        }

        public void SetAmbientDuck(float multiplier, float fadeDuration = 0.25f)
        {
            targetAmbientDuck = Mathf.Clamp01(multiplier);
            ambientDuckSpeed = fadeDuration > 0.01f
                ? Mathf.Max(0.2f, Mathf.Abs(targetAmbientDuck - currentAmbientDuck) / fadeDuration)
                : 100f;
        }

        public void PlayFootstep(FootstepSurface surface)
        {
            if (footstepSource == null) return;
            if (footstepSource.isPlaying || Time.unscaledTime - lastFootstepTime < 0.18f) return;

            AudioClip clip = surface switch
            {
                FootstepSurface.Wood => activeFootstepWoodClip,
                FootstepSurface.Tile => activeFootstepTileClip,
                _ => activeFootstepCarpetClip
            };
            if (clip == null) return;

            lastFootstepTime = Time.unscaledTime;
            footstepSource.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
            float baseVol = surface == FootstepSurface.Carpet ? 0.24f : (surface == FootstepSurface.Tile ? 0.22f : 0.26f);
            footstepSource.PlayOneShot(clip, baseVol);
        }

        public void PlayInspectOpen()
        {
            if (uiSource != null && activeInspectOpenClip != null)
            {
                uiSource.pitch = 1f;
                uiSource.PlayOneShot(activeInspectOpenClip, 0.42f);
            }
        }

        public void PlayInspectClose()
        {
            if (uiSource != null && activeInspectCloseClip != null)
            {
                uiSource.pitch = 1f;
                uiSource.PlayOneShot(activeInspectCloseClip, 0.35f);
            }
        }

        public void PlayUiConfirm()
        {
            if (uiSource != null && activeUiConfirmClip != null)
            {
                uiSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                uiSource.PlayOneShot(activeUiConfirmClip, 0.28f);
            }
        }

        public void PlayDialogueBlip(bool isUnknown)
        {
            if (uiSource == null) return;
            if (Time.unscaledTime - lastDialogueBlipTime < 0.055f) return;

            AudioClip clip = isUnknown ? activeUnknownBlipClip : activeAdrianBlipClip;
            if (clip == null) return;

            lastDialogueBlipTime = Time.unscaledTime;
            uiSource.pitch = isUnknown
                ? UnityEngine.Random.Range(0.92f, 1.02f)
                : UnityEngine.Random.Range(0.95f, 1.05f);
            uiSource.PlayOneShot(clip, isUnknown ? 0.16f : 0.13f);
        }

        public void PlaySubtleCreak()
        {
            if (sfxSource != null && activeSubtleCreakClip != null)
            {
                sfxSource.PlayOneShot(activeSubtleCreakClip, 0.32f);
            }
        }

        public void PlayRoomShiftSwell()
        {
            if (stingerSource != null && activeRoomShiftSwellClip != null)
            {
                stingerSource.PlayOneShot(activeRoomShiftSwellClip, 0.55f);
            }
        }

        public void PlayEndingResonance()
        {
            if (stingerSource != null && activeEndingResonanceClip != null)
            {
                stingerSource.PlayOneShot(activeEndingResonanceClip, 0.72f);
            }
        }

        private Coroutine alarmBeepCoroutine;

        public void StartAlarmBuzzing()
        {
            if (alarmBeepCoroutine != null) StopCoroutine(alarmBeepCoroutine);
            alarmBeepCoroutine = StartCoroutine(AlarmBuzzingLoop());
        }

        public void StopAlarmBuzzing()
        {
            if (alarmBeepCoroutine != null)
            {
                StopCoroutine(alarmBeepCoroutine);
                alarmBeepCoroutine = null;
            }
        }

        private IEnumerator AlarmBuzzingLoop()
        {
            while (true)
            {
                PlayClockTick();
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>
        /// Sets heartbeat intensity from 0.0 (calm/silent) to 1.0 (intense panic).
        /// </summary>
        public void SetHeartbeatIntensity(float intensity)
        {
            targetHeartbeatVolume = Mathf.Clamp01(intensity) * 0.75f;
            if (heartbeatSource != null)
            {
                // Increase heartbeat speed/pitch as tension rises
                heartbeatSource.pitch = Mathf.Lerp(0.85f, 1.4f, Mathf.Clamp01(intensity));
            }
        }

        // ====================================================================
        // PROCEDURAL HORROR AUDIO WAVEFORM SYNTHESIZERS
        // ====================================================================

        private AudioClip GenerateSubBassDrone()
        {
            int sampleRate = 44100;
            float duration = 4.0f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Complex low-frequency drone (55Hz sub, 110Hz harmonic, 58Hz slow beat)
                float s1 = Mathf.Sin(2f * Mathf.PI * 55f * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.35f;
                float beat = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.25f;
                // Subtle binaural rumble modulation
                float mod = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                samples[i] = (s1 + s2 + beat) * 0.45f * mod;
            }

            var clip = AudioClip.Create("Procedural_HorrorDrone", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateHeartbeat()
        {
            int sampleRate = 44100;
            float duration = 1.0f; // 1 beat per second
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Lub (first thump at t=0)
            int lubLen = Mathf.FloorToInt(sampleRate * 0.16f);
            for (int i = 0; i < lubLen; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin((float)i / lubLen * Mathf.PI);
                float wave = Mathf.Sin(2f * Mathf.PI * (50f - 20f * (t / 0.16f)) * t);
                samples[i] += wave * env * 0.85f;
            }

            // Dub (second thump at t=0.25s)
            int dubStart = Mathf.FloorToInt(sampleRate * 0.25f);
            int dubLen = Mathf.FloorToInt(sampleRate * 0.14f);
            for (int i = 0; i < dubLen && (dubStart + i) < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin((float)i / dubLen * Mathf.PI);
                float wave = Mathf.Sin(2f * Mathf.PI * (58f - 24f * (t / 0.14f)) * t);
                samples[dubStart + i] += wave * env * 0.70f;
            }

            var clip = AudioClip.Create("Procedural_Heartbeat", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateDoorCreak()
        {
            int sampleRate = 44100;
            float duration = 0.55f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                // Frequency sweep creak (220Hz up to 340Hz and down)
                float freq = 200f + 140f * Mathf.Sin(t * 12f);
                float creak = Mathf.Sin(2f * Mathf.PI * freq * t) * (Mathf.Sin(t * 180f) > 0f ? 1f : -0.6f);
                samples[i] = creak * env * 0.35f;
            }

            var clip = AudioClip.Create("Procedural_DoorCreak", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateKeyClink()
        {
            int sampleRate = 44100;
            float duration = 0.4f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 14f);
                // Bright metallic chime harmonics (2600Hz + 3900Hz)
                float tone1 = Mathf.Sin(2f * Mathf.PI * 2600f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 3920f * t) * 0.6f;
                float tone3 = Mathf.Sin(2f * Mathf.PI * 5200f * t) * 0.25f;
                samples[i] = (tone1 + tone2 + tone3) * env * 0.4f;
            }

            var clip = AudioClip.Create("Procedural_KeyClink", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GeneratePaperRustle()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];
            var rand = new System.Random(42);

            float last = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI) * (1f - (t / duration) * 0.5f);
                // Filtered noise
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                last = (last + (white * 0.25f)) * 0.85f;
                samples[i] = last * env * 0.5f;
            }

            var clip = AudioClip.Create("Procedural_PaperRustle", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateHorrorStinger()
        {
            int sampleRate = 44100;
            float duration = 1.6f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 2.8f);
                // Dissonant microtonal cluster (tritone & sharp minor seconds)
                float f1 = Mathf.Sin(2f * Mathf.PI * 185f * t);
                float f2 = Mathf.Sin(2f * Mathf.PI * 196f * t);
                float f3 = Mathf.Sin(2f * Mathf.PI * 261f * t) * 0.8f;
                float f4 = Mathf.Sin(2f * Mathf.PI * 370f * t) * 0.6f;
                samples[i] = (f1 + f2 + f3 + f4) * 0.25f * env;
            }

            var clip = AudioClip.Create("Procedural_HorrorStinger", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateClockTick()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 60f);
                float click = Mathf.Sin(2f * Mathf.PI * 1600f * t);
                samples[i] = click * env * 0.6f;
            }

            var clip = AudioClip.Create("Procedural_ClockTick", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateObjectiveChime()
        {
            int sampleRate = 44100;
            float duration = 1.1f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 3.5f);
                // Warm chord chime (Major triad: C5 523Hz + G5 784Hz + C6 1046Hz)
                float c1 = Mathf.Sin(2f * Mathf.PI * 523.25f * t);
                float c2 = Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.6f;
                float c3 = Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.35f;
                samples[i] = (c1 + c2 + c3) * 0.3f * env;
            }

            var clip = AudioClip.Create("Procedural_ObjectiveChime", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GeneratePhoneVibrate()
        {
            int sampleRate = 44100;
            float duration = 0.55f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float pulse = 0f;
                // Dual motor buzz: Pulse 1 (0.0s - 0.2s), Pause (0.2s - 0.28s), Pulse 2 (0.28s - 0.52s)
                if (t < 0.2f)
                {
                    float pEnv = Mathf.Sin(t / 0.2f * Mathf.PI);
                    pulse = (Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.3f) * pEnv;
                }
                else if (t >= 0.28f && t <= 0.52f)
                {
                    float pEnv = Mathf.Sin((t - 0.28f) / 0.24f * Mathf.PI);
                    pulse = (Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 280f * t) * 0.3f) * pEnv;
                }

                // Add faint electronic chime overtone at the beginning
                float chime = (t < 0.3f) ? Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-t * 12f) * 0.25f : 0f;

                samples[i] = (pulse * 0.65f) + chime;
            }

            var clip = AudioClip.Create("Procedural_PhoneVibrate", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeMonoClip(string clipName, float duration, Func<float, float> sampleAtTime)
        {
            const int sampleRate = 44100;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                samples[i] = sampleAtTime((float)i / sampleRate);
            }

            var clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateFootstepCarpet()
        {
            var rand = new System.Random(101);
            float lastNoise = 0f;
            return SynthesizeMonoClip("Procedural_Footstep_Carpet", 0.11f, t =>
            {
                float env = Mathf.Exp(-t * 36f) * Mathf.Sin(Mathf.Min(1f, t / 0.015f) * Mathf.PI * 0.5f);
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 45f, t / 0.11f) * t);
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lastNoise = lastNoise * 0.88f + white * 0.12f;
                return (body * 0.55f + lastNoise * 0.45f) * env * 0.6f;
            });
        }

        private AudioClip GenerateFootstepWood()
        {
            var rand = new System.Random(202);
            float lastNoise = 0f;
            return SynthesizeMonoClip("Procedural_Footstep_Wood", 0.10f, t =>
            {
                float env = Mathf.Exp(-t * 42f);
                float wood1 = Mathf.Sin(2f * Mathf.PI * 165f * t);
                float wood2 = Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.45f;
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lastNoise = lastNoise * 0.78f + white * 0.22f;
                return (wood1 * 0.5f + wood2 * 0.3f + lastNoise * 0.25f) * env * 0.55f;
            });
        }

        private AudioClip GenerateFootstepTile()
        {
            var rand = new System.Random(303);
            return SynthesizeMonoClip("Procedural_Footstep_Tile", 0.075f, t =>
            {
                float env = Mathf.Exp(-t * 62f);
                float tap = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 890f * t) * 0.3f;
                float click = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.2f;
                return (tap + click) * env * 0.48f;
            });
        }

        private AudioClip GenerateInspectTransition(bool isOpen)
        {
            var rand = new System.Random(isOpen ? 404 : 405);
            float last = 0f;
            float dur = isOpen ? 0.18f : 0.14f;
            return SynthesizeMonoClip(isOpen ? "Procedural_InspectOpen" : "Procedural_InspectClose", dur, t =>
            {
                float p = t / dur;
                float env = Mathf.Sin(p * Mathf.PI) * (isOpen ? (1f - p * 0.3f) : (1f - p * 0.5f));
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                last = last * 0.84f + white * 0.16f;
                float toneFreq = isOpen ? Mathf.Lerp(180f, 310f, p) : Mathf.Lerp(260f, 150f, p);
                float softTone = Mathf.Sin(2f * Mathf.PI * toneFreq * t) * 0.18f;
                return (last * 0.45f + softTone) * env * 0.55f;
            });
        }

        private AudioClip GenerateUiConfirm()
        {
            return SynthesizeMonoClip("Procedural_UiConfirm", 0.07f, t =>
            {
                float env = Mathf.Exp(-t * 55f);
                float f1 = Mathf.Sin(2f * Mathf.PI * 420f * t);
                float f2 = Mathf.Sin(2f * Mathf.PI * 630f * t) * 0.35f;
                return (f1 + f2) * env * 0.35f;
            });
        }

        private AudioClip GenerateDialogueBlip(bool isUnknown)
        {
            return SynthesizeMonoClip(isUnknown ? "Procedural_Blip_Unknown" : "Procedural_Blip_Adrian", 0.055f, t =>
            {
                float env = Mathf.Exp(-t * 68f);
                if (isUnknown)
                {
                    float s1 = Mathf.Sin(2f * Mathf.PI * 680f * t);
                    float s2 = Mathf.Sin(2f * Mathf.PI * 1020f * t) * 0.4f;
                    return (s1 + s2) * env * 0.25f;
                }
                float warm = Mathf.Sin(2f * Mathf.PI * 240f * t);
                return warm * env * 0.28f;
            });
        }

        private AudioClip GenerateSubtleCreak()
        {
            return SynthesizeMonoClip("Procedural_SubtleCreak", 0.9f, t =>
            {
                float p = t / 0.9f;
                float env = Mathf.Sin(p * Mathf.PI) * Mathf.Exp(-t * 1.4f);
                float freq = 145f + 45f * Mathf.Sin(t * 9.5f);
                float osc = Mathf.Sin(2f * Mathf.PI * freq * t) * (Mathf.Sin(t * 75f) > 0f ? 0.85f : -0.45f);
                return osc * env * 0.28f;
            });
        }

        private AudioClip GenerateRoomShiftSwell()
        {
            return SynthesizeMonoClip("Procedural_RoomShiftSwell", 1.25f, t =>
            {
                float p = t / 1.25f;
                float env = Mathf.Sin(p * Mathf.PI) * Mathf.Exp(-t * 1.1f);
                float freq = Mathf.Lerp(78f, 44f, p);
                float sub = Mathf.Sin(2f * Mathf.PI * freq * t);
                float ghost = Mathf.Sin(2f * Mathf.PI * (freq * 1.52f) * t) * 0.35f;
                return (sub + ghost) * env * 0.4f;
            });
        }

        private AudioClip GenerateEndingResonance()
        {
            return SynthesizeMonoClip("Procedural_EndingResonance", 3.2f, t =>
            {
                float attack = Mathf.Clamp01(t / 0.35f);
                float decay = Mathf.Exp(-t * 0.85f);
                // Restrained A minor chord (A3 220Hz, C4 261.6Hz, E4 329.6Hz, A4 440Hz)
                float c1 = Mathf.Sin(2f * Mathf.PI * 220.0f * t);
                float c2 = Mathf.Sin(2f * Mathf.PI * 261.63f * t) * 0.7f;
                float c3 = Mathf.Sin(2f * Mathf.PI * 329.63f * t) * 0.5f;
                float c4 = Mathf.Sin(2f * Mathf.PI * 440.0f * t) * 0.25f;
                return (c1 + c2 + c3 + c4) * 0.22f * attack * decay;
            });
        }
    }
}
