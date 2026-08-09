using System.Windows.Media.Animation;
using UserControl = System.Windows.Controls.UserControl;

namespace Awayra.App.Views;

/// <summary>
/// Animated movement prompt shown during a Move Break: a figure stops typing, leaves the desk,
/// walks out with the camera following, stretches overhead, bends side to side, rolls their
/// shoulders, turns to face the viewer for three squats and three jumps, then walks back and sits
/// down. The loop starts and ends in the same pose so it reads as a continuous round trip rather
/// than a restart.
/// </summary>
public partial class MoveExerciseView : UserControl
{
    private readonly Storyboard _scene;
    private bool _running;

    public MoveExerciseView()
    {
        InitializeComponent();
        _scene = (Storyboard)Resources["Move.Storyboard"];
        Unloaded += (_, _) => StopAnimation();
    }

    public void StartAnimation()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _scene.Begin(this, true);
    }

    public void StopAnimation()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _scene.Stop(this);
    }

    /// <summary>
    /// Freezes the scene on the seated starting pose and states the whole routine in one line, so
    /// a user who asked for reduced motion still knows what to do.
    /// </summary>
    public void ApplyReducedMotion()
    {
        StopAnimation();
        MoveCaptionText.Text = "Stand up, walk, stretch, three squats, three jumps, then sit back down";
    }
}
