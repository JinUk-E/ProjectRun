#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using MoraeGames.Game;

namespace MoraeGames.Player
{
    /// <summary>Runs on the baked game map with frozen actors; never awards progression.</summary>
    public static class PolicePursuitChecks
    {
        public static void Run(CopController cop, RobberController robber, HeistBuilding building)
        {
            var ai = cop.GetComponent<AICopController>();
            var agent = cop.GetComponent<NavMeshAgent>();
            Vector3 saved = robber.transform.position;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var target = typeof(AICopController).GetField("_currentTarget", flags);
            var last = typeof(AICopController).GetField("_lastKnownPosition", flags);
            var seen = typeof(AICopController).GetField("_lastSeenAt", flags);
            try
            {
                Vector3 center = building.transform.position;
                Check(agent.Warp(center + Vector3.back * 8), "Police approach not walkable");
                robber.transform.position = center + Vector3.back * 3;
                Physics.SyncTransforms();
                ai.SendMessage("DetectRobber");
                Check((Transform)target.GetValue(ai) == robber.transform, "Visible robber was not acquired");
                last.SetValue(ai, robber.transform.position - Vector3.right * .5f);
                seen.SetValue(ai, Time.time - .2f);
                ai.SendMessage("ChaseAndArrestTarget");
                Check(agent.destination.x > robber.transform.position.x + .2f, "Pursuit did not lead visible motion");
                Vector3 observed = robber.transform.position;
                robber.transform.position = center + new Vector3(-7, 0, 2);
                Physics.SyncTransforms();
                Check(!HeistBuilding.HasSight(cop.transform.position, robber.transform.position), "Test target not hidden");
                ai.SendMessage("ChaseAndArrestTarget");
                Check(target.GetValue(ai) == null, "Hidden target still tracked live");
                Check(Vector3.Distance(agent.destination, observed) < 1, "Lost sight did not pursue last observation");
                robber.transform.position = center + new Vector3(-9, 0, 4);
                Physics.SyncTransforms();
                ai.SendMessage("SearchForRobber");
                Check(Vector3.Distance(agent.destination, observed) < 1, "Search followed hidden movement");
                seen.SetValue(ai, Time.time - ai.searchMemorySeconds - 1);
                target.SetValue(ai, null);
                typeof(AICopController).GetField("_wanderTimer", flags).SetValue(ai, 1f);
                ai.SendMessage("SearchForRobber");
                Check(Vector3.Distance(agent.destination, building.loot.transform.position) < 2, "Expired search did not return to alarm objective");
                File.WriteAllText("TestResults/police-pursuit.txt", "PASS: visible acquisition; movement lead; wall occlusion; pursuit of last seen position; hidden movement not tracked; expired search returns to alarm.");
            }
            finally
            {
                robber.transform.position = saved;
                target.SetValue(ai, null); seen.SetValue(ai, -100f);
                Physics.SyncTransforms();
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
