using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Saving
{
    /// <summary>
    /// Encrypts a slot's text with AES-256-CBC and signs it with HMAC-SHA256 (encrypt-then-MAC), keys derived from the
    /// password with PBKDF2. Keeps players from reading or editing the file by hand; the password ships with the game,
    /// so it is no protection against someone who takes the build apart. Another password or a changed byte throws.
    /// Layout: IV (16) + cipher + MAC (32).
    /// </summary>
    public sealed class AesSaveTransform : ISaveTransform
    {
        private const int KEY_SIZE = 32;
        private const int IV_SIZE = 16;
        private const int MAC_SIZE = 32;
        private const int ITERATIONS = 10000;

        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("Saving.AesSaveTransform");

        private readonly byte[] _encryptionKey;
        private readonly byte[] _macKey;

        public AesSaveTransform(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("The password must not be empty.", nameof(password));

            using var derive = new Rfc2898DeriveBytes(password, Salt, ITERATIONS, HashAlgorithmName.SHA256);
            _encryptionKey = derive.GetBytes(KEY_SIZE);
            _macKey = derive.GetBytes(KEY_SIZE);
        }

        public string Id => "aes";

        public byte[] Encode(byte[] data)
        {
            using Aes aes = CreateAes();
            aes.GenerateIV();
            byte[] cipher;
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
                cipher = encryptor.TransformFinalBlock(data, 0, data.Length);

            var result = new byte[IV_SIZE + cipher.Length + MAC_SIZE];
            Buffer.BlockCopy(aes.IV, 0, result, 0, IV_SIZE);
            Buffer.BlockCopy(cipher, 0, result, IV_SIZE, cipher.Length);
            byte[] mac = Sign(result, IV_SIZE + cipher.Length);
            Buffer.BlockCopy(mac, 0, result, IV_SIZE + cipher.Length, MAC_SIZE);
            return result;
        }

        public byte[] Decode(byte[] data)
        {
            if (data == null || data.Length < IV_SIZE + MAC_SIZE + 16)
                throw new InvalidDataException("The encrypted save is too short.");

            int signedLength = data.Length - MAC_SIZE;
            byte[] expected = Sign(data, signedLength);
            int difference = 0;
            for (int index = 0; index < MAC_SIZE; index++)
                difference |= expected[index] ^ data[signedLength + index];

            if (difference != 0)
                throw new InvalidDataException("The encrypted save does not match its password or was changed.");

            using Aes aes = CreateAes();
            var iv = new byte[IV_SIZE];
            Buffer.BlockCopy(data, 0, iv, 0, IV_SIZE);
            aes.IV = iv;
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, IV_SIZE, signedLength - IV_SIZE);
        }

        private Aes CreateAes()
        {
            Aes aes = Aes.Create();
            aes.KeySize = KEY_SIZE * 8;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _encryptionKey;
            return aes;
        }

        private byte[] Sign(byte[] data, int length)
        {
            using var hmac = new HMACSHA256(_macKey);
            return hmac.ComputeHash(data, 0, length);
        }
    }
}
