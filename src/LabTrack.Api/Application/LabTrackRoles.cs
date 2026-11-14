namespace LabTrack.Api.Application;

public static class LabTrackRoles
{
    public const string Administrator = "LabAdmin";
    public const string Technician = "LabTechnician";
    public const string StaffPolicy = "LabStaff";
    public const string AdministratorPolicy = "LabAdministrators";

    public static readonly string[] All = [Administrator, Technician];
    public static readonly string[] Staff = [Administrator, Technician];
}
