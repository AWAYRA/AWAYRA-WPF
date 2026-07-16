using System.Runtime.InteropServices;
using Awayra.Core.Abstractions;
using Awayra.App.Interop;

namespace Awayra.App.Services;

public sealed class WindowsIdleMonitor : IIdleMonitor
{
    public TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LastInputInfo
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.LastInputInfo>()
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var tick = unchecked((uint)Environment.TickCount);
        var idleMs = tick - info.DwTime;
        return TimeSpan.FromMilliseconds(idleMs);
    }

    public bool IsIdle(TimeSpan threshold) => GetIdleTime() >= threshold;
}
