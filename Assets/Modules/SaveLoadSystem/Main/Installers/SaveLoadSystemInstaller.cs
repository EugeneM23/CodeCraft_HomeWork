using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace SaveLoadSystem
{
    [CreateAssetMenu(fileName = "SaveLoadSystemInstaller", menuName = "Zenject/App/SaveLoadSystemInstaller")]
    public class SaveLoadSystemInstaller : ScriptableObjectInstaller
    {
        [Header("Save/Load Settings")] [SerializeField]
        private bool _autoSave;

        [SerializeField] private float _savePeriod = 30f;

        [Header("Repository Settings")] [SerializeField]
        private string _fileName;

        [SerializeField] private string _aesPassword;

        [ShowInInspector] private readonly byte[] _aesSolt =
        {
            0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64,
            0x76, 0x65, 0x64, 0x65, 0x76, 0x20, 0x53, 0x61,
            0x6c, 0x74, 0x20, 0x4b, 0x65, 0x79, 0x20, 0x32,
            0x30, 0x32, 0x35, 0x20, 0x76, 0x31, 0x2e, 0x30
        };

        private string _uri = "http://127.0.0.1:8080/";

        public override void InstallBindings()
        {
            string path = Application.streamingAssetsPath + "/" + _fileName;

            Container
                .BindInterfacesAndSelfTo<GameRepository>()
                .AsSingle()
                .WithArguments(path, _aesPassword, _aesSolt);

            Container
                .Bind<ApplicationEvents>()
                .FromNewComponentOnRoot()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<SaveLoadService>()
                .AsSingle();
            
            if (_autoSave)
            {
                Container
                    .BindInterfacesAndSelfTo<GameSaveController>()
                    .AsSingle()
                    .WithArguments(_savePeriod)
                    .NonLazy();
            }
        }
    }
}