using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace SaveLoadSystem
{
    public class GameDedicatedRepository : IGameDedicatedRepository
    {
        private const string SAVE_TIME_KEY = "SaveTime";
        private static readonly DateTime originalTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly GameClient _gameClient;
        private readonly string _filePath;

        public GameDedicatedRepository(GameClient gameClient, string filePath)
        {
            _gameClient = gameClient;
            _filePath = filePath;
        }

        public async UniTask Save(Dictionary<string, string> saveData)
        {
            TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - originalTime;
            string saveTime = timeSpan.TotalSeconds.ToString("F0");

            saveData[SAVE_TIME_KEY] = saveTime;

            string content = JsonConvert.SerializeObject(saveData);
            await File.WriteAllTextAsync(_filePath, content);
            await _gameClient.Save(content);
        }

        public async UniTask<Dictionary<string, string>> Load()
        {
            //Get local data
            long localSaveTime = -1;
            Dictionary<string, string> localData;

            if (File.Exists(_filePath))
            {
                string content = await File.ReadAllTextAsync(_filePath);
                if (content == null)
                {
                    localData = new Dictionary<string, string>();
                }
                else
                {
                    localData = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    localSaveTime = long.Parse(localData[SAVE_TIME_KEY]);
                }
            }
            else
            {
                localData = new Dictionary<string, string>();
            }

            //Get remote data

            long remoteSaveTime = -1;
            Dictionary<string, string> remoteData;

            var (success, remoteJson) = await _gameClient.Load();

            if (success)
            {
                remoteData = JsonConvert.DeserializeObject<Dictionary<string, string>>(remoteJson);
                remoteSaveTime = long.Parse(remoteData[SAVE_TIME_KEY]);
            }
            else
            {
                remoteData = new Dictionary<string, string>();
            }

            if (localSaveTime >= remoteSaveTime)
            {
                Debug.Log("Load local data");
                return localData;
            }
            else
            {
                Debug.Log("Load remote data");
                return remoteData;
            }
        }
     }
}