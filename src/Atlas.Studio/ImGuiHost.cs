using System.Runtime.CompilerServices;
using Hexa.NET.GLFW;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.OpenGL;
using BackendGLFWwindowPtr = Hexa.NET.ImGui.Backends.GLFW.GLFWwindowPtr;
using GLFWmonitorPtr = Hexa.NET.GLFW.GLFWmonitorPtr;
using GLFWwindowPtr = Hexa.NET.GLFW.GLFWwindowPtr;

namespace Atlas.Studio;

/// <summary>
/// Owns the GLFW window, OpenGL context, and Dear ImGui lifecycle, and pumps a
/// render callback each frame.
/// </summary>
/// <remarks>
/// This is pure platform plumbing, deliberately separated from any Atlas UI so
/// the screens never touch windowing concerns. The setup mirrors Hexa.NET's
/// reference GLFW + OpenGL3 example (docking and multi-viewport enabled).
/// </remarks>
internal sealed class ImGuiHost
{
    private readonly string _title;

    public ImGuiHost(string title) => _title = title;

    /// <summary>
    /// Opens the window and runs the loop, invoking <paramref name="renderFrame"/>
    /// once per frame to submit ImGui windows. Returns when the window closes.
    /// </summary>
    public unsafe void Run(Action renderFrame)
    {
        ArgumentNullException.ThrowIfNull(renderFrame);

        if (GLFW.Init() == 0)
        {
            throw new InvalidOperationException("Failed to initialize GLFW.");
        }

        const string GlslVersion = "#version 150";
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 3);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 2);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);

        GLFWmonitorPtr monitor = GLFW.GetPrimaryMonitor();
        float scale = ImGuiImplGLFW.GetContentScaleForMonitor(
            Unsafe.BitCast<GLFWmonitorPtr, Hexa.NET.ImGui.Backends.GLFW.GLFWmonitorPtr>(monitor));
        if (scale <= 0)
        {
            scale = 1f;
        }

        GLFWwindowPtr window = GLFW.CreateWindow((int)(1440 * scale), (int)(900 * scale), _title, null, null);
        if (window.IsNull)
        {
            GLFW.Terminate();
            throw new InvalidOperationException("Failed to create the GLFW window.");
        }

        GLFW.MakeContextCurrent(window);
        GLFW.SwapInterval(1);

        ImGuiContextPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

        ImGui.StyleColorsDark();
        ImGuiStylePtr style = ImGui.GetStyle();
        style.ScaleAllSizes(scale);
        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
        }

        ImGuiImplGLFW.SetCurrentContext(context);
        if (!ImGuiImplGLFW.InitForOpenGL(Unsafe.BitCast<GLFWwindowPtr, BackendGLFWwindowPtr>(window), true))
        {
            GLFW.Terminate();
            throw new InvalidOperationException("Failed to initialize the ImGui GLFW backend.");
        }

        ImGuiImplOpenGL3.SetCurrentContext(context);
        if (!ImGuiImplOpenGL3.Init(GlslVersion))
        {
            GLFW.Terminate();
            throw new InvalidOperationException("Failed to initialize the ImGui OpenGL3 backend.");
        }

        var gl = new GL(new GlfwBindingsContext(window));

        try
        {
            while (GLFW.WindowShouldClose(window) == 0)
            {
                GLFW.PollEvents();

                if (GLFW.GetWindowAttrib(window, GLFW.GLFW_ICONIFIED) != 0)
                {
                    ImGuiImplGLFW.Sleep(10);
                    continue;
                }

                GLFW.MakeContextCurrent(window);
                gl.ClearColor(0.09f, 0.10f, 0.12f, 1.0f);
                gl.Clear(GLClearBufferMask.ColorBufferBit);

                ImGuiImplOpenGL3.NewFrame();
                ImGuiImplGLFW.NewFrame();
                ImGui.NewFrame();

                renderFrame();

                ImGui.Render();
                GLFW.MakeContextCurrent(window);
                ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

                if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
                {
                    ImGui.UpdatePlatformWindows();
                    ImGui.RenderPlatformWindowsDefault();
                    GLFW.MakeContextCurrent(window);
                }

                GLFW.SwapBuffers(window);
            }
        }
        finally
        {
            ImGuiImplOpenGL3.Shutdown();
            ImGuiImplOpenGL3.SetCurrentContext(null);
            ImGuiImplGLFW.Shutdown();
            ImGuiImplGLFW.SetCurrentContext(null);
            ImGui.DestroyContext();
            gl.Dispose();
            GLFW.DestroyWindow(window);
            GLFW.Terminate();
        }
    }

    /// <summary>Bridges Hexa.NET.OpenGL's loader to the GLFW context.</summary>
    private sealed unsafe class GlfwBindingsContext : HexaGen.Runtime.IGLContext
    {
        private readonly GLFWwindowPtr _window;

        public GlfwBindingsContext(GLFWwindowPtr window) => _window = window;

        public nint Handle => (nint)_window.Handle;

        public bool IsCurrent => GLFW.GetCurrentContext() == _window;

        public void Dispose()
        {
        }

        public nint GetProcAddress(string procName) => (nint)GLFW.GetProcAddress(procName);

        public bool IsExtensionSupported(string extensionName) => GLFW.ExtensionSupported(extensionName) != 0;

        public void MakeCurrent() => GLFW.MakeContextCurrent(_window);

        public void SwapBuffers() => GLFW.SwapBuffers(_window);

        public void SwapInterval(int interval) => GLFW.SwapInterval(interval);

        public bool TryGetProcAddress(string procName, out nint procAddress)
        {
            procAddress = (nint)GLFW.GetProcAddress(procName);
            return procAddress != 0;
        }
    }
}
