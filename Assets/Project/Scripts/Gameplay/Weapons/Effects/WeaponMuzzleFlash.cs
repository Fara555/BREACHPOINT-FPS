using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    [DisallowMultipleComponent]
    public sealed class WeaponMuzzleFlash : MonoBehaviour
    {
        private const string FlareObjectName = "Flare Graph Planes";
        private const string SparksObjectName = "Sparks VFX Graph";
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("VFX Graph")]
        [SerializeField] private VisualEffectAsset _sparksAsset;

        [Header("Graph Planes Flare")]
        [SerializeField] private Material _flareMaterial;
        [SerializeField, Min(0.05f)] private float _flareLength = 0.36f;
        [SerializeField, Min(0.01f)] private float _flareRadius = 0.115f;
        [SerializeField, Range(2, 6)] private int _planeCount = 3;
        [SerializeField, Min(0f)] private float _flareDuration = 0.038f;
        [SerializeField] private Vector2 _flareScaleRange = new(0.9f, 1.1f);
        [SerializeField, ColorUsage(false, true)]
        private Color _flareColor = new(4.5f, 0.72f, 0.08f, 1f);

        [Header("Light")]
        [SerializeField] private Light _flashLight;
        [SerializeField, Min(0f)] private float _lightDuration = 0.028f;
        [SerializeField] private Vector2 _lightIntensityRange = new(220f, 340f);
        [SerializeField, Min(0f)] private float _lightRange = 4.5f;
        [SerializeField] private Color _lightColor = new(1f, 0.48f, 0.16f);

        private MeshRenderer _flareRenderer;
        private Transform _flareTransform;
        private VisualEffect _sparks;
        private Material _runtimeFlareMaterial;
        private Mesh _runtimeFlareMesh;
        private float _flareDisableTime;
        private Vector3 _flareBaseScale;
        private float _lightDisableTime;
        private float _lightPeakIntensity;
        private float _sparksStopTime;
        private bool _sparksSpawnerActive;

        private void Awake()
        {
            BuildFlare();
            BuildSparks();
            SetFlareVisible(false);
            DisableLight();
        }

        private void Update()
        {
            UpdateFlare();
            UpdateSparks();
            UpdateFlashLight();
        }

        private void OnDisable()
        {
            SetFlareVisible(false);
            _sparks?.Stop();
            _sparksSpawnerActive = false;
            DisableLight();
        }

        private void OnDestroy()
        {
            Destroy(_runtimeFlareMaterial);
            Destroy(_runtimeFlareMesh);
        }

        public void Play()
        {
            PlayFlare();
            PlaySparks();
            EnableLight();
        }

        private void BuildFlare()
        {
            Transform existing = transform.Find(FlareObjectName);
            GameObject flareObject;
            if (existing != null)
            {
                flareObject = existing.gameObject;
            }
            else
            {
                flareObject = new GameObject(FlareObjectName);
                flareObject.layer = gameObject.layer;
                flareObject.transform.SetParent(transform, false);
            }

            _flareTransform = flareObject.transform;
            MeshFilter meshFilter = flareObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = flareObject.AddComponent<MeshFilter>();
            }

            _flareRenderer = flareObject.GetComponent<MeshRenderer>();
            if (_flareRenderer == null)
            {
                _flareRenderer = flareObject.AddComponent<MeshRenderer>();
            }

            _runtimeFlareMesh = CreateGraphPlanesMesh();
            meshFilter.sharedMesh = _runtimeFlareMesh;

            if (_flareMaterial != null)
            {
                _runtimeFlareMaterial = new Material(_flareMaterial)
                {
                    name = $"{_flareMaterial.name} (Runtime)"
                };
                _flareRenderer.sharedMaterial = _runtimeFlareMaterial;
            }

            _flareRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _flareRenderer.receiveShadows = false;
            _flareRenderer.lightProbeUsage = LightProbeUsage.Off;
            _flareRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _flareRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private Mesh CreateGraphPlanesMesh()
        {
            int planeCount = Mathf.Clamp(_planeCount, 2, 6);
            Vector3[] vertices = new Vector3[planeCount * 4];
            Vector2[] uvs = new Vector2[planeCount * 4];
            int[] triangles = new int[planeCount * 12];

            for (int i = 0; i < planeCount; i++)
            {
                float angle = Mathf.PI * i / planeCount;
                Vector3 radial = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 planeOffset = radial * _flareRadius;
                int vertex = i * 4;
                int triangle = i * 12;

                vertices[vertex] = -planeOffset;
                vertices[vertex + 1] = planeOffset;
                vertices[vertex + 2] = -planeOffset + Vector3.forward * _flareLength;
                vertices[vertex + 3] = planeOffset + Vector3.forward * _flareLength;

                uvs[vertex] = new Vector2(0f, 0f);
                uvs[vertex + 1] = new Vector2(1f, 0f);
                uvs[vertex + 2] = new Vector2(0f, 1f);
                uvs[vertex + 3] = new Vector2(1f, 1f);

                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
                triangles[triangle + 6] = vertex + 1;
                triangles[triangle + 7] = vertex + 2;
                triangles[triangle + 8] = vertex;
                triangles[triangle + 9] = vertex + 3;
                triangles[triangle + 10] = vertex + 2;
                triangles[triangle + 11] = vertex + 1;
            }

            Mesh mesh = new()
            {
                name = "Muzzle Flare Graph Planes",
                vertices = vertices,
                uv = uvs,
                triangles = triangles,
                bounds = new Bounds(
                    Vector3.forward * (_flareLength * 0.5f),
                    new Vector3(_flareRadius * 2f, _flareRadius * 2f, _flareLength))
            };
            return mesh;
        }

        private void BuildSparks()
        {
            if (_sparksAsset == null)
            {
                return;
            }

            Transform existing = transform.Find(SparksObjectName);
            GameObject sparksObject;
            if (existing != null)
            {
                sparksObject = existing.gameObject;
            }
            else
            {
                sparksObject = new GameObject(SparksObjectName);
                sparksObject.layer = gameObject.layer;
                sparksObject.transform.SetParent(transform, false);
            }

            sparksObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sparksObject.transform.localPosition = Vector3.forward * 0.04f;
            _sparks = sparksObject.GetComponent<VisualEffect>();
            if (_sparks == null)
            {
                _sparks = sparksObject.AddComponent<VisualEffect>();
            }

            _sparks.visualEffectAsset = _sparksAsset;
            _sparks.initialEventName = string.Empty;
            _sparks.pause = false;
            _sparks.Reinit();
            _sparks.Stop();
        }

        private void PlayFlare()
        {
            if (_flareRenderer == null)
            {
                return;
            }

            float scale = Random.Range(_flareScaleRange.x, _flareScaleRange.y);
            _flareBaseScale = new Vector3(scale, scale, Random.Range(0.88f, 1.12f));
            _flareTransform.localScale = _flareBaseScale;
            _flareTransform.localRotation = Quaternion.AngleAxis(
                Random.Range(0f, 360f),
                Vector3.forward);

            if (_runtimeFlareMaterial != null)
            {
                Color color = _flareColor * Random.Range(0.85f, 1.2f);
                SetMaterialColor(_runtimeFlareMaterial, color);
            }

            SetFlareVisible(true);
            _flareDisableTime = Time.time + _flareDuration;
        }

        private void PlaySparks()
        {
            if (_sparks == null)
            {
                return;
            }

            _sparks.enabled = true;
            _sparks.gameObject.SetActive(true);
            _sparks.Reinit();
            _sparks.Play();
            _sparksStopTime = Time.time + 0.05f;
            _sparksSpawnerActive = true;
        }

        private void UpdateSparks()
        {
            if (!_sparksSpawnerActive || _sparks == null || Time.time < _sparksStopTime)
            {
                return;
            }

            _sparks.Stop();
            _sparksSpawnerActive = false;
        }

        private void UpdateFlare()
        {
            if (_flareRenderer == null || !_flareRenderer.enabled)
            {
                return;
            }

            float remaining = _flareDisableTime - Time.time;
            if (remaining <= 0f || _flareDuration <= 0f)
            {
                SetFlareVisible(false);
                return;
            }

            float normalized = remaining / _flareDuration;
            float pulse = Mathf.Sqrt(normalized);
            _flareTransform.localScale = new Vector3(
                _flareBaseScale.x,
                _flareBaseScale.y,
                _flareBaseScale.z * Mathf.Lerp(0.94f, 1f, pulse));

            if (_runtimeFlareMaterial != null)
            {
                SetMaterialColor(_runtimeFlareMaterial, _flareColor * (pulse * 1.4f));
            }
        }

        private void EnableLight()
        {
            if (_flashLight == null) return;
            _lightPeakIntensity = Random.Range(_lightIntensityRange.x, _lightIntensityRange.y);
            _flashLight.color = _lightColor;
            _flashLight.range = _lightRange;
            _flashLight.intensity = _lightPeakIntensity;
            _flashLight.enabled = true;
            _lightDisableTime = Time.time + _lightDuration;
        }

        private void UpdateFlashLight()
        {
            if (_flashLight == null || !_flashLight.enabled) return;
            float remaining = _lightDisableTime - Time.time;
            if (remaining <= 0f || _lightDuration <= 0f)
            {
                DisableLight();
                return;
            }

            float normalized = remaining / _lightDuration;
            _flashLight.intensity = _lightPeakIntensity * normalized * normalized;
        }

        private void SetFlareVisible(bool visible)
        {
            if (_flareRenderer != null)
            {
                _flareRenderer.enabled = visible;
            }
        }

        private void DisableLight()
        {
            if (_flashLight != null)
            {
                _flashLight.enabled = false;
            }
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty(EmissiveColorId)) material.SetColor(EmissiveColorId, color);
            if (material.HasProperty(UnlitColorId)) material.SetColor(UnlitColorId, color);
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        private void OnValidate()
        {
            _flareScaleRange.x = Mathf.Max(0.01f, _flareScaleRange.x);
            _flareScaleRange.y = Mathf.Max(_flareScaleRange.x, _flareScaleRange.y);
            _lightIntensityRange.x = Mathf.Max(0f, _lightIntensityRange.x);
            _lightIntensityRange.y = Mathf.Max(_lightIntensityRange.x, _lightIntensityRange.y);
        }
    }
}
