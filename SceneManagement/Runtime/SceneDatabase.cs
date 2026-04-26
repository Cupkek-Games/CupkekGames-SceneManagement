#if UNITY_ADDRESSABLES
using CupkekGames.Core;

namespace CupkekGames.Systems
{
    public class SceneDatabase : KeyValueDatabaseMonoSO<string, SceneSO>
    {
        private static SceneDatabase _instance;

        public static SceneDatabase Instance
        {
            get
            {
                return _instance;
            }
        }

        protected override void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject); // Optional: keep this instance across scenes
            }
            else
            {
                Destroy(gameObject); // Destroy duplicate instances
                return;
            }

            base.Awake();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
#endif
