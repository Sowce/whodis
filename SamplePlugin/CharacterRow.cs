using System.Collections.Generic;

namespace SamplePlugin
{
    public struct CharacterRow
    {
        public byte Party;
        public bool Blocked;
        public uint JobIcon;
        public ulong Id;
        public string? Name;
        public List<string>? oldNames;
    }
}
