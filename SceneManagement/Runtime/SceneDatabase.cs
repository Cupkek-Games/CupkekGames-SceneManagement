#if UNITY_ADDRESSABLES
using CupkekGames.KeyValueDatabases;

namespace CupkekGames.SceneManagement
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
                // Roots only — a child rides on its (already persistent) root's
                // lifetime; DDOL on a child is a warning no-op.
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
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
