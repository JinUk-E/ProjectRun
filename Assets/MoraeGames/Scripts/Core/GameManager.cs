using UnityEngine;
using System;

namespace MoraeGames.Core
{
    /// <summary>
    /// Global game state manager. Singleton that persists across scenes.
    /// Broadcasts state transitions and system messages via events.
    /// </summary>
    public class GameManager : SingletonBase<GameManager>
    {
        public enum GameState
        {
            Lobby,
            RoleSelection,
            Ready,
            Playing,
            Result
        }

        /// <summary>The current game state.</summary>
        public GameState CurrentState { get; private set; }

        /// <summary>Fired whenever the game state changes.</summary>
        public event Action<GameState> OnStateChanged;
        /// <summary>Fired when the game enters the Ready state.</summary>
        public event Action OnGameReady;
        /// <summary>Fired when the game enters the Playing state.</summary>
        public event Action OnGameStart;
        /// <summary>Fired when the game ends. Parameter is the winner role name.</summary>
        public event Action<string> OnGameEnd;

        /// <summary>Fired when a system message is broadcast (message, color).</summary>
        public event Action<string, Color> OnSystemMessage;

        // No Start() scene load here: an auto-created instance (first accessed via
        // the Instance getter mid-game) must never hijack the active scene.
        // CurrentState defaults to Lobby; InGameManager drives Ready/Playing.

        public void ReturnToLobby()
        {
            ChangeState(GameState.Lobby);
        }

        /// <summary>Broadcasts game end: transitions to Result and fires OnGameEnd.</summary>
        public void NotifyGameEnd(string winnerName)
        {
            ChangeState(GameState.Result);
            OnGameEnd?.Invoke(winnerName);
        }

        /// <summary>Transitions to a new game state and fires relevant events.</summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            Debug.Log($"Game State changed to: {newState}");
            OnStateChanged?.Invoke(newState);

            switch (newState)
            {
                case GameState.Lobby:
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
                    break;
                case GameState.Ready:
                    OnGameReady?.Invoke();
                    break;
                case GameState.Playing:
                    OnGameStart?.Invoke();
                    break;
            }
        }

        /// <summary>Broadcasts a system message to all listeners.</summary>
        public void BroadcastSystemMessage(string message, Color color)
        {
            OnSystemMessage?.Invoke(message, color);
            Debug.Log($"[SystemMessage] {message}");
        }
    }
}
