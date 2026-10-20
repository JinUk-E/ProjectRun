using UnityEngine;
using UnityEngine.UI;
using MoraeGames.Player;
using MoraeGames.Core;

namespace MoraeGames.UI
{
    /// <summary>
    /// Renders a top-down minimap using a dedicated camera and RenderTexture.
    /// Shows player positions and key objective markers.
    /// Call <see cref="SetLocalPlayer"/> to assign the player to follow.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        [Header("Minimap Settings")]
        public RawImage minimapImage;
        public float mapSize = 60f; // World units visible
        public float minimapHeight = 80f;

        private Camera _minimapCamera;
        private RenderTexture _renderTexture;
        private Transform _localPlayer;
        private Material _fogMaterial;

        // Minimap markers
        private GameObject _prisonMarker;

        private void Start()
        {
            SetupMinimapCamera();
            CreatePrisonMarker();
            FindLocalPlayer();
        }

        private void SetupMinimapCamera()
        {
            GameObject camObj = new GameObject("MinimapCamera");
            _minimapCamera = camObj.AddComponent<Camera>();
            _minimapCamera.orthographic = true;
            _minimapCamera.orthographicSize = mapSize / 2f;
            _minimapCamera.cullingMask = ~0; // Everything
            _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            _minimapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
            _minimapCamera.depth = -1;

            // Top-down view
            camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camObj.transform.position = new Vector3(0f, minimapHeight, 0f);

            // Create render texture
            _renderTexture = new RenderTexture(256, 256, 16);
            _minimapCamera.targetTexture = _renderTexture;
            _fogMaterial = new Material(Resources.Load<Shader>("MinimapVisionFog"));
            _fogMaterial.SetFloat("_MapSize", mapSize);

            if (minimapImage != null)
            {
                minimapImage.texture = _renderTexture;
                minimapImage.material = _fogMaterial;
            }
        }

        private void CreatePrisonMarker()
        {
            _prisonMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _prisonMarker.name = "MinimapPrisonMarker";
            // Use the scene-injected prison position, not the hardcoded constant
            _prisonMarker.transform.position = PrisonHelper.GetPrisonPosition() + Vector3.up * 20f;
            _prisonMarker.transform.localScale = new Vector3(3f, 0.1f, 3f);
            Destroy(_prisonMarker.GetComponent<Collider>());
            RendererTint.SetColor(_prisonMarker.GetComponent<Renderer>(), new Color(1f, 0.3f, 0.3f, 0.8f));
        }

        /// <summary>
        /// Finds the local player once at startup. No per-frame search.
        /// </summary>
        private void FindLocalPlayer()
        {
            var players = PlayerController.Instances;
            foreach (var p in players)
            {
                if (p != null && p.IsLocallyControlled)
                {
                    _localPlayer = p.transform;
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            // Retry until the local player exists (spawn order is not guaranteed)
            if (_localPlayer == null)
            {
                FindLocalPlayer();
                if (_localPlayer == null) return;
            }

            // Follow the local player
            if (_minimapCamera != null)
            {
                _minimapCamera.transform.position = new Vector3(
                    _localPlayer.position.x, minimapHeight, _localPlayer.position.z);
            }
        }

        /// <summary>Sets the minimap display target image.</summary>
        public void SetMinimapImage(RawImage image)
        {
            minimapImage = image;
            if (_renderTexture != null)
            {
                minimapImage.texture = _renderTexture;
                minimapImage.material = _fogMaterial;
            }
        }

        /// <summary>Explicitly sets the local player to follow on the minimap.</summary>
        public void SetLocalPlayer(Transform player)
        {
            _localPlayer = player;
        }

        private void OnDestroy()
        {
            if (_fogMaterial != null) Destroy(_fogMaterial);
            if (_minimapCamera != null) Destroy(_minimapCamera.gameObject);
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
            if (_prisonMarker != null)
                Destroy(_prisonMarker);
        }
    }
}
