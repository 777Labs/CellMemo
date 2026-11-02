using System;
using System.Collections.Generic;

namespace CellMemo.Models
{
    public class TabManifestEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Memo1";
        public string? FilePath { get; set; }
        public bool IsDirty { get; set; }
        public string LineEnding { get; set; } = "CRLF";
        public string EncodingName { get; set; } = "UTF-8";
        public bool HasBom { get; set; }
    }

    public class SessionManifest
    {
        public int ActiveTabIndex { get; set; } = 0;
        public List<TabManifestEntry> Tabs { get; set; } = new();
    }

    public class TabSessionData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Memo1";
        public string? FilePath { get; set; }
        public bool IsDirty { get; set; }
        public string LineEnding { get; set; } = "CRLF";
        public string EncodingName { get; set; } = "UTF-8";
        public bool HasBom { get; set; }
        public int ActiveRow { get; set; } = 1;
        public int ActiveCol { get; set; } = 1;
        public int ScrollRow { get; set; } = 1;
        public int ScrollCol { get; set; } = 1;
        public Dictionary<string, string> Cells { get; set; } = new();
        public Dictionary<int, int> ColumnWidths { get; set; } = new();
        public Dictionary<int, int> RowHeights { get; set; } = new();
    }
}
