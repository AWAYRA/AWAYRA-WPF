using Awayra.Core.Models;

namespace Awayra.Core.Coordination;

public readonly record struct OverlaySessionState(bool EyeVisible, bool MoveVisible)
{
    public static OverlaySessionState Empty => new(false, false);

    public bool HasAnyVisible => EyeVisible || MoveVisible;

    public bool BothVisible => EyeVisible && MoveVisible;
}

public static class OverlaySessionPolicy
{
    public static OverlaySessionState AfterCloseAll() => OverlaySessionState.Empty;

    public static OverlaySessionState AfterShow(BreakType breakType, OverlaySessionState current)
    {
        _ = current;
        return breakType switch
        {
            BreakType.Eye => new OverlaySessionState(EyeVisible: true, MoveVisible: false),
            BreakType.Move => new OverlaySessionState(EyeVisible: false, MoveVisible: true),
            _ => throw new ArgumentOutOfRangeException(nameof(breakType), breakType, null)
        };
    }
}
