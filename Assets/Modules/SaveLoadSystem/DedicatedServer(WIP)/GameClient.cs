using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace SaveLoadSystem
{
    public class GameClient
    {
        private readonly string _uri;

        public GameClient(string uri)
        {
            _uri = uri;
        }

        public async UniTask<bool> Save(string json)
        {
            UnityWebRequest request = UnityWebRequest.Put(_uri + "save", json);

            await request.SendWebRequest();

            return request.result == UnityWebRequest.Result.Success;
        }

        public async UniTask<(bool, string)> Load()
        {
            UnityWebRequest request = UnityWebRequest.Get(_uri + "load");
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                return (false, null);
            
             string json = request.downloadHandler.text;
 
            return json == null ? (false, null) : (true, json);
        }
    }
}