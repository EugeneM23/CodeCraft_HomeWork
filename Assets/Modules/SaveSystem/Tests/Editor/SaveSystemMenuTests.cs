using System.IO;
using NUnit.Framework;
using Saving.Editor;
using UnityEngine;

namespace Saving.Tests
{
    public class SaveSystemMenuTests
    {
        [Test]
        public void SaveSystemMenu_SavesFolder_IsDefaultFolderUnderPersistentDataPath() =>
            Assert.AreEqual(Path.Combine(Application.persistentDataPath, SaveLoadSystem.DEFAULT_FOLDER),
                SaveSystemMenu.SavesFolder);
    }
}
