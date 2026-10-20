using UnityEngine;

namespace MoraeGames.Game
{
    [DefaultExecutionOrder(100)]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 30, -20);
        public float smoothSpeed = 10f;
        private Transform _lastTarget;
        private void Awake()
        {
            if (GetComponent<FogOfWarCamera>() == null) gameObject.AddComponent<FogOfWarCamera>();
        }

        private void LateUpdate()
        {
            if (target != null)
            {
                Vector3 desiredPosition = target.position + offset;
                // Exponential damping gives the same response at 30/60/120 fps.
                bool snap = _lastTarget != target || Vector3.Distance(transform.position, desiredPosition) > 8f;
                float response = 1f - Mathf.Exp(-Mathf.Max(18f, smoothSpeed) * Time.deltaTime);
                transform.position = snap ? desiredPosition : Vector3.Lerp(transform.position, desiredPosition, response);
                _lastTarget = target;
                
                // Optional: keep camera looking at target
                // transform.LookAt(target); 
                // Since the camera is statically angled from InGameSetupEditor, we can just keep its rotation fixed or LookAt.
                // Keeping rotation fixed to Euler(60, 0, 0) as set in Setup is fine.
            }
        }
    }
}
