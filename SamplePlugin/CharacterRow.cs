using System.Collections.Generic;

namespace SamplePlugin
{
    public enum BlockStatus
    {
        NotBlocked,
        MaybeBlocked,
        Blocked
    }

    public struct CharacterRow
    {
        public byte Party;
        public BlockStatus Blocked;
        public uint JobIcon;
        public ulong Id;
        public string? Name;
        public List<string>? oldNames;
    }
}
