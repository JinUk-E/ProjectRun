using UnityEngine;
using MoraeGames.Core;

namespace MoraeGames.Game
{
    /// <summary>
    /// Timer-based co-op switch: stepping on the trigger opens a linked door
    /// for a set duration before automatically closing it.
    /// </summary>
    public class CoopSwitch : MonoBehaviour
    {
        [Header("Switch Settings")]
        public GameObject linkedDoor;
        public float doorOpenDuration = GameConstants.CoopSwitchDoorDuration;

        private float _doorTimer = 0f;
        private bool _isDoorOpen = false;

        private Vector3 _doorClosedPosition;
        private Vector3 _doorOpenPosition;

        // Cached door collider (Rule §2: cache in Awake/Start)
        private Collider _doorCollider;

        private void Start()
        {
            if (linkedDoor != null)
            {
                _doorClosedPosition = linkedDoor.transform.position;
                _doorOpenPosition = _doorClosedPosition + Vector3.up * 4f; // Slide up to open
                _doorCollider = linkedDoor.GetComponent<Collider>();
            }
        }

        private void Update()
        {
            if (!_isDoorOpen || linkedDoor == null) return;

            // Spec: the door stays open for a fixed duration, then auto-closes.
            // Purely timer-driven — no occupancy counter that a destroyed object
            // (e.g. an expired decoy) could leave permanently out of sync.
            _doorTimer -= Time.deltaTime;
            if (_doorTimer <= 0f)
            {
                CloseDoor();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Real characters only (decoys/props have no PlayerController)
            if (other.transform.root.GetComponent<MoraeGames.Player.PlayerController>() == null) return;

            // (Re)opens and refreshes the timer while someone keeps stepping on it
            OpenDoor();
        }

        private void OpenDoor()
        {
            _isDoorOpen = true;
            _doorTimer = doorOpenDuration;
            if (linkedDoor != null)
            {
                linkedDoor.transform.position = _doorOpenPosition;
                if (_doorCollider != null) _doorCollider.enabled = false;
            }
            Debug.Log($"[CoopSwitch] Door opened for {doorOpenDuration}s!");
        }

        private void CloseDoor()
        {
            _isDoorOpen = false;
            _doorTimer = 0f;
            if (linkedDoor != null)
            {
                linkedDoor.transform.position = _doorClosedPosition;
                if (_doorCollider != null) _doorCollider.enabled = true;
            }
            Debug.Log("[CoopSwitch] Door closed.");
        }
    }
}
