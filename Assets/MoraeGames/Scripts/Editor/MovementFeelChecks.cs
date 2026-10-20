using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using MoraeGames.Player;
using MoraeGames.Network;
using MoraeGames.UI;

namespace MoraeGames.Editor
{
    /// <summary>Physics and delayed-snapshot regression tests, independent of user saves or cloud services.</summary>
    public static class MovementFeelChecks
    {
        public static void Run()
        {
            GameObject actor = null, wall = null, corner = null;
            try
            {
                Vector3 origin = new Vector3(10000f, 10000f, 10000f);
                actor = new GameObject("MovementTestActor");
                actor.transform.position = origin;
                var body = actor.AddComponent<Rigidbody>(); body.isKinematic = true;
                var capsule = actor.AddComponent<CapsuleCollider>();
                capsule.radius = 0.45f; capsule.height = 2f; capsule.center = Vector3.up;
                var motor = new PlanarMovementMotor(actor.transform, body, capsule);
                Physics.SyncTransforms();
                Near(motor.Step(origin, Vector2.right, 5f, 0.02f), origin + Vector3.right * 0.1f, "free movement");
                Near(motor.Step(origin, Vector2.zero, 5f, 0.02f), origin, "immediate stop");

                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = origin + new Vector3(2f, 1f, 0f);
                wall.transform.localScale = new Vector3(1f, 4f, 20f);
                Physics.SyncTransforms();
                Vector3 position = origin;
                for (int i = 0; i < 100; i++) position = motor.Step(position, Vector2.one.normalized, 5f, 0.02f);
                Check(position.x - origin.x < 1.06f && position.z - origin.z > 6f, "diagonal wall slide");
                Check(motor.Step(origin, Vector2.right, 200f, 0.02f).x - origin.x < 1.06f, "high-speed wall sweep");
                Vector3 touching = origin + Vector3.right * 1.06f;
                Vector3 escaped = motor.Step(touching, Vector2.up, 5f, 0.02f);
                Check(escaped.x - origin.x < 1.05f && escaped.z > touching.z + 0.08f, "overlapping contact recovers and slides");
                corner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                corner.transform.position = origin + new Vector3(0f, 1f, 2f);
                corner.transform.localScale = new Vector3(20f, 4f, 1f);
                Physics.SyncTransforms();
                position = origin;
                for (int i = 0; i < 80; i++) position = motor.Step(position, Vector2.one.normalized, 5f, 0.02f);
                Check(position.x - origin.x < 1.06f && position.z - origin.z < 1.06f, "closed corner cannot be crossed");
                wall.GetComponent<Collider>().enabled = false;
                corner.GetComponent<Collider>().enabled = false;
                Physics.SyncTransforms();

                // Six fixed ticks = 120 ms delayed server acknowledgement. Inputs remain immediate.
                var prediction = new ClientMovementPrediction(motor, origin, Quaternion.identity, 5f);
                Vector3[] authoritative = new Vector3[51]; authoritative[0] = origin;
                for (uint tick = 1; tick <= 50; tick++)
                {
                    authoritative[tick] = motor.Step(authoritative[tick - 1], Vector2.right, 5f, 0.02f);
                    prediction.Step(tick, Vector2.right, 0.02f, 720f);
                    if (tick > 6)
                    {
                        uint acknowledged = tick - 6;
                        prediction.Reconcile(acknowledged, acknowledged, authoritative[acknowledged], Quaternion.identity, 5f, false, 720f);
                    }
                    Near(prediction.Position, authoritative[tick], "prediction under 120ms snapshot delay");
                }
                Check(prediction.PendingCount == 6, "only unacknowledged inputs remain");
                Check(!prediction.Reconcile(43, 43, origin, Quaternion.identity, 5f, false, 720f), "reordered snapshot ignored");
                prediction.Reconcile(51, 50, authoritative[50], Quaternion.identity, 5f, false, 720f);
                Check(prediction.PendingCount == 0, "later ack retires inputs after packet loss");
                Vector3 prison = origin + Vector3.forward * 20f;
                prediction.Reconcile(52, 50, prison, Quaternion.identity, 5f, true, 720f);
                prediction.Step(51, Vector2.one, 0.02f, 720f);
                Near(prediction.VisualPosition, prison, "arrest teleport snaps and blocks prediction");
                prediction.Reconcile(53, 51, prison, Quaternion.identity, 5f, false, 720f);
                prediction.Step(52, Vector2.right, 0.02f, 720f);
                Check(prediction.Position.x > prison.x, "rescue resumes prediction");
                Check(ClientMovementPrediction.IsNewer(0, uint.MaxValue), "sequence wrap");
                for (uint i = 53; i < 200; i++) prediction.Step(i, Vector2.right, 0.02f, 720f);
                Check(prediction.Overflowed && prediction.PendingCount == 128, "bounded history during lost connection");
                Check(VirtualJoystick.FilterInput(new Vector2(0.03f, 0f), 0.08f) == Vector2.zero, "joystick dead zone");
                Check(Mathf.Abs(VirtualJoystick.FilterInput(Vector2.right, 0.08f).x - 1f) < 0.001f, "full joystick speed");
                Directory.CreateDirectory("TestResults");
                File.WriteAllText("TestResults/movement-feel.txt", "PASS: immediate movement/stop, wall sliding, fast sweep, closed corner, 120ms delayed acknowledgements, reordered snapshots, packet-loss recovery, arrest/rescue, sequence wrap, bounded history, joystick dead zone.");
                Debug.Log("[MovementFeelChecks] PASS");
            }
            finally
            {
                if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
                if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
                if (corner != null) UnityEngine.Object.DestroyImmediate(corner);
            }
        }

        private static void Near(Vector3 actual, Vector3 expected, string name) => Check(Vector3.Distance(actual, expected) < 0.02f, name);
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("Movement regression: " + name); }
    }
}
