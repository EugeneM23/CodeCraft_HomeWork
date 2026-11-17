using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Saving
{
    /// <summary>
    /// A storage that runs every file through steps (GZipSaveTransform, AesSaveTransform) before the inner one: the
    /// written text is MARKER + "gzip+aes:" + Base64. Text without the marker is read as it is, so plain saves from
    /// before still load (<see cref="AcceptPlainText"/> false refuses them). Readers (<see cref="AddReader"/>) decode
    /// steps that are no longer written, so turning compression or encryption off keeps old saves readable. Text that
    /// the steps cannot read — another key, a missing step, a changed byte — throws InvalidDataException: the load
    /// fails and leaves the file alone instead of calling it corrupt.
    /// </summary>
    public sealed class TransformSaveStorage : ISaveStorage
    {
        public const string MARKER = "#save:";

        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly ISaveStorage _inner;
        private readonly ISaveTransform[] _steps;
        private readonly List<ISaveTransform> _readers = new();
        private readonly string _header;

        public TransformSaveStorage(ISaveStorage inner, params ISaveTransform[] steps)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _steps = steps?.Where(step => step != null).ToArray() ?? Array.Empty<ISaveTransform>();
            foreach (ISaveTransform step in _steps)
                ValidateId(step);

            _header = MARKER + string.Join("+", _steps.Select(step => step.Id)) + ":";
        }

        /// <summary>
        /// Plain text (no marker) is read as it is: saves from before the steps were turned on. False makes it an
        /// error, so a hand-edited plain file does not load.
        /// </summary>
        public bool AcceptPlainText { get; set; } = true;

        /// <summary>A step that is only read, never written: one that was turned off.</summary>
        public void AddReader(ISaveTransform reader)
        {
            ValidateId(reader ?? throw new ArgumentNullException(nameof(reader)));
            _readers.Add(reader);
        }

        public IReadOnlyList<int> ListSlots() => _inner.ListSlots();

        public bool TryRead(int slot, string file, out string text)
        {
            if (!_inner.TryRead(slot, file, out string raw))
            {
                text = null;
                return false;
            }

            text = Decode(raw);
            return true;
        }

        public bool TryReadBackup(int slot, string file, out string text)
        {
            if (!_inner.TryReadBackup(slot, file, out string raw))
            {
                text = null;
                return false;
            }

            text = Decode(raw);
            return true;
        }

        public void Write(int slot, string file, string text) => _inner.Write(slot, file, Encode(text));
        public void Quarantine(int slot, string file, bool backup) => _inner.Quarantine(slot, file, backup);
        public void DeleteSlot(int slot) => _inner.DeleteSlot(slot);

        public string Encode(string text)
        {
            if (_steps.Length == 0)
                return text;

            byte[] data = Utf8.GetBytes(text ?? string.Empty);
            foreach (ISaveTransform step in _steps)
                data = step.Encode(data);

            return _header + Convert.ToBase64String(data);
        }

        public string Decode(string raw)
        {
            if (raw == null || !raw.StartsWith(MARKER, StringComparison.Ordinal))
            {
                if (raw != null && !AcceptPlainText)
                    throw new InvalidDataException("The save text is plain, and this storage reads only its own steps.");

                return raw;
            }

            int colon = raw.IndexOf(':', MARKER.Length);
            if (colon < 0)
                throw new InvalidDataException("The save text has a broken transform header.");

            string[] ids = raw.Substring(MARKER.Length, colon - MARKER.Length)
                .Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                byte[] data = Convert.FromBase64String(raw.Substring(colon + 1));
                for (int index = ids.Length - 1; index >= 0; index--)
                    data = StepOf(ids[index]).Decode(data);

                return Utf8.GetString(data);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException($"The save text could not be decoded: {exception.Message}", exception);
            }
        }

        private ISaveTransform StepOf(string id) =>
            _steps.FirstOrDefault(step => step.Id == id) ?? _readers.FirstOrDefault(reader => reader.Id == id) ??
            throw new InvalidDataException($"The save was written with the '{id}' step, which this storage does not have.");

        private static void ValidateId(ISaveTransform step)
        {
            if (string.IsNullOrEmpty(step.Id) || !step.Id.All(char.IsLetterOrDigit))
                throw new ArgumentException($"A transform id must be letters and digits: '{step.Id}'.", nameof(step));
        }
    }
}
