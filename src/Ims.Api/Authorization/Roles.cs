using Ims.Api.Domain;

namespace Ims.Api.Authorization;

/// <summary>Role names as strings (for claims / [Authorize]) built from the Role enum.</summary>
public static class Roles
{
    public static readonly string Admin = nameof(Role.ADMIN);
    public static readonly string ComponentTracker = nameof(Role.COMPONENT_TRACKER);
    public static readonly string ReceiveUser = nameof(Role.RECEIVE_USER);
    public static readonly string DeliveryUser = nameof(Role.DELIVERY_USER);
    public static readonly string ReportViewer = nameof(Role.REPORT_VIEWER);
}
