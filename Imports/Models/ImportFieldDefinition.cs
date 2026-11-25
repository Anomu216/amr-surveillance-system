namespace AMRSurveillance.Imports.Models;

public class ImportFieldDefinition
{
    public string Name { get; set; } = string.Empty;

    public ImportFieldType Type { get; set; }

    public string? LookupName { get; set; }

    public bool Required { get; set; }
}