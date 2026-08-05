using System.Windows.Media.Animation;
using UserControl = System.Windows.Controls.UserControl;

namespace Awayra.App.Views;

/// <summary>
/// Animated eye exercise shown during an Eye Reset break: a focus cue that travels out and back,
/// and ten complete blinks counted out for the user. Motion is opt-in so the Reduced motion
/// setting still leaves a readable, static illustration behind.
/// </summary>
public partial class EyeExerciseView : UserControl
{
    private readonly Storyboard _blink;
    private readonly Storyboard _counter;
    private readonly Storyboard _focus;
    private bool _running;

    public EyeExerciseView()
    {
        InitializeComponent();
        _blink = (Storyboard)Resources["Eye.BlinkStoryboard"];
        _counter = (Storyboard)Resources["Eye.CounterStoryboard"];
        _focus = (Storyboard)Resources["Eye.FocusStoryboard"];
        Unloaded += (_, _) => StopAnimation();
    }

    public void StartAnimation()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _blink.Begin(this, true);
        _counter.Begin(this, true);
        _focus.Begin(this, true);
    }

    public void StopAnimation()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _blink.Stop(this);
        _counter.Stop(this);
        _focus.Stop(this);
    }

    /// <summary>
    /// Leaves the eye open and replaces the per-blink counter with a single static instruction,
    /// so a user who asked for reduced motion still gets the guidance without the movement.
    /// </summary>
    public void ApplyReducedMotion()
    {
        StopAnimation();
        FocusHintText.Text = "Look at something far away";
        BlinkCounterText.Text = "Then blink slowly ten times";
    }
}
