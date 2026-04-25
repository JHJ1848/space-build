using UnityEngine;

[DisallowMultipleComponent]
public class StylizedDayNightCycle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Material skyboxMaterial;

    [Header("Timing")]
    [SerializeField] private float cycleDurationSeconds = 180f;
    [SerializeField] private float sunYaw = -40f;

    [Header("Lighting")]
    [SerializeField] private Color dayLightColor = new Color(1f, 0.95f, 0.88f, 1f);
    [SerializeField] private Color sunsetLightColor = new Color(1f, 0.63f, 0.42f, 1f);
    [SerializeField] private Color nightLightColor = new Color(0.32f, 0.42f, 0.62f, 1f);
    [SerializeField] private float dayIntensity = 1.2f;
    [SerializeField] private float nightIntensity = 0.18f;

    [Header("Atmosphere")]
    [SerializeField] private Color dayAmbientColor = new Color(0.59f, 0.66f, 0.78f, 1f);
    [SerializeField] private Color sunsetAmbientColor = new Color(0.78f, 0.56f, 0.52f, 1f);
    [SerializeField] private Color nightAmbientColor = new Color(0.11f, 0.16f, 0.28f, 1f);
    [SerializeField] private Color dayFogColor = new Color(0.71f, 0.83f, 0.97f, 1f);
    [SerializeField] private Color sunsetFogColor = new Color(0.98f, 0.66f, 0.64f, 1f);
    [SerializeField] private Color nightFogColor = new Color(0.07f, 0.10f, 0.18f, 1f);
    [SerializeField] private float dayFogDensity = 0.0022f;
    [SerializeField] private float nightFogDensity = 0.0045f;

    [Header("Sky")]
    [SerializeField] private Color daySkyTint = new Color(0.37f, 0.62f, 1f, 1f);
    [SerializeField] private Color sunsetSkyTint = new Color(1f, 0.52f, 0.58f, 1f);
    [SerializeField] private Color nightSkyTint = new Color(0.06f, 0.10f, 0.23f, 1f);
    [SerializeField] private Color dayGroundColor = new Color(0.96f, 0.73f, 0.79f, 1f);
    [SerializeField] private Color sunsetGroundColor = new Color(1f, 0.56f, 0.46f, 1f);
    [SerializeField] private Color nightGroundColor = new Color(0.02f, 0.02f, 0.05f, 1f);
    [SerializeField] private float dayExposure = 1.25f;
    [SerializeField] private float nightExposure = 0.5f;

    private Material _runtimeSkybox;
    private Material _previousSkybox;

    private void OnEnable()
    {
        if (skyboxMaterial != null)
        {
            _previousSkybox = RenderSettings.skybox;
            _runtimeSkybox = Instantiate(skyboxMaterial);
            _runtimeSkybox.name = skyboxMaterial.name + "_Runtime";
            RenderSettings.skybox = _runtimeSkybox;
            DynamicGI.UpdateEnvironment();
        }

        if (sunLight != null)
        {
            RenderSettings.sun = sunLight;
        }

        ApplyCycle(0f);
    }

    private void OnDisable()
    {
        if (_runtimeSkybox != null && RenderSettings.skybox == _runtimeSkybox)
        {
            RenderSettings.skybox = _previousSkybox;
        }

        if (_runtimeSkybox != null)
        {
            Destroy(_runtimeSkybox);
            _runtimeSkybox = null;
        }
    }

    private void Update()
    {
        if (cycleDurationSeconds <= 0.01f)
        {
            ApplyCycle(0f);
            return;
        }

        float time01 = Mathf.Repeat(Time.time / cycleDurationSeconds, 1f);
        ApplyCycle(time01);
    }

    private void ApplyCycle(float time01)
    {
        float angle = time01 * Mathf.PI * 2f;
        float sunHeight = Mathf.Sin(angle - Mathf.PI * 0.5f);
        float daylight = Mathf.Clamp01(sunHeight * 0.5f + 0.5f);
        float twilight = Mathf.Clamp01(1f - Mathf.Abs(sunHeight) * 3f);

        if (sunLight != null)
        {
            float pitch = Mathf.Lerp(-30f, 210f, time01);
            sunLight.transform.rotation = Quaternion.Euler(pitch, sunYaw, 0f);
            sunLight.color = BlendColor(nightLightColor, sunsetLightColor, dayLightColor, daylight, twilight);
            sunLight.intensity = Mathf.Lerp(nightIntensity, dayIntensity, daylight) + twilight * 0.1f;
        }

        RenderSettings.ambientLight = BlendColor(nightAmbientColor, sunsetAmbientColor, dayAmbientColor, daylight, twilight);
        RenderSettings.fogColor = BlendColor(nightFogColor, sunsetFogColor, dayFogColor, daylight, twilight);
        RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, daylight);

        if (_runtimeSkybox != null)
        {
            SetSkyColor("_SkyTint", BlendColor(nightSkyTint, sunsetSkyTint, daySkyTint, daylight, twilight));
            SetSkyColor("_GroundColor", BlendColor(nightGroundColor, sunsetGroundColor, dayGroundColor, daylight, twilight));
            SetSkyFloat("_Exposure", Mathf.Lerp(nightExposure, dayExposure, daylight) + twilight * 0.08f);
        }
    }

    private Color BlendColor(Color night, Color sunset, Color day, float daylight, float twilight)
    {
        Color baseColor = Color.Lerp(night, day, daylight);
        return Color.Lerp(baseColor, sunset, twilight * (1f - Mathf.Abs(daylight - 0.5f) * 2f));
    }

    private void SetSkyColor(string propertyName, Color value)
    {
        if (_runtimeSkybox != null && _runtimeSkybox.HasProperty(propertyName))
        {
            _runtimeSkybox.SetColor(propertyName, value);
        }
    }

    private void SetSkyFloat(string propertyName, float value)
    {
        if (_runtimeSkybox != null && _runtimeSkybox.HasProperty(propertyName))
        {
            _runtimeSkybox.SetFloat(propertyName, value);
        }
    }
}
