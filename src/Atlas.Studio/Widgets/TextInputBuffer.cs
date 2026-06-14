using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Widgets;

/// <summary>
/// A reusable text-edit buffer for Dear ImGui input widgets.
/// </summary>
/// <remarks>
/// ImGui edits text in a caller-owned native byte buffer. This wraps that buffer
/// and the UTF-8 conversions so the screens can work in terms of
/// <see cref="System.String"/> without repeating unsafe interop.
/// </remarks>
internal sealed class TextInputBuffer
{
    private readonly byte[] _buffer;

    public TextInputBuffer(int capacityBytes = 8192) => _buffer = new byte[capacityBytes];

    /// <summary>The current text, decoded up to the null terminator.</summary>
    public string Text
    {
        get
        {
            int length = Array.IndexOf(_buffer, (byte)0);
            if (length < 0)
            {
                length = _buffer.Length;
            }

            return Encoding.UTF8.GetString(_buffer, 0, length);
        }
    }

    public bool IsEmpty => _buffer[0] == 0;

    public void Clear() => Array.Clear(_buffer);

    public void SetText(string value)
    {
        Array.Clear(_buffer);
        int count = Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, 0);
        if (count >= _buffer.Length)
        {
            _buffer[^1] = 0;
        }
    }

    /// <summary>Renders a single-line input. Returns true while editing.</summary>
    public unsafe bool InputLine(string label, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        fixed (byte* ptr = _buffer)
        {
            return ImGui.InputText(label, ptr, (nuint)_buffer.Length, flags);
        }
    }

    /// <summary>Renders a multi-line input. Returns true while editing.</summary>
    public unsafe bool InputMultiline(string label, Vector2 size, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        fixed (byte* ptr = _buffer)
        {
            return ImGui.InputTextMultiline(label, ptr, (nuint)_buffer.Length, size, flags);
        }
    }
}
