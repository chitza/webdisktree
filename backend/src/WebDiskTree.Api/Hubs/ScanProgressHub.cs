using Microsoft.AspNetCore.SignalR;

namespace WebDiskTree.Api.Hubs;

public class ScanProgressHub : Hub
{
    public Task JoinScanGroup(Guid scanId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(scanId));

    public Task LeaveScanGroup(Guid scanId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(scanId));

    public static string GroupName(Guid scanId) => $"scan-{scanId}";
}
