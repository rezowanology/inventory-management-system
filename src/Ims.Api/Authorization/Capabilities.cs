using Ims.Api.Domain;

namespace Ims.Api.Authorization;

/// <summary>
/// SINGLE SOURCE OF TRUTH for the permission matrix. Program.cs registers one authorization policy per
/// capability; controllers use [Authorize(Policy = Capabilities.X)]; GET /api/auth/me returns the list so the
/// React app never has to re-encode the matrix.
/// </summary>
public static class Capabilities
{
    public const string Dashboard = "dashboard";
    public const string ChangePassword = "account.change-password";
    public const string OpsTrack = "ops.track";
    public const string OpsReceive = "ops.receive";
    public const string OpsDeliver = "ops.deliver";
    public const string OpsLookup = "ops.lookup";
    public const string HistoryView = "history.view";
    public const string HistoryReverse = "history.reverse";
    public const string ReportsView = "reports.view";
    public const string MasterDataManage = "masterdata.manage";
    public const string UsersManage = "users.manage";
    public const string AuditView = "audit.view";

    /// <summary>capability -> roles allowed.</summary>
    public static readonly IReadOnlyDictionary<string, Role[]> Matrix = new Dictionary<string, Role[]>
    {
        [Dashboard] = [Role.ADMIN, Role.COMPONENT_TRACKER, Role.RECEIVE_USER, Role.DELIVERY_USER, Role.REPORT_VIEWER],
        [ChangePassword] = [Role.ADMIN, Role.COMPONENT_TRACKER, Role.RECEIVE_USER, Role.DELIVERY_USER, Role.REPORT_VIEWER],
        [OpsTrack] = [Role.ADMIN, Role.COMPONENT_TRACKER],
        [OpsReceive] = [Role.ADMIN, Role.RECEIVE_USER],
        [OpsDeliver] = [Role.ADMIN, Role.DELIVERY_USER],
        [OpsLookup] = [Role.ADMIN, Role.COMPONENT_TRACKER, Role.RECEIVE_USER, Role.DELIVERY_USER],
        [HistoryView] = [Role.ADMIN, Role.REPORT_VIEWER],
        [HistoryReverse] = [Role.ADMIN],
        [ReportsView] = [Role.ADMIN, Role.REPORT_VIEWER],
        [MasterDataManage] = [Role.ADMIN],
        [UsersManage] = [Role.ADMIN],
        [AuditView] = [Role.ADMIN],
    };

    public static IEnumerable<string> ForRole(Role role) =>
        Matrix.Where(kv => kv.Value.Contains(role)).Select(kv => kv.Key);
}
