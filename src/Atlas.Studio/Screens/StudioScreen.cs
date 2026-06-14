using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Base class for a dockable dashboard screen.
/// </summary>
/// <remarks>
/// Each screen is an independent ImGui window the user can show, hide, and dock.
/// The base handles the window frame and visibility toggle so subclasses only
/// implement their body.
/// </remarks>
internal abstract class StudioScreen
{
    /// <summary>The window title and menu label.</summary>
    public abstract string Title { get; }

    /// <summary>Whether the screen is currently shown.</summary>
    public bool Open { get; set; } = true;

    /// <summary>Draws the screen window if it is open.</summary>
    public void Draw()
    {
        if (!Open)
        {
            return;
        }

        bool open = Open;
        if (ImGui.Begin(Title, ref open))
        {
            RenderBody();
        }

        ImGui.End();
        Open = open;
    }

    /// <summary>Renders the screen's contents inside its window.</summary>
    protected abstract void RenderBody();
}
