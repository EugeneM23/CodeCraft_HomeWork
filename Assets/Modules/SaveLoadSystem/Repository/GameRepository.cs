using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace SaveLoadSystem
{
    public class GameRepository : IGameRepository
    {
        private readonly string _filePath;
        private readonly string _aesPassword;
        private readonly byte[] _aesSalt;

        public GameRepository(string filePath, string aesPassword, byte[] aesSalt)
        {
            _filePath = filePath;
            _aesPassword = aesPassword;
            _aesSalt = aesSalt;
        }

        public Dictionary<string, string> LoadData()
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, string>();

            byte[] content = File.ReadAllBytes(_filePath);
            //content = AesEncryptor.Decrypt(content, _aesPassword, _aesSalt);

            string json = Encoding.UTF8.GetString(content);
            Dictionary<string, string> result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            if (result == null)
                return new Dictionary<string, string>();

            return result;
        }

        public void SaveData(Dictionary<string, string> data)
        {
            string dataText = JsonConvert.SerializeObject(data, Formatting.Indented);
            byte[] bytes = Encoding.UTF8.GetBytes(dataText);
            //bytes = AesEncryptor.Encrypt(bytes, _aesPassword, _aesSalt);

            File.WriteAllBytes(_filePath, bytes);
        }
    }
}