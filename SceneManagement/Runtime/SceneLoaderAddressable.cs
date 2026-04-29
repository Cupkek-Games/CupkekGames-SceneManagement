#if UNITY_ADDRESSABLES
using CupkekGames.EditorInspector;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using CupkekGames.Singletons;
using CupkekGames.Luna;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CupkekGames.SceneManagement
{
  /// <summary>
  /// Addressables scene loading API (singleton). Does not load any scene by itself — add
  /// <see cref="SceneLoaderAddressableStartup"/> for an automatic first load from the init scene, or
  /// <see cref="InitializationLoader"/> when you need persistent scenes loaded first.
  /// </summary>
  public class SceneLoaderAddressable : Singleton<SceneLoaderAddressable>
  {
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void InitializeEditorCleanup()
    {
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
      // Clean up when exiting playmode to prevent stale addressable handles
      if (state == PlayModeStateChange.ExitingPlayMode)
      {
        if (Instance != null)
        {
          Instance.Cleanup();
        }
      }
    }
#endif

    private void OnDestroy()
    {
      Cleanup();

#if UNITY_EDITOR
      EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
    }

    private void Cleanup()
    {
      // Stop any running coroutines
      StopAllCoroutines();

      // Clear all state to prevent addressable handle issues
      _loadedScenes.Clear();
      _sceneLoadRequests.Clear();
      _currentScreenTransition = null;
      _pendingManualFadeOutTransition = null;
      _activeScene = null;

      // Clear static events to prevent memory leaks
      SceneReadyEvent = null;
      SceneUnloadEvent = null;
    }

    private SceneTransition _currentScreenTransition = null;

    /// <summary>
    /// Set when a load request used <see cref="SceneLoadRequest.DeferFadeOutUntilManualComplete"/>.
    /// Cleared after <see cref="CompleteDeferredLoadingTransition"/>.
    /// </summary>
    private SceneTransition _pendingManualFadeOutTransition;

    //Parameters coming from scene loading requests
    private SceneSO _activeScene;

    [MultiLineHeader("For debug purposes only. Do not modify.")] [SerializeField]
    private List<SceneSO> _loadedScenes = new List<SceneSO>();

    public List<SceneSO> LoadedScenes => _loadedScenes;

    private List<SceneLoadRequest> _sceneLoadRequests = new List<SceneLoadRequest>();

    // Events
    public static Action<List<SceneSO>> SceneReadyEvent;
    public static Action<List<SceneSO>> SceneUnloadEvent;

    /// <summary>
    /// Starts the loading process
    /// </summary>
    private IEnumerator StartProcess()
    {
      // Debug.Log("SceneLoader: Starting loading process. " + _sceneLoadRequests.Count + " requests in queue.");
      SceneLoadRequest sceneLoadRequest = _sceneLoadRequests[0];

      sceneLoadRequest = ValidateRequest(sceneLoadRequest);

      if (sceneLoadRequest.ScenesLeftToLoad == 0 && sceneLoadRequest.ScenesLeftToUnload == 0)
      {
        // Debug.Log("SceneLoader: No valid scenes to load or unload. Ignoring request.");
        OnProcessCompleted();
        yield break;
      }

      // Old code
      // if (sceneLoadRequest.FadeDuration > 0)
      // {
      //   // Instant fade in if we are showing the loading screen
      //   if (sceneLoadRequest.ShowLoadingScreen)
      //   {
      //     _fadeRequestChannel.FadeIn(0);
      //   }
      //   else
      //   {
      //     _fadeRequestChannel.FadeIn(sceneLoadRequest.FadeDuration);
      //   }
      // }

      if (_currentScreenTransition == null && sceneLoadRequest.SceneLoadTransition != null)
      {
        _currentScreenTransition = sceneLoadRequest.SceneLoadTransition;

        _currentScreenTransition.FadeIn();

        float delay = _currentScreenTransition.GetStartDelay();

        if (delay > 0)
        {
          yield return new WaitForSeconds(delay);
        }
      }

      if (sceneLoadRequest.ScenesLeftToUnload > 0)
      {
        UnLoadScenes();
      }
      else
      {
        LoadNewScenes();
      }
    }

    private void UnLoadScenes()
    {
      // InternalDebug.Log("SceneLoader: Unloading scenes...");
      SceneLoadRequest sceneLoadRequest = _sceneLoadRequests[0];

      for (int i = 0; i < sceneLoadRequest.ScenesToUnload.Count; i++)
      {
        SceneSO gameSceneSO = sceneLoadRequest.ScenesToUnload[i];

        AsyncOperationHandle<SceneInstance> _unloadingOperationHandle = gameSceneSO.sceneReference.UnLoadScene();
        _unloadingOperationHandle.Completed += OnSceneUnLoaded;

        // InternalDebug.Log("SceneLoader: UnLoading scene: " + gameSceneSO.name);
      }
    }

    private void OnSceneUnLoaded(AsyncOperationHandle<SceneInstance> obj)
    {
      SceneLoadRequest sceneLoadRequest = _sceneLoadRequests[0];

      sceneLoadRequest.ScenesLeftToUnload--;

      // InternalDebug.Log("SceneLoader: Scene unloaded. " + sceneLoadRequest.ScenesLeftToUNNLoad + " scenes left to unload.");
      if (sceneLoadRequest.ScenesLeftToUnload == 0) // Last scene has finished unloading
      {
        // remove loaded scenes from the list of currently loaded scenes
        _loadedScenes = _loadedScenes.Except(sceneLoadRequest.ScenesToUnload).ToList();

        SceneUnloadEvent?.Invoke(sceneLoadRequest.ScenesToUnload); // Raise scene unload event

        sceneLoadRequest.ScenesToUnload = null;

        // load new scenes
        if (sceneLoadRequest.ScenesLeftToLoad > 0)
        {
          LoadNewScenes();
        }
        else
        {
          OnProcessCompleted();
        }
      }
    }

    /// <summary>
    /// Releases a stale handle from AssetReference and retries loading the scene
    /// </summary>
    private void ReleaseStaleHandleAndRetryLoad(SceneSO gameSceneSO)
    {
      try
      {
        // Release the stale handle from AssetReference
        gameSceneSO.sceneReference.ReleaseAsset();

        // Retry loading the scene
        AsyncOperationHandle<SceneInstance> _loadingOperationHandle =
          gameSceneSO.sceneReference.LoadSceneAsync(LoadSceneMode.Additive, true);

        if (_loadingOperationHandle.IsValid())
        {
          if (_activeScene != null && _activeScene == gameSceneSO)
          {
            _loadingOperationHandle.Completed += OnActiveSceneLoaded;
          }
          else
          {
            _loadingOperationHandle.Completed += OnSceneLoaded;
          }
        }
        else
        {
          Debug.LogError(
            $"Scene {gameSceneSO.name} handle is invalid after release and retry. This should not happen.");
          OnSceneLoaded(default);
        }
      }
      catch (System.Exception ex)
      {
        Debug.LogError($"Failed to load scene {gameSceneSO.name} after releasing stale handle: {ex.Message}");
        OnSceneLoaded(default);
      }
    }

    /// <summary>
    /// Kicks off the asynchronous loading of a scene, either menu or Location.
    /// </summary>
    private void LoadNewScenes()
    {
      // InternalDebug.Log("SceneLoader: Loading scenes...");
      SceneLoadRequest sceneLoadRequest = _sceneLoadRequests[0];

      int index = sceneLoadRequest.GetNextSceneToLoadIndex();
      SceneSO gameSceneSO = sceneLoadRequest.ScenesToLoad[index];

      // Debug.Log("SceneLoader: Loading scene: " + gameSceneSO.name);

      if (!_loadedScenes.Contains(gameSceneSO))
      {
        Debug.Log("Loading Scene: " + gameSceneSO.name);

        if (!gameSceneSO.sceneReference.RuntimeKeyIsValid())
        {
          throw new System.Exception("Scene Reference RuntimeKeyIs NOT Valid: " + gameSceneSO.name);
        }

        try
        {
          AsyncOperationHandle<SceneInstance> _loadingOperationHandle =
            gameSceneSO.sceneReference.LoadSceneAsync(LoadSceneMode.Additive, true);

          // Check if handle is valid before using it
          if (_loadingOperationHandle.IsValid())
          {
            if (_activeScene != null && _activeScene == gameSceneSO)
            {
              _loadingOperationHandle.Completed += OnActiveSceneLoaded;
            }
            else
            {
              _loadingOperationHandle.Completed += OnSceneLoaded;
            }
          }
          else
          {
            // Handle is invalid - likely a stale handle from editor play mode cycling
            // Release the stale handle and retry loading
            Debug.LogWarning(
              $"Scene {gameSceneSO.name} handle is invalid (stale handle). Releasing and retrying load. This can happen when stopping/playing in editor.");
            ReleaseStaleHandleAndRetryLoad(gameSceneSO);
          }
        }
        catch (System.Exception)
        {
          // If scene is already loaded, Addressables will throw an exception
          // This can happen when stopping/playing in editor due to stale handles
          // Release the stale handle and retry loading
          Debug.LogWarning(
            $"Scene {gameSceneSO.name} appears already loaded in Addressables (stale handle). Releasing and retrying load. This can happen when stopping/playing in editor.");
          ReleaseStaleHandleAndRetryLoad(gameSceneSO);
        }
      }
      else
      {
        // Debug.Log("SceneLoader: Scene already loaded: " + gameSceneSO.name);
        OnSceneLoaded(default);
      }
    }

    private void OnActiveSceneLoaded(AsyncOperationHandle<SceneInstance> obj)
    {
      Scene s = obj.Result.Scene;
      SceneManager.SetActiveScene(s);

      OnSceneLoaded(obj);
    }

    private void OnSceneLoaded(AsyncOperationHandle<SceneInstance> obj)
    {
      SceneLoadRequest sceneLoadRequest = _sceneLoadRequests[0];

      sceneLoadRequest.ScenesLeftToLoad--;

      // Debug.Log("SceneLoader: Scene loaded. " + sceneLoadRequest.ScenesLeftToLoad + " scenes left to load.");
      if (sceneLoadRequest.ScenesLeftToLoad == 0) // Last scene has finished loading
      {
        // After all scenes are loaded, save loaded scenes to the list of currently loaded scenes
        OnProcessCompleted();
      }
      else
      {
        LoadNewScenes();
      }
    }

    // To prevent a new loading request while already loading a new scene
    private bool IsLoading()
    {
      return _sceneLoadRequests.Count > 0;
    }

    private void OnProcessCompleted()
    {
      SceneLoadRequest sceneLoadRequest = _sceneLoadRequests[0];

      if (sceneLoadRequest.ScenesToLoad != null)
      {
        // LightProbes.TetrahedralizeAsync();

        _loadedScenes.AddRange(sceneLoadRequest.ScenesToLoad);
        SceneReadyEvent?.Invoke(sceneLoadRequest.ScenesToLoad); // Raise scene ready event
      }

      // Debug.Log("SceneLoader: Loading process completed. " + _sceneLoadRequests.Count + " requests in queue.");

      _sceneLoadRequests.RemoveAt(0);
      if (_sceneLoadRequests.Count > 0)
      {
        StartCoroutine(StartProcess());
      }
      else // Last scene has finished loading
      {
        Resources.UnloadUnusedAssets(); // Unload unused assets

        if (_currentScreenTransition != null) // Loading screen is shown
        {
          if (sceneLoadRequest.DeferFadeOutUntilManualComplete)
          {
            _pendingManualFadeOutTransition = _currentScreenTransition;
          }
          else
          {
            _currentScreenTransition.FadeOut();
          }

          _currentScreenTransition = null;
        }
      }
    }

    /// <summary>
    /// Ends the loading presentation after a deferred load (see <see cref="SceneLoadRequest.DeferFadeOutUntilManualComplete"/>).
    /// Safe to call when nothing is pending (no-op).
    /// </summary>
    public void CompleteDeferredLoadingTransition()
    {
      TryCompleteDeferredLoadingTransition();
    }

    /// <summary>
    /// Returns true if a deferred fade was pending and FadeOut was applied.
    /// </summary>
    public bool TryCompleteDeferredLoadingTransition()
    {
      if (_pendingManualFadeOutTransition == null)
        return false;

      _pendingManualFadeOutTransition.FadeOut();
      _pendingManualFadeOutTransition = null;
      return true;
    }

    // Public methods

    /// <summary>
    /// UnLoads the scenes in the list before loading the scenes in the list, additive and async. 
    /// </summary>
    public void LoadSceneRequest(List<SceneSO> scenesToLoad, List<SceneSO> scenesToUNNLoad,
      SceneTransition sceneLoadTransitionType, bool deferFadeOutUntilManualComplete = false)
    {
      //Prevent a double-loading, for situations where the player falls in two Exit colliders in one frame
      // if (IsLoading())
      // {
      //   Debug.Log("SceneLoader: Already loading a scene. Ignoring request.");
      //   return;
      // }

      SceneLoadRequest sceneLoadRequest =
        new SceneLoadRequest(scenesToLoad, scenesToUNNLoad, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
      _sceneLoadRequests.Add(sceneLoadRequest);

      // Debug.Log("SceneLoader: Loading scene request added. " + _sceneLoadRequests.Count + " requests in queue.");

      // if (sceneLoadRequest.ScenesToLoad != null)
      // {
      //   for (int j = 0; j < sceneLoadRequest.ScenesToLoad.Count; j++)
      //   {
      //     Debug.Log("SceneLoader: Loading scene: " + sceneLoadRequest.ScenesToLoad[j].name);
      //   }
      // }
      // if (sceneLoadRequest.ScenesToUNNLoad != null)
      // {
      //   for (int j = 0; j < sceneLoadRequest.ScenesToUNNLoad.Count; j++)
      //   {
      //     Debug.Log("SceneLoader: UnLoading scene: " + sceneLoadRequest.ScenesToUNNLoad[j].name);
      //   }
      // }

      if (_sceneLoadRequests.Count == 1)
      {
        StartCoroutine(StartProcess());
      }
    }

    private SceneLoadRequest ValidateRequest(SceneLoadRequest sceneLoadRequest)
    {
      if (sceneLoadRequest.ScenesToLoad != null)
      {
        // Remove scenes that are already loaded
        // List<GameSceneSO> loadedScenesAfterRequest = _loadedScenes.Except(sceneLoadRequest.ScenesToUNNLoad).ToList();
        sceneLoadRequest.ScenesToLoad = sceneLoadRequest.ScenesToLoad.Except(_loadedScenes).ToList();
        sceneLoadRequest.ScenesLeftToLoad = sceneLoadRequest.ScenesToLoad.Count;
      }

      if (sceneLoadRequest.ScenesToUnload != null)
      {
        // Remove scenes that are not loaded
        sceneLoadRequest.ScenesToUnload = sceneLoadRequest.ScenesToUnload.Intersect(_loadedScenes).ToList();
        sceneLoadRequest.ScenesLeftToUnload = sceneLoadRequest.ScenesToUnload.Count;
      }

      // if (sceneLoadRequest.ScenesLeftToLoad == 0 && sceneLoadRequest.ScenesLeftToUNNLoad == 0)
      // {
      //   return sceneLoadRequest;
      // }

      return sceneLoadRequest;
    }

    public void LoadScene(List<SceneSO> scenesToLoad, SceneTransition sceneLoadTransitionType,
      bool deferFadeOutUntilManualComplete = false)
    {
      LoadSceneRequest(scenesToLoad, null, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
    }

    public void LoadScene(SceneSO sceneToLoad, SceneTransition sceneLoadTransitionType,
      bool deferFadeOutUntilManualComplete = false)
    {
      List<SceneSO> scenesToLoad = new()
      {
        sceneToLoad
      };
      LoadSceneRequest(scenesToLoad, null, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
    }

    public void LoadScene(SceneSO sceneToLoad)
    {
      List<SceneSO> scenesToLoad = new()
      {
        sceneToLoad
      };
      LoadSceneRequest(scenesToLoad, null, null);
    }

    public void UnLoadScene(List<SceneSO> scenesToUNNLoad, SceneTransition sceneLoadTransitionType,
      bool deferFadeOutUntilManualComplete = false)
    {
      LoadSceneRequest(null, scenesToUNNLoad, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
    }

    public void UnLoadScene(SceneSO scenesToUNNLoad, SceneTransition sceneLoadTransitionType,
      bool deferFadeOutUntilManualComplete = false)
    {
      List<SceneSO> scenesToUnLoad = new List<SceneSO>();
      scenesToUnLoad.Add(scenesToUNNLoad);
      LoadSceneRequest(null, scenesToUnLoad, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
    }

    public void UnLoadScene(SceneSO scenesToUNNLoad)
    {
      // Debug.Log("UnLoadScene: " + scenesToUNNLoad.name + "");
      List<SceneSO> scenesToUnLoad = new()
      {
        scenesToUNNLoad
      };
      LoadSceneRequest(null, scenesToUnLoad, null);
    }

    public void LoadSceneAndUnLoadCurrent(SceneSO sceneToLoad, SceneTransition sceneLoadTransitionType,
      bool deferFadeOutUntilManualComplete = false)
    {
      List<SceneSO> scenesToLoad = new List<SceneSO>();
      scenesToLoad.Add(sceneToLoad);
      List<SceneSO> scenesToUnLoad = new List<SceneSO>();
      scenesToUnLoad.AddRange(_loadedScenes);

      Debug.Log(_loadedScenes.Count);

      scenesToUnLoad = scenesToUnLoad.Except(scenesToLoad).ToList();
      LoadSceneRequest(scenesToLoad, scenesToUnLoad, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
    }

    public void LoadSceneAndUnLoadCurrent(List<SceneSO> scenesToLoad, SceneTransition sceneLoadTransitionType,
      bool deferFadeOutUntilManualComplete = false)
    {
      List<SceneSO> scenesToUnLoad = new List<SceneSO>();
      scenesToUnLoad.AddRange(_loadedScenes);

      scenesToUnLoad = scenesToUnLoad.Except(scenesToLoad).ToList();
      LoadSceneRequest(scenesToLoad, scenesToUnLoad, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
    }

    public void UnloadAllCurrent(SceneTransition sceneLoadTransitionType, bool deferFadeOutUntilManualComplete = false)
    {
      List<SceneSO> scenesToUnLoad = new List<SceneSO>();
      scenesToUnLoad.AddRange(_loadedScenes);
      LoadSceneRequest(null, scenesToUnLoad, sceneLoadTransitionType, deferFadeOutUntilManualComplete);
    }

    // Use this to set the active scene, must be called before loading the active scene
    // This is needed because the active scene is set after the scene is loaded
    public void SetActiveScene(SceneSO scene)
    {
      _activeScene = scene;
    }

    public bool IsLoaded(SceneSO gameSceneSO)
    {
      return _loadedScenes.Contains(gameSceneSO);
    }
  }
}
#endif