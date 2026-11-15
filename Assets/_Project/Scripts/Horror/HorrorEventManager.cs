using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Room0317.Apartment;
using Room0317.Audio;
using Room0317.Dialogue;
using Room0317.UI;

namespace Room0317.Horror
{
    /// <summary>
    /// Master coordinator for scripted horror events and psychological tension (Step 10).
    /// Manages electrical light flickering when room states shift, dynamic heartbeat modulation
    /// based on room eeriness and clue proximity, and the climactic 03:17 AM event.
    /// </summary>
    public class HorrorEventManager : MonoBehaviour
    {
        public static HorrorEventManager Instance { get; private set; }

        private struct LightDefaults
        {
            public float intensity;
            public Color color;
        }

        private DialogueBox dialogueBox;
        private ScreenFader screenFader;
        private Coroutine activeFlickerCoroutine;
        private readonly Dictionary<Light2D, LightDefaults> lightDefaults = new Dictionary<Light2D, LightDefaults>();
        private readonly Dictionary<ApartmentRoom, List<Light2D>> roomLightsCache = new Dictionary<ApartmentRoom, List<Light2D>>();
        private float nextAmbientCreakTime = 18f;
        private ParticleSystem ambientDustPs;

        public static event Action OnHorrorClimaxTriggered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();
            screenFader = FindFirstObjectByType<ScreenFader>();

            CacheSceneLights();
            SetupAmbientDust();
            nextAmbientCreakTime = Time.time + UnityEngine.Random.Range(18f, 32f);

            ChangingRoomManager.OnRoomShifted += HandleRoomShifted;
            ApartmentManager.OnCurrentRoomChanged += HandleCurrentRoomChanged;
        }

        private void OnDestroy()
        {
            ChangingRoomManager.OnRoomShifted -= HandleRoomShifted;
            ApartmentManager.OnCurrentRoomChanged -= HandleCurrentRoomChanged;
        }

        private void Update()
        {
            if (Time.time < nextAmbientCreakTime) return;
            nextAmbientCreakTime = Time.time + UnityEngine.Random.Range(26f, 45f);

            if (InteractionBlocker.IsBlocked(dialogueBox)) return;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySubtleCreak();
            }

            // Subtle faint lamp flutter only in Eerie State B rooms
            var curRoom = ApartmentManager.Instance != null ? ApartmentManager.Instance.CurrentRoom : null;
            if (curRoom != null && curRoom.CurrentState == RoomState.B && activeFlickerCoroutine == null)
            {
                activeFlickerCoroutine = StartCoroutine(BriefMicroDipRoutine(curRoom));
            }
        }

        private void CacheSceneLights()
        {
            lightDefaults.Clear();
            roomLightsCache.Clear();

            var allLights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l == null || l.lightType == Light2D.LightType.Global) continue;
                lightDefaults[l] = new LightDefaults { intensity = l.intensity, color = l.color };
            }
        }

        private List<Light2D> ResolveRoomLights(ApartmentRoom room)
        {
            if (room == null) return new List<Light2D>();
            if (roomLightsCache.TryGetValue(room, out var cached)) return cached;

            var matched = new List<Light2D>();
            var childLights = room.GetComponentsInChildren<Light2D>(true);
            foreach (var l in childLights)
            {
                if (l != null && l.lightType != Light2D.LightType.Global && !matched.Contains(l))
                {
                    if (!lightDefaults.ContainsKey(l))
                        lightDefaults[l] = new LightDefaults { intensity = l.intensity, color = l.color };
                    matched.Add(l);
                }
            }

            // Also match scene-level lights stored under --- LIGHTS --- by name or room bounds
            string expectedName = "Light_" + room.RoomId;
            foreach (var kvp in lightDefaults)
            {
                var l = kvp.Key;
                if (l == null || matched.Contains(l)) continue;
                bool nameMatch = string.Equals(l.gameObject.name, expectedName, StringComparison.OrdinalIgnoreCase);
                bool distMatch = Vector2.Distance(l.transform.position, room.transform.position) <= 9.0f;
                if (nameMatch || distMatch)
                {
                    matched.Add(l);
                }
            }

            roomLightsCache[room] = matched;
            return matched;
        }

        private void HandleRoomShifted(ApartmentRoom room, RoomState newState)
        {
            if (newState == RoomState.B)
            {
                TriggerRoomShiftSpook(room);
            }
            else if (room != null)
            {
                ApplyRoomLightingTone(room, RoomState.A);
            }
        }

        private void HandleCurrentRoomChanged(ApartmentRoom room)
        {
            if (room == null) return;

            // Adjust heartbeat based on whether the active room is Normal (State A) or Eerie (State B)
            if (AudioManager.Instance != null)
            {
                float tension = room.CurrentState == RoomState.B ? 0.65f : 0.0f;
                AudioManager.Instance.SetHeartbeatIntensity(tension);
            }

            ApplyRoomLightingTone(room, room.CurrentState);
        }

        public void TriggerRoomShiftSpook(ApartmentRoom room)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayRoomShiftSwell();
                AudioManager.Instance.PlayHorrorStinger();
            }

            if (ApartmentManager.Instance != null)
            {
                ApartmentManager.Instance.TriggerCameraMicroShake(0.035f, 0.28f);
            }

            if (room != null)
            {
                if (activeFlickerCoroutine != null) StopCoroutine(activeFlickerCoroutine);
                activeFlickerCoroutine = StartCoroutine(FlickerLightsRoutine(room, 1.35f));
            }
        }

        public void DimActiveRoomForEnding(float duration = 2.2f)
        {
            var curRoom = ApartmentManager.Instance != null ? ApartmentManager.Instance.CurrentRoom : null;
            if (curRoom != null)
            {
                StartCoroutine(DimLightsRoutine(curRoom, duration));
            }
        }

        private void ApplyRoomLightingTone(ApartmentRoom room, RoomState state)
        {
            var lights = ResolveRoomLights(room);
            foreach (var l in lights)
            {
                if (l == null || !lightDefaults.TryGetValue(l, out var def)) continue;
                if (state == RoomState.B)
                {
                    l.intensity = def.intensity * 0.78f;
                    l.color = Color.Lerp(def.color, new Color(0.68f, 0.78f, 0.92f), 0.34f);
                }
                else
                {
                    l.intensity = def.intensity;
                    l.color = def.color;
                }
            }
        }

        private IEnumerator FlickerLightsRoutine(ApartmentRoom room, float duration)
        {
            var lights = ResolveRoomLights(room);
            if (lights.Count == 0)
            {
                activeFlickerCoroutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Restrained eerie brownout dip without rapid harsh strobing
                float flicker = UnityEngine.Random.value > 0.3f ? UnityEngine.Random.Range(0.28f, 0.65f) : 0.14f;
                for (int i = 0; i < lights.Count; i++)
                {
                    var l = lights[i];
                    if (l != null && lightDefaults.TryGetValue(l, out var def))
                    {
                        l.intensity = def.intensity * flicker;
                    }
                }

                float wait = UnityEngine.Random.Range(0.07f, 0.16f);
                elapsed += wait;
                yield return new WaitForSeconds(wait);
            }

            ApplyRoomLightingTone(room, room.CurrentState);
            activeFlickerCoroutine = null;
        }

        private IEnumerator BriefMicroDipRoutine(ApartmentRoom room)
        {
            var lights = ResolveRoomLights(room);
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i] != null && lightDefaults.TryGetValue(lights[i], out var def))
                {
                    lights[i].intensity = def.intensity * 0.56f;
                }
            }

            yield return new WaitForSeconds(0.18f);

            ApplyRoomLightingTone(room, room.CurrentState);
            activeFlickerCoroutine = null;
        }

        private IEnumerator DimLightsRoutine(ApartmentRoom room, float duration)
        {
            var lights = ResolveRoomLights(room);
            var startIntensities = new float[lights.Count];
            for (int i = 0; i < lights.Count; i++)
            {
                startIntensities[i] = lights[i] != null ? lights[i].intensity : 0f;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < lights.Count; i++)
                {
                    if (lights[i] != null)
                    {
                        lights[i].intensity = Mathf.Lerp(startIntensities[i], startIntensities[i] * 0.25f, t);
                    }
                }
                yield return null;
            }
        }

        private void SetupAmbientDust()
        {
            if (ambientDustPs != null) return;
            Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam == null) return;

            var dustGo = new GameObject("AmbientDustMotes");
            dustGo.transform.SetParent(cam.transform, false);
            dustGo.transform.localPosition = new Vector3(0f, 0f, 5f);

            ambientDustPs = dustGo.AddComponent<ParticleSystem>();
            var main = ambientDustPs.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = 22;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.95f, 0.92f, 0.84f, 0.05f),
                new Color(0.85f, 0.88f, 0.95f, 0.11f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;

            var emission = ambientDustPs.emission;
            emission.rateOverTime = 2.8f;

            var shape = ambientDustPs.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 8f, 0.1f);

            var colOverLife = ambientDustPs.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var rend = dustGo.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerName = "Props_Front";
                rend.sortingOrder = 180;
                Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    mat.mainTexture = CreateSoftDustTexture();
                    rend.sharedMaterial = mat;
                }
            }
        }

        private static Texture2D CreateSoftDustTexture()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / maxR);
                    float a = Mathf.Pow(1f - d, 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private IEnumerator ClimaxSequenceRoutine()
        {
            OnHorrorClimaxTriggered?.Invoke();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayHorrorStinger();
                AudioManager.Instance.SetHeartbeatIntensity(1.0f);
            }

            // Strobe all lights across the entire apartment
            var allLights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            var original = new float[allLights.Length];
            for (int i = 0; i < allLights.Length; i++) original[i] = allLights[i].intensity;

            for (int f = 0; f < 8; f++)
            {
                for (int i = 0; i < allLights.Length; i++)
                {
                    allLights[i].intensity = (f % 2 == 0) ? 0.05f : original[i];
                }
                yield return new WaitForSeconds(0.08f);
            }

            for (int i = 0; i < allLights.Length; i++) allLights[i].intensity = original[i];

            // Unlock front door
            var doors = FindObjectsByType<DoorTransition>(FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                if (d.DestinationLabel.Contains("Exit"))
                {
                    d.SetLocked(false);
                }
            }
        }
    }
}
