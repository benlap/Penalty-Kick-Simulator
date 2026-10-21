
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using DG.Tweening;
using UnityEngine;

public class PenaltyGameStateController : MonoBehaviour
{
    public enum GameState
    {
        Title,
        PlayerKick,
        GoalMade
    }

    [Serializable]
    public class StateConfiguration
    {
        public GameState state;

        [Header("Camera")]
        [Tooltip("Cinemachine virtual camera used by this state.")]
        public CinemachineVirtualCameraBase camera;

        [Header("UI")]
        [Tooltip("CanvasGroup on the root of this state's UI.")]
        public CanvasGroup uiParent;

        [Min(0f)]
        public float fadeInDuration = 0.35f;

        [Min(0f)]
        public float fadeOutDuration = 0.25f;
    }

        [Header("Initial State")]
        [SerializeField] private GameState initialState = GameState.Title;

        [Header("State Configurations")]
        [SerializeField] private List<StateConfiguration> stateConfigurations =
            new List<StateConfiguration>();

        [Header("Camera Priorities")]
        [SerializeField] private int inactiveCameraPriority = 0;
        [SerializeField] private int activeCameraPriority = 20;

        [Header("Transition")]
        [Tooltip("Use unscaled time so transitions also work while timeScale is zero.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Title Screen Input")]
        [SerializeField] private bool anyKeyStartsGame = true;

        [Header("Gameplay References")]
        [SerializeField] private PenaltyPlayerController playerController;

        public GameState CurrentState { get; private set; }
        public bool IsTransitioning { get; private set; }

        public event Action<GameState, GameState> OnStateChanged;

        private readonly Dictionary<GameState, StateConfiguration> configurationLookup =
            new Dictionary<GameState, StateConfiguration>();

        private Sequence transitionSequence;
        private Coroutine transitionCoroutine;
        private bool initialized;

        private void Awake()
        {
            BuildConfigurationLookup();
            InitializeState(initialState);
        }

        private void OnDestroy()
        {
            if (transitionSequence != null)
                transitionSequence.Kill();

            if (transitionCoroutine != null)
                StopCoroutine(transitionCoroutine);

            KillAllUIAnimations();
        }

        private void Update()
        {
            if (!anyKeyStartsGame)
                return;

            if (CurrentState != GameState.Title)
                return;

            if (IsTransitioning)
                return;

            if (Input.anyKeyDown)
            {
                ShowPlayerKick();
            }
        }

        private void BuildConfigurationLookup()
        {
            configurationLookup.Clear();

            foreach (StateConfiguration config in stateConfigurations)
            {
                if (config == null)
                    continue;

                if (configurationLookup.ContainsKey(config.state))
                {
                    Debug.LogWarning(
                        $"Duplicate configuration for state {config.state}. " +
                        "Only the first configuration will be used.",
                        this);

                    continue;
                }

                configurationLookup.Add(config.state, config);
            }
        }

        private void InitializeState(GameState startState)
        {
            // Put every configured UI parent into its hidden state.
            foreach (StateConfiguration config in stateConfigurations)
            {
                if (config == null)
                    continue;

                if (config.uiParent != null)
                {
                    KillUIAnimation(config.uiParent);

                    config.uiParent.alpha = 0f;
                    config.uiParent.interactable = false;
                    config.uiParent.blocksRaycasts = false;
                }

                if (config.camera != null)
                    config.camera.Priority = inactiveCameraPriority;
            }

            CurrentState = startState;

            if (playerController != null)
            {
                playerController.SetInputEnabled(
                    startState == GameState.PlayerKick
                );
            }

            StateConfiguration initialConfig = GetConfiguration(startState);

            if (initialConfig == null)
            {
                Debug.LogError(
                    $"No configuration found for initial state {startState}.",
                    this);
                return;
            }

            if (initialConfig.camera != null)
                initialConfig.camera.Priority = activeCameraPriority;

            if (initialConfig.uiParent != null)
            {
                initialConfig.uiParent.alpha = 1f;
                initialConfig.uiParent.interactable = true;
                initialConfig.uiParent.blocksRaycasts = true;
            }

            initialized = true;
        }

        /// <summary>
        /// Transition to a new game state.
        /// </summary>
        public void SetState(GameState newState)
        {
            if (!initialized)
                return;

            if (newState == CurrentState && !IsTransitioning)
                return;

            StateConfiguration nextConfig = GetConfiguration(newState);

            if (nextConfig == null)
            {
                Debug.LogError(
                    $"Cannot transition to {newState}: no configuration exists.",
                    this);
                return;
            }

            CancelCurrentTransition();

            GameState previousState = CurrentState;

            StateConfiguration previousConfig = GetConfiguration(previousState);

            CurrentState = newState;
            IsTransitioning = true;

            if (playerController != null)
            {
                playerController.SetInputEnabled(
                    newState == GameState.PlayerKick
                );
            }

            OnStateChanged?.Invoke(previousState, newState);

            transitionCoroutine = StartCoroutine(
                TransitionRoutine(previousConfig, nextConfig));
        }

        private IEnumerator TransitionRoutine(
            StateConfiguration previousConfig,
            StateConfiguration nextConfig)
        {
            CanvasGroup previousUI =
                previousConfig != null ? previousConfig.uiParent : null;

            CanvasGroup nextUI = nextConfig.uiParent;

            // Fade out the current UI.
            if (previousUI != null && previousUI != nextUI)
            {
                previousUI.interactable = false;
                previousUI.blocksRaycasts = false;

                yield return FadeCanvasGroup(
                    previousUI,
                    0f,
                    previousConfig.fadeOutDuration);
            }

            // Switch camera priorities after the old UI fades out.
            foreach (StateConfiguration config in stateConfigurations)
            {
                if (config?.camera != null)
                    config.camera.Priority = inactiveCameraPriority;
            }

            if (nextConfig.camera != null)
                nextConfig.camera.Priority = activeCameraPriority;

            // Prepare the next UI before fading it in.
            if (nextUI != null)
            {
                nextUI.gameObject.SetActive(true);
                nextUI.alpha = 0f;
                nextUI.interactable = false;
                nextUI.blocksRaycasts = false;

                yield return FadeCanvasGroup(
                    nextUI,
                    1f,
                    nextConfig.fadeInDuration);

                nextUI.interactable = true;
                nextUI.blocksRaycasts = true;
            }

            // Ensure the previous UI is fully hidden.
            if (previousUI != null && previousUI != nextUI)
            {
                previousUI.alpha = 0f;
                previousUI.interactable = false;
                previousUI.blocksRaycasts = false;
            }

            IsTransitioning = false;
            transitionCoroutine = null;
        }

        private IEnumerator FadeCanvasGroup(
            CanvasGroup group,
            float targetAlpha,
            float duration)
        {
            if (group == null)
                yield break;

            if (duration <= 0f)
            {
                group.alpha = targetAlpha;
                yield break;
            }

            bool completed = false;

            Tween tween = group
                .DOFade(targetAlpha, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => completed = true);

            transitionSequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .Append(tween);

            yield return new WaitUntil(() => completed);
        }

        private StateConfiguration GetConfiguration(GameState state)
        {
            configurationLookup.TryGetValue(state, out StateConfiguration config);
            return config;
        }

        private void CancelCurrentTransition()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (transitionSequence != null)
            {
                transitionSequence.Kill();
                transitionSequence = null;
            }

            KillAllUIAnimations();

            IsTransitioning = false;
        }

        private void KillAllUIAnimations()
        {
            foreach (StateConfiguration config in stateConfigurations)
            {
                if (config != null && config.uiParent != null)
                    KillUIAnimation(config.uiParent);
            }
        }

        private void KillUIAnimation(CanvasGroup group)
        {
            if (group != null)
                DOTween.Kill(group);
        }

        // Public methods for UnityEvents and Inspector wiring.

        public void ShowTitle()
        {
            SetState(GameState.Title);
        }

        public void ShowPlayerKick()
        {
            SetState(GameState.PlayerKick);
        }

        public void ShowGoalMade()
        {
            SetState(GameState.GoalMade);
        }
    }
