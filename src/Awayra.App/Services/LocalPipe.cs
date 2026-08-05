using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Awayra.App.Services;

/// <summary>
/// Shared construction for every named pipe Awayra listens on.
/// <para>
/// Pipe names live in a machine-wide namespace and a pipe created with default security is reachable
/// by any local account. Every Awayra pipe is per-user by design, so the name carries the owner's SID
/// and the ACL grants that account alone. Without this, a fixed name such as
/// "Awayra.UiTest.Commands" was an unauthenticated local channel that could quit the application.
/// </para>
/// </summary>
internal static class LocalPipe
{
    private static readonly SecurityIdentifier CurrentUser =
        WindowsIdentity.GetCurrent().User ?? new SecurityIdentifier(WellKnownSidType.WorldSid, null);

    public static string NameFor(string baseName) => $"{baseName}.{CurrentUser.Value}";

    public static NamedPipeServerStream CreateServer(
        string pipeName,
        PipeDirection direction,
        int maxServerInstances = 1)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            CurrentUser,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            direction,
            maxServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
    }
}
