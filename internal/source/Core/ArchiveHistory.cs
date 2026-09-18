using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace murumsWiiModStudio
{
    internal sealed class ArchiveHistory
    {
        private sealed class State
        {
            public byte[] Data;
            public string Hash;
        }

        private readonly List<State> _states = new List<State>();
        private int _position = -1;
        private long _bytes;
        private string _savedHash;
        private readonly long _limit;
        public ArchiveHistory(long limit = 96L * 1024 * 1024)
        {
            _limit = limit;
        }

        public bool CanUndo
        {
            get
            {
                return _position > 0;
            }
        }

        public bool CanRedo
        {
            get
            {
                return _position >= 0 && _position < _states.Count - 1;
            }
        }

        public bool IsDirty
        {
            get
            {
                return _position >= 0 && _states[_position].Hash != _savedHash;
            }
        }

        private static string Hash(byte[] data)
        {
            using (SHA256 hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(data));
        }

        public void Reset(byte[] data)
        {
            _states.Clear();
            _position = -1;
            _bytes = 0;
            _savedHash = null;
            Record(data);
            MarkSaved();
        }

        public void MarkSaved()
        {
            _savedHash = _position < 0 ? null : _states[_position].Hash;
        }

        public void Record(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            string hash = Hash(data);
            if (_position >= 0 && _states[_position].Hash == hash)
                return;
            while (_states.Count > _position + 1)
            {
                _bytes -= _states[_states.Count - 1].Data.Length;
                _states.RemoveAt(_states.Count - 1);
            }

            _states.Add(new State { Data = (byte[])data.Clone(), Hash = hash });
            _bytes += data.Length;
            while (_states.Count > 1 && (_states.Count > 21 || _bytes > _limit))
            {
                _bytes -= _states[0].Data.Length;
                _states.RemoveAt(0);
            }

            _position = _states.Count - 1;
        }

        public byte[] Undo()
        {
            if (!CanUndo)
                return null;
            return (byte[])_states[--_position].Data.Clone();
        }

        public byte[] Redo()
        {
            if (!CanRedo)
                return null;
            return (byte[])_states[++_position].Data.Clone();
        }
    }
}
