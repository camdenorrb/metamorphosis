using System;

namespace Metamorphosis.Objects
{
    public class CommitInfo
    {
        public long Id { get; set; }
        public long? ParentId { get; set; }
        public string Branch { get; set; } = "main";
        public string Hash { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public long TimestampTicks { get; set; }
        public string SnapshotFile { get; set; } = string.Empty;
        public int? ElementCount { get; set; }
        public string? ModelPath { get; set; }
        public string? RevitVersion { get; set; }
        public string? DocumentGuid { get; set; }
    }
}
