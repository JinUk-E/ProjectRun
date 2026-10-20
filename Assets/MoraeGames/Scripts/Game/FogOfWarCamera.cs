using UnityEngine;

namespace MoraeGames.Game
{
    /// <summary>Camera-local fog mask shared by gameplay and minimap; does not change server simulation.</summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)]
    public sealed class FogOfWarCamera : MonoBehaviour
    {
        public Transform target;
        public float clearRadius = 10f;
        public float hiddenRadius = 14f;
        private Camera _camera;
        private CameraFollow _follow;
        private GameObject _quad;
        private Mesh _mesh;
        private Material _material;
        private Matrix4x4 _projection;
        private readonly Vector3[] _corners = new Vector3[4];
        private static readonly int Center = Shader.PropertyToID("_VisionCenter");
        private static readonly int Radii = Shader.PropertyToID("_VisionRadii");
        private static readonly int Owner = Shader.PropertyToID("_OwnerCameraPosition");

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _follow = GetComponent<CameraFollow>();
            _material = new Material(Resources.Load<Shader>("LocalVisionFog"));
            _quad = new GameObject("LocalVisionFog", typeof(MeshFilter), typeof(MeshRenderer));
            _quad.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "Vision mask" };
            _mesh.vertices = _corners;
            _mesh.triangles = new[] { 0,1,2,0,2,3 };
            _quad.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Per-camera geometry must not be drawn into another camera (e.g. the minimap).
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }

        private void BeforeCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera) return;
            RefreshMask();
        }
        private void LateUpdate()
        {
            RefreshMask();
            foreach (var subject in LocalVisionSubject.Instances)
            {
                Vector3 offset = target != null ? subject.transform.position - target.position : Vector3.zero;
                offset.y = 0;
                subject.SetHidden(target == null || offset.sqrMagnitude > hiddenRadius * hiddenRadius);
            }
        }

        private void OnDisable()
        {
            foreach (var subject in LocalVisionSubject.Instances) subject.SetHidden(false);
        }

        private void RefreshMask()
        {
            if (_follow != null) target = _follow.target;
            _material.SetVector(Owner, transform.position);
            if (_projection != _camera.projectionMatrix)
            {
                _projection = _camera.projectionMatrix;
                float distance = _camera.nearClipPlane + .05f;
                _camera.CalculateFrustumCorners(new Rect(0,0,1,1), distance, Camera.MonoOrStereoscopicEye.Mono, _corners);
                _mesh.vertices = _corners;
                _mesh.RecalculateBounds();
            }
            _material.SetVector(Center, target != null ? target.position : Vector3.zero);
            // Static landmarks remain readable even before a player is assigned.
            _material.SetVector(Radii, target != null ? new Vector4(clearRadius, hiddenRadius, 1, 0) : new Vector4(0,.01f,0,0));
        }

        private void OnDestroy()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            if (_quad != null) Destroy(_quad);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
