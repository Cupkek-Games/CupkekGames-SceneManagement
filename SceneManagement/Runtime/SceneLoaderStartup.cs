using UnityEngine;
using CupkekGames.Luna;

namespace CupkekGames.SceneManagement
{
    /// <summary>
    /// Optional automatic load of a build-index scene on <c>Start</c> (e.g. from an initialization scene).
    /// For Addressables, add <c>SceneLoaderAddressableStartup</c> (same folder, requires **UNITY_ADDRESSABLES**).
    /// Place on the same GameObject as <see cref="SceneLoader"/> or anywhere; disable this component or
    /// <see cref="_loadOnStart"/> to skip automatic loading while keeping <see cref="SceneLoader"/> for manual loads.
    /// Use deferred fade-out to keep the transition up until <c>SceneLoader.CompleteDeferredLoadingTransition()</c> runs in the loaded scene.
    /// </summary>
    public class SceneLoaderStartup : MonoBehaviour
    {
        [SerializeField]
        private bool _loadOnStart = true;

        [SerializeField]
        private string _startSceneName = "";

        [Header("Index is used if name is empty")]
        [SerializeField]
        private int _startSceneIndex = 1;

        [SerializeField]
        private string _transitionKey = "Fade";

        [Tooltip("Keeps the loading transition visible until SceneLoader.CompleteDeferredLoadingTransition() (e.g. from a sequencer in the loaded scene).")]
        [SerializeField]
        private bool _deferFadeOutUntilManualComplete;

        private void Start()
        {
            if (!_loadOnStart)
                return;

            SceneTransition transition = SceneTransitionDatabase.Instance.Transitions.GetValue(_transitionKey);

            if (string.IsNullOrEmpty(_startSceneName))
                SceneLoader.Instance.LoadScene(_startSceneIndex, transition, _deferFadeOutUntilManualComplete);
            else
                SceneLoader.Instance.LoadScene(_startSceneName, transition, _deferFadeOutUntilManualComplete);
        }
    }
}
