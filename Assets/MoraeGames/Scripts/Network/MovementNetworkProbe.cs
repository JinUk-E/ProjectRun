#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using MoraeGames.Player;
using MoraeGames.Game;

namespace MoraeGames.Network
{
    /// <summary>Opt-in development-player integration probe. Excluded from release builds.</summary>
    public sealed class MovementNetworkProbe : MonoBehaviour
    {
        private bool _host;
        private string _report;
        private float _started;
        private float _playingAt = -1f;
        private NetworkCharacter _human;
        private RobberController _robber;
        private Vector3 _origin;
        private Vector2 _direction;
        private bool _firstInput, _arrested, _rescued, _finished;
        private bool _arrestSnapVerified;
        private Animator _visualAnimator;
        private bool _runVerified;
        private float _maxAnimationSpeed;
        private MoraeGames.UI.InGameUIManager _hud;
        private string _initialTimer;
        private bool _hudTimerChanged, _hudArrested;
        private float _arrestTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            string role = args.FirstOrDefault(s => s.StartsWith("--movement-probe="));
            if (role == null) return;
            Application.runInBackground = true;
            var bootstrap = FindFirstObjectByType<ServerBootstrap>();
            if (bootstrap != null) bootstrap.enabled = false;
            var probe = new GameObject("MovementNetworkProbe").AddComponent<MovementNetworkProbe>();
            probe._host = role.EndsWith("host");
            probe._report = args.First(s => s.StartsWith("--movement-report=")).Substring("--movement-report=".Length);
            DontDestroyOnLoad(probe.gameObject);
        }

        private void Start()
        {
            _started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
            bool started = _host ? NetworkSessionManager.StartHost() : NetworkSessionManager.StartClient("127.0.0.1");
            if (!started) Finish(false, "Session startup failed");
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string trace, LogType type)
        {
            if (!_finished && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                Finish(false, message);
        }

        private void Update()
        {
            if (_finished) return;
            if (Time.realtimeSinceStartup - _started > 45f) { Finish(false, "Timeout"); return; }
            var nm = NetworkManager.Singleton;
            if (_host && !NetworkSessionManager.MatchStarted && nm.ConnectedClientsList.Count == 2)
                NetworkSessionManager.BeginMatch();
            if (!NetworkCharacter.MatchAllowsActions) return;
            if (_playingAt < 0f)
            {
                _playingAt = Time.time;
                foreach (var ai in FindObjectsByType<AIController>(FindObjectsSortMode.None)) ai.enabled = false;
                foreach (var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
                    if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                _human = FindObjectsByType<NetworkCharacter>(FindObjectsSortMode.None)
                    .FirstOrDefault(c => !c.IsAiControlled && (_host ? !c.IsOwnedByServer : c.IsOwner));
                if (_human == null) { Finish(false, "Remote human missing"); return; }
                _robber = _human.GetComponent<RobberController>();
                if (_robber == null) { Finish(false, "Client should occupy robber slot"); return; }
                _origin = _human.transform.position;
                _hud = FindFirstObjectByType<MoraeGames.UI.InGameUIManager>();
                if (_hud == null || _hud.timerText == null) { Finish(false, "HUD wiring missing"); return; }
                _initialTimer = _hud.timerText.text;
                _visualAnimator = _human.GetComponentInChildren<Animator>();
                if (!_host)
                {
                    var player = _human.GetComponent<PlayerController>();
                    player.isLocalPlayer = false;
                    _direction = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right }
                        .OrderByDescending(d => Vector3.Distance(player.MovementMotor.Step(_origin, d, 5f, 0.2f), _origin)).First();
                }
            }
            float elapsed = Time.time - _playingAt;
            if (_host)
            {
                if (!_arrested && elapsed > 2f)
                {
                    if (Vector3.Distance(_origin, _human.transform.position) < 0.1f)
                    {
                        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        Finish(false, "Client input did not move server body. enabled=" + _robber.enabled +
                            " kinematic=" + _human.GetComponent<Rigidbody>().isKinematic + " arrested=" + _robber.IsArrested +
                            " received=" + typeof(NetworkCharacter).GetField("_receivedSequence", flags).GetValue(_human) +
                            " input=" + typeof(NetworkCharacter).GetField("_moveInput", flags).GetValue(_human));
                        return;
                    }
                    _robber.OnArrested(); _arrested = true;
                }
                if (!_rescued && elapsed > 3.5f)
                {
                    var prison = FindFirstObjectByType<PrisonZone>();
                    var rescuer = FindObjectsByType<RobberController>(FindObjectsSortMode.None).First(r => !r.IsArrested);
                    rescuer.transform.position=prison.InteractionPosition;
                    var rescueBody=rescuer.GetComponent<Rigidbody>(); if(rescueBody!=null) rescueBody.position=prison.InteractionPosition;
                    if (!prison.TryStartRescue(rescuer.gameObject)) { Finish(false, "Rescue could not start"); return; }
                    prison.UpdateRescue(prison.rescueDuration + 0.1f);
                    _rescued = !_robber.IsArrested;
                }
                if (elapsed > 7f) Finish(_arrested && _rescued, "Host accepted client input, arrested and rescued the remote player");
            }
            else
            {
                if (_hud.timerText.text != _initialTimer) _hudTimerChanged = true;
                if (_robber.IsArrested && _hud.DisplayedArrested > 0 && _hud.DisplayedPhase.Contains("체포됨")) _hudArrested = true;
                if (_visualAnimator != null && !_arrested)
                {
                    _maxAnimationSpeed = Mathf.Max(_maxAnimationSpeed, _visualAnimator.GetFloat("Speed"));
                    if (_maxAnimationSpeed > 0.05f) _runVerified = true;
                }
                if (_robber.IsArrested)
                {
                    if (!_arrested) { _arrested = true; _arrestTime = Time.time; }
                    if (!_arrestSnapVerified && Time.time - _arrestTime > 0.3f)
                    {
                        // Verify the arrest once; a later rescue snapshot can precede its state flag.
                        if (Vector3.Distance(_human.PresentedPosition, MoraeGames.Core.PrisonHelper.GetPrisonPosition()) > 0.2f)
                            Finish(false, "Prediction did not snap to prison: presented=" + _human.PresentedPosition);
                        else if (_visualAnimator == null || !_visualAnimator.GetBool("Arrested"))
                            Finish(false, "Arrest animation was not activated");
                        else _arrestSnapVerified = true;
                    }
                }
                else if (_arrested && elapsed > 4f)
                    Finish(_firstInput && _arrestSnapVerified && _runVerified && _hudTimerChanged && _hudArrested,
                        "Prediction=" + _firstInput + ", arrest=" + _arrestSnapVerified + ", run=" + _runVerified +
                        ", timerHUD=" + _hudTimerChanged + ", arrestHUD=" + _hudArrested + "; received rescue");
            }
        }

        private void FixedUpdate()
        {
            if (_host || _finished || _human == null || _playingAt < 0f) return;
            float elapsed = Time.time - _playingAt;
            Vector2 input = elapsed > 0.4f && elapsed < 1.3f ? _direction : Vector2.zero;
            Vector3 previous = _human.PresentedPosition;
            Vector3 serverPosition = _human.transform.position;
            _human.SubmitMovement(input);
            if (!_firstInput && input != Vector2.zero && Vector3.Distance(previous, _human.PresentedPosition) > 0.001f)
            {
                _firstInput = true;
                if (_human.transform.position != serverPosition) Finish(false, "Prediction changed authoritative root");
            }
        }

        private void Finish(bool passed, string detail)
        {
            if (_finished) return;
            _finished = true;
            File.WriteAllText(_report, (passed ? "PASS: " : "FAIL: ") + detail);
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
