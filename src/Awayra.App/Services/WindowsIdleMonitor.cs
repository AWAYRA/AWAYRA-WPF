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

        // Both values are 32-bit tick counts that wrap roughly every 49.7 days. Unchecked subtraction
        // gives the correct elapsed span across a wrap without the off-by-one the explicit form had.
        var tick = unchecked((uint)Environment.TickCount);
        var idleMs = unchecked(tick - info.DwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }

    public bool IsIdle(TimeSpan threshold) => GetIdleTime() >= threshold;
}
