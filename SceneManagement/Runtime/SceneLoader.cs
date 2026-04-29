using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using CupkekGames.Singleton;
using CupkekGames.Luna;

namespace CupkekGames.SceneManagement
{
    /// <summary>
    /// Build-index scene loading API (singleton). Does not load any scene by itself — add <see cref="SceneLoaderStartup"/> if you want an automatic first scene from the init scene.
    /// </summary>
    public class SceneLoader : Singleton<SceneLoader>
    {
        private SceneTransition _pendingFadeOutTransition;
        private string _pendingSceneName;
        private int _pendingSceneIndex = -1;
        private bool _hasDeferredCompletion;

        public event Action<string, int> OnSceneLoad;

        public void LoadScene(string sceneName, SceneTransition sceneLoadTransition,
            bool deferFadeOutUntilManualComplete = false)
        {
            StartCoroutine(LoadSceneAsync(sceneName, -1, sceneLoadTransition, deferFadeOutUntilManualComplete));
        }

        public void LoadScene(int sceneIndex, SceneTransition sceneLoadTransition,
            bool deferFadeOutUntilManualComplete = false)
        {
            StartCoroutine(LoadSceneAsync(null, sceneIndex, sceneLoadTransition, deferFadeOutUntilManualComplete));
        }

        /// <summary>
        /// Call after a load started with <c>deferFadeOutUntilManualComplete: true</c> to hide the loading transition and raise <see cref="OnSceneLoad"/>.
        /// No-op if there is no pending deferred load (e.g. cold play from a single scene).
        /// </summary>
        public void CompleteDeferredLoadingTransition()
        {
            TryCompleteDeferredLoadingTransition();
        }

        /// <summary>
        /// Returns true if a deferred scene load was pending and the fade-out was applied.
        /// False when nothing was pending — use <see cref="FadeOutTransitionByKey"/> for cold start (play directly in MainMenu without init SceneLoader load).
        /// </summary>
        public bool TryCompleteDeferredLoadingTransition()
        {
            if (!_hasDeferredCompletion || _pendingFadeOutTransition == null)
                return false;

            SceneTransition transition = _pendingFadeOutTransition;
            string name = _pendingSceneName;
            int index = _pendingSceneIndex;
            _pendingFadeOutTransition = null;
            _hasDeferredCompletion = false;

            transition.FadeOut();
            OnSceneLoad?.Invoke(name, index);
            return true;
        }

        /// <summary>
        /// FadeOut a transition from <see cref="SceneTransitionDatabase"/> by key (e.g. same key as <see cref="SceneLoaderStartup"/>).
        /// Use when <see cref="TryCompleteDeferredLoadingTransition"/> returned false but loading UI is still visible (no SceneLoader load ran this session).
        /// </summary>
        public static void FadeOutTransitionByKey(string transitionKey)
        {
            if (string.IsNullOrEmpty(transitionKey) || SceneTransitionDatabase.Instance == null)
                return;

            SceneTransition t = SceneTransitionDatabase.Instance.Transitions.GetValue(transitionKey);
            t?.FadeOut();
        }

        // Coroutine for loading the scene asynchronously
        private IEnumerator LoadSceneAsync(string sceneName, int sceneIndex, SceneTransition sceneLoadTransition,
            bool deferFadeOutUntilManualComplete = false)
        {
            if (_hasDeferredCompletion)
            {
                _pendingFadeOutTransition = null;
                _hasDeferredCompletion = false;
            }

            // Activate loading UI
            sceneLoadTransition.FadeIn();

            float delay = sceneLoadTransition.GetStartDelay();

            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }

            // Start loading the scene asynchronously
            AsyncOperation operation;
            if (sceneIndex >= 0)
            {
                operation = SceneManager.LoadSceneAsync(sceneIndex);
            }
            else
            {
                operation = SceneManager.LoadSceneAsync(sceneName);
            }

            // Prevent the scene from activating immediately when loading is complete
            operation.allowSceneActivation = false;

            // Update the UI based on loading progress
            while (!operation.isDone)
            {
                // Check if the scene has finished loading (progress >= 0.9f)
                if (operation.progress >= 0.9f)
                {
                    operation.allowSceneActivation = true;
                }

                yield return null;  // Wait for the next frame
            }

            if (!deferFadeOutUntilManualComplete)
            {
                sceneLoadTransition.FadeOut();
                OnSceneLoad?.Invoke(sceneName, sceneIndex);
            }
            else
            {
                _pendingFadeOutTransition = sceneLoadTransition;
                _pendingSceneName = sceneName;
                _pendingSceneIndex = sceneIndex;
                _hasDeferredCompletion = true;
            }
        }

        public bool IsLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }
        public bool IsLoaded(int sceneIndex)
        {
            Scene scene = SceneManager.GetSceneByBuildIndex(sceneIndex);
            return scene.IsValid() && scene.isLoaded;
        }
    }
}
