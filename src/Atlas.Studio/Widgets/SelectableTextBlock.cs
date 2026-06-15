using Hexa.NET.ImGui;

namespace Atlas.Studio.Widgets;

/// <summary>
/// Renders read-only body text that word-wraps to the available width and can be
/// copied to the clipboard via a right-click menu.
/// </summary>
/// <remarks>
/// A read-only <c>InputTextMultiline</c> supports drag-selection but does not
/// word-wrap (it scrolls horizontally), which makes long model replies hard to
/// read. Wrapping is the priority here, so the block draws with
/// <see cref="ImGui.TextWrapped(string)"/> and keeps copy support through a
/// context menu rather than native selection.
/// </remarks>
internal sealed class SelectableTextBlock
{
    private string _text = string.Empty;

    /// <summary>Stores the text to display.</summary>
    public void SetText(string value) => _text = value ?? string.Empty;

    /// <summary>Draws the wrapped text at the current cursor, with a copy context menu.</summary>
    public void Draw()
    {
        if (_text.Length == 0)
        {
            return;
        }

        ImGui.TextWrapped(_text);

        if (ImGui.BeginPopupContextItem("##copytext"))
        {
            if (ImGui.MenuItem("Copy"))
            {
                ImGui.SetClipboardText(_text);
            }

            ImGui.EndPopup();
        }
    }
}
