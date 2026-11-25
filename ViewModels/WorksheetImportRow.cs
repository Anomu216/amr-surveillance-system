namespace AMRSurveillance.ViewModels;

public class WorksheetImportRow
{
    public string IsolateID { get; set; } = string.Empty;

    public string PatientID { get; set; } = string.Empty;

    public string CultureResult { get; set; } = string.Empty;

    public string? ManuscriptID { get; set; }

    public string? AcceptedID { get; set; }

    public string? WHONETID { get; set; }

    public string? GramReaction { get; set; }

    public string? Morphology { get; set; }

    public string? Hemolysis { get; set; }

    public string? Catalase { get; set; }

    public string? Oxidase { get; set; }

    public string? Coagulase { get; set; }

    public string? LactoseFermentation { get; set; }

    public string? Comments { get; set; }

    public string? ASTCard { get; set; }

    public string? ESBL { get; set; }

    public string? CefoxitinScreen { get; set; }

    public string? InducibleClindamycinResistance { get; set; }

    public string? ASTComments { get; set; }

    public Dictionary<string, string?> MICResults { get; set; }
        = new();

    public Dictionary<string, string?> InterpretationResults { get; set; }
        = new();
}