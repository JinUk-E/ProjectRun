using UnityEngine;
using MoraeGames.Player;

namespace MoraeGames.Network
{
    /// <summary>Bounded input history. Server snapshots retire acknowledged input and replay the remainder.</summary>
    public sealed class ClientMovementPrediction
    {
        private struct Frame { public uint Sequence; public Vector2 Input; public float Delta; }
        private readonly Frame[] _frames = new Frame[128];
        private readonly PlanarMovementMotor _motor;
        private int _head;
        private int _count;
        private uint _lastSnapshot;
        private bool _hasSnapshot;
        private Vector3 _visualError;
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 VisualPosition => Position + _visualError;
        public int PendingCount => _count;
        public float ServerSpeed { get; private set; }
        public bool Blocked { get; private set; }
        public bool Overflowed { get; private set; }

        public ClientMovementPrediction(PlanarMovementMotor motor, Vector3 position, Quaternion rotation, float speed)
        {
            _motor = motor; Position = position; Rotation = rotation; ServerSpeed = speed;
        }

        public static bool IsNewer(uint value, uint reference) => unchecked((int)(value - reference)) > 0;

        public void Step(uint sequence, Vector2 input, float deltaTime, float turnSpeed)
        {
            if (Blocked || Overflowed) return;
            if (_count == _frames.Length) { Overflowed = true; return; }
            input = NetworkCharacter.SanitizeInput(input);
            _frames[(_head + _count++) % _frames.Length] = new Frame { Sequence = sequence, Input = input, Delta = deltaTime };
            Position = _motor.Step(Position, input, ServerSpeed, deltaTime);
            Rotation = PlanarMovementMotor.Turn(Rotation, input, turnSpeed, deltaTime);
        }

        /// <summary>Returns false for duplicate or reordered snapshots. Never sends positions to the server.</summary>
        public bool Reconcile(uint snapshot, uint acknowledged, Vector3 position, Quaternion rotation,
            float speed, bool blocked, float turnSpeed)
        {
            if (_hasSnapshot && !IsNewer(snapshot, _lastSnapshot)) return false;
            _hasSnapshot = true;
            _lastSnapshot = snapshot;
            Vector3 previousVisual = VisualPosition;
            bool snap = blocked || Blocked || Overflowed || Vector3.Distance(position, Position) > 3f;
            while (_count > 0 && !IsNewer(_frames[_head].Sequence, acknowledged))
            { _head = (_head + 1) % _frames.Length; _count--; }
            if (snap) { _head = 0; _count = 0; }
            ServerSpeed = Mathf.Max(0f, speed);
            Blocked = blocked;
            Overflowed = false;
            Position = position;
            Rotation = rotation;
            for (int i = 0; i < _count; i++)
            {
                Frame frame = _frames[(_head + i) % _frames.Length];
                Position = _motor.Step(Position, frame.Input, ServerSpeed, frame.Delta);
                Rotation = PlanarMovementMotor.Turn(Rotation, frame.Input, turnSpeed, frame.Delta);
            }
            _visualError = snap ? Vector3.zero : Vector3.ClampMagnitude(previousVisual - Position, 0.5f);
            return true;
        }

        public void SmoothCorrection(float deltaTime) => _visualError *= Mathf.Exp(-20f * deltaTime);
    }
}
