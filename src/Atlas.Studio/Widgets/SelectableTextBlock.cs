using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Widgets;

/// <summary>
/// Renders read-only text that supports mouse selection and clipboard copy
/// (Ctrl+C), unlike plain <see cref="ImGui.TextWrapped"/>.
/// </summary>
/// <remarks>
/// Dear ImGui only exposes selection on input widgets. A read-only
/// <c>InputTextMultiline</c> is the standard pattern for copy-friendly transcript
/// text; styling is flattened so it reads like body copy rather than a text box.
/// </remarks>
internal sealed class SelectableTextBlock
{
    private byte[] _buffer = new byte[256];
    private string _text = string.Empty;

    /// <summary>Stores the text to display. Grows the native buffer as needed.</summary>
    public void SetText(string value)
    {
        _text = value ?? string.Empty;

        int byteCount = Encoding.UTF8.GetByteCount(_text);
        int required = byteCount + 1;
        if (required > _buffer.Length)
        {
            _buffer = new byte[required + 512];
        }

        Array.Clear(_buffer);
        if (byteCount > 0)
        {
            Encoding.UTF8.GetBytes(_text, 0, _text.Length, _buffer, 0);
        }
    }

    /// <summary>
    /// Draws the block at the current cursor, auto-sizing height to the wrapped
    /// content width.
    /// </summary>
    public unsafe void Draw()
    {
        if (_text.Length == 0)
        {
            return;
        }

        float width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        ImGuiStylePtr style = ImGui.GetStyle();
        Vector2 wrapped = ImGui.CalcTextSize(_text, wrapWidth: width);
        float height = MathF.Max(ImGui.GetTextLineHeight(), wrapped.Y)
            + style.FramePadding.Y * 2f;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0f, style.FramePadding.Y * 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f));

        fixed (byte* ptr = _buffer)
        {
            ImGui.InputTextMultiline(
                "##selectable",
                ptr,
                (nuint)_buffer.Length,
                new Vector2(width, height),
                ImGuiInputTextFlags.ReadOnly);
        }

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
    }
}
