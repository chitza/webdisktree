using Microsoft.AspNetCore.SignalR;
using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Api.Hubs;

/// <summary>Bridges the transport-agnostic <see cref="IScanProgressReporter"/> contract (implemented here, not
/// in Infrastructure, so the scan engine/background service never depend on SignalR directly) to the hub.</summary>
public class SignalRScanProgressReporter(IHubContext<ScanProgressHub> hubContext) : IScanProgressReporter
{
    public void ReportProgress(Guid scanId, long filesScanned, long dirsScanned, long bytesScanned, string currentPath)
    {
        _ = hubContext.Clients.Group(ScanProgressHub.GroupName(scanId)).SendAsync("ScanProgress", new
        {
            scanId,
            filesScanned,
            dirsScanned,
            bytesScanned,
            currentPath,
        });
    }

    public void ReportCompleted(Guid scanId)
    {
        _ = hubContext.Clients.Group(ScanProgressHub.GroupName(scanId)).SendAsync("ScanCompleted", new { scanId });
    }

    public void ReportFailed(Guid scanId, string errorMessage)
    {
        _ = hubContext.Clients.Group(ScanProgressHub.GroupName(scanId)).SendAsync("ScanFailed", new { scanId, errorMessage });
    }

    public void ReportCancelled(Guid scanId)
    {
        _ = hubContext.Clients.Group(ScanProgressHub.GroupName(scanId)).SendAsync("ScanCancelled", new { scanId });
    }
}
