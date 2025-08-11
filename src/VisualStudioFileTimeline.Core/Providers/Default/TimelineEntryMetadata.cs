using System.Text.Json.Serialization;

namespace VisualStudioFileTimeline.Providers.Default;

public record FileTimelineMetadata(int Version, string Resource, Dictionary<string, FileTimelineMetadataEntryInfo> Entries);

public record FileTimelineMetadataEntryInfo([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] string? Source)
{
    public long Timestamp { get; set; }
};
