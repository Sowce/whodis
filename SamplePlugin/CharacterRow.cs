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
        public uint JobIcon;
        public ulong Id;
        public string? Name;
        public List<string>? oldNames;
        public BlockStatus Blocked;
        public string? BlacklistNote;
    }
}
