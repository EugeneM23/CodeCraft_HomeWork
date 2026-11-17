using System.Collections.Generic;

namespace Saving
{
    /// <summary>
    /// Where slots live. The boundary is a file's text, so the disk, a platform's save API or memory plug in the
    /// same way. Write is atomic and keeps the previous version as &lt;file&gt;.bak.
    /// </summary>
    public interface ISaveStorage
    {
        IReadOnlyList<int> ListSlots();                          // slots that have any file, ascending
        bool TryRead(int slot, string file, out string text);    // false when the file is missing
        bool TryReadBackup(int slot, string file, out string text);
        void Write(int slot, string file, string text);
        void Quarantine(int slot, string file, bool backup);     // <file> or <file>.bak -> *.corrupt
        void DeleteSlot(int slot);
    }
}
