namespace Awayra.App.Interop;

public sealed class DisplayBoundsStabilizer
{
    private readonly int _requiredStableSamples;
    private readonly int _maximumSamples;
    private System.Drawing.Rectangle? _lastBounds;
    private int _stableSamples;
    private int _totalSamples;

    public DisplayBoundsStabilizer(int requiredStableSamples = 2, int maximumSamples = 8)
    {
        if (requiredStableSamples < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredStableSamples));
        }

        if (maximumSamples < requiredStableSamples)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSamples));
        }

        _requiredStableSamples = requiredStableSamples;
        _maximumSamples = maximumSamples;
    }

    public System.Drawing.Rectangle? CurrentBounds => _lastBounds;

    public bool Observe(System.Drawing.Rectangle bounds)
    {
        _totalSamples++;
        if (_lastBounds == bounds)
        {
            _stableSamples++;
        }
        else
        {
            _lastBounds = bounds;
            _stableSamples = 1;
        }

        return _stableSamples >= _requiredStableSamples || _totalSamples >= _maximumSamples;
    }

    public void Reset()
    {
        _lastBounds = null;
        _stableSamples = 0;
        _totalSamples = 0;
    }
}
