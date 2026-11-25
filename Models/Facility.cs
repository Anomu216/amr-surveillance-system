namespace AMRSurveillance.Models;

public class Facility
{
    //Primary key: Facility ID (e.g DFM, BSA, LAM e.t.c)
    public string FacilityID { get; set; }= string.Empty;

    public string FacilityName { get; set; } = string.Empty;

    public string County { get; set; } = string.Empty;

    public string SubCounty { get; set; } = string.Empty;

    public string FacilityLevel { get; set; } = string.Empty;

    public string Ownership { get; set; } = string.Empty;

    //Navigation Property where a facility can have many patients
    public ICollection<Patient> Patients { get; set; } = new List<Patient>();
}
