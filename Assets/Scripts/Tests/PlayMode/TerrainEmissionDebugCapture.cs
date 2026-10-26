#nullable enable

using System;
using System.Reflection;
using Kern.Tools.ImGui;
using Kern.Tools.ImGui.Windows;
using Kern.World.Terrain;
using UnityEngine;

namespace Kern.Tests.PlayMode
{
    public sealed class TerrainEmissionDebugCapture : MonoBehaviour
    {
        private TerrainDebugWindow? _window;
        public int Repaints
        {
            get; private set;
        }

        public void InitializeWindow(TerrainRenderer? renderer = null)
        {
            _window = new TerrainDebugWindow(renderer);
            ToolWindows.Register(_window);
            _window.Rect = new Rect(16, 16, 360, 420);
            _window.Visible = true;
            _window.Collapsed = false;
            ToolWindows.RequestScale(1f);
            ToolWindows.Enabled = true;
        }

        // Batch mode does not deliver OnGUI. Establish the native IMGUI
        // container explicitly, then draw the unchanged production content.
        // Native floating-window callbacks require a display-backed GUI loop.
        public void RenderOffscreen(RenderTexture target, Vector2? clickPosition = null)
        {
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
            Type utility = typeof(GUIUtility);
            Type stateType = utility.Assembly.GetType("UnityEngine.ObjectGUIState", throwOnError: true)!;
            object state = Activator.CreateInstance(stateType, nonPublic: true)!;
            MethodInfo beginContainer = utility.GetMethod("BeginContainer", Flags)!;
            MethodInfo beginGui = utility.GetMethod("BeginGUI", Flags)!;
            MethodInfo endContainer = utility.GetMethod("EndContainer", Flags)!;
            MethodInfo layout = typeof(GUILayoutUtility).GetMethod("LayoutFromContainer", Flags)!;
            Type clip = utility.Assembly.GetType("UnityEngine.GUIClip", throwOnError: true)!;
            MethodInfo pushParent = clip.GetMethod("Internal_PushParentClip", Flags, null,
                new[] { typeof(Matrix4x4), typeof(Matrix4x4), typeof(Rect) }, null)!;
            MethodInfo popParent = clip.GetMethod("Internal_PopParentClip", Flags)!;
            MethodInfo absoluteMouse = clip.GetMethod("GetAbsoluteMousePosition", BindingFlags.Static | BindingFlags.Public)!;
            Type eventBindings = utility.Assembly.GetType("UnityEngine.EventIMGUIBindings", throwOnError: true)!;
            eventBindings.GetMethod("Internal_MakeMasterEventCurrent", Flags)!.Invoke(null, new object[] { 0 });
            Event master = (Event)eventBindings.GetField("s_MasterEvent", Flags)!.GetValue(null)!;
            Event savedMaster = new(master);
            MethodInfo copyEvent = typeof(Event).GetMethod("CopyFrom", BindingFlags.Instance | BindingFlags.NonPublic)!;
            PropertyInfo eventTypeResolver = typeof(Event).GetProperty("typeResolver", Flags)!;
            var previousResolver = (Func<IntPtr, EventType>)eventTypeResolver.GetValue(null)!;
            IntPtr injectedPointer = (IntPtr)typeof(Event).GetField("m_Ptr", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(master)!;
            // Input source injection for an offscreen IMGUI container: there is
            // no display-backed GUIView to route native focus/clip events.
            // Widget hit testing, mouse capture, callbacks and renderer updates
            // remain the production implementations. Native window routing is
            // deliberately outside this fixture's scope.
            eventTypeResolver.SetValue(null, new Func<IntPtr, EventType>(pointer =>
                clickPosition.HasValue && pointer == injectedPointer ? master.rawType : previousResolver(pointer)));
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, new Color(0.03f, 0.04f, 0.05f, 1f));
                EventType[] events = clickPosition.HasValue
                    ? new[] { EventType.Layout, EventType.Repaint, EventType.MouseDown, EventType.MouseUp, EventType.Layout, EventType.Repaint }
                    : new[] { EventType.Layout, EventType.Repaint };
                foreach (EventType eventType in events)
                {
                    // Native clipping snapshots the parent event's absolute
                    // pointer position when the container is entered.
                    Event input = new()
                    {
                        type = eventType,
                        button = 0,
                        mousePosition = clickPosition ?? Vector2.zero
                    };
                    copyEvent.Invoke(master, new object[] { input });
                    Event.current = master;
                    beginContainer.Invoke(null, new[] { state });
                    try
                    {
                        copyEvent.Invoke(master, new object[] { input });
                        Event.current = master;
                        beginGui.Invoke(null, new object[] { 0, GetEntityId(), 1 });
                        // The offscreen target uses one physical pixel per GUI
                        // point; do not inherit the host display's Retina scale.
                        utility.GetProperty("pixelsPerPoint", Flags)!.SetValue(null, 1f);
                        GL.Viewport(new Rect(0, 0, target.width, target.height));
                        object layoutCache = typeof(GUILayoutUtility).GetMethod("SelectIDListLayout", Flags)!
                            .Invoke(null, new object[] { GetEntityId() })!;
                        layoutCache.GetType().GetMethod("ResetCursor")!.Invoke(layoutCache, null);
                        if (eventType is EventType.MouseDown or EventType.MouseUp)
                        {
                            Debug.Log($"[EmissionInput] before {Event.current.type} at {Event.current.mousePosition}, enabled={GUI.enabled}, hot={GUIUtility.hotControl}");
                        }
                        GL.PushMatrix();
                        try
                        {
                            GL.LoadPixelMatrix(0, target.width, target.height, 0);
                            Vector2 absolute = (Vector2)absoluteMouse.Invoke(null, null)!;
                            Vector2 requested = clickPosition ?? absolute;
                            Matrix4x4 inputTransform = Matrix4x4.Translate(new Vector3(absolute.x - requested.x, absolute.y - requested.y, 0));
                            pushParent.Invoke(null, new object[] { Matrix4x4.identity, inputTransform,
                                new Rect(0, 0, target.width, target.height) });
                            GUI.BeginGroup(new Rect(0, 0, target.width, target.height));
                            try
                            {
                                GUI.skin = ToolTheme.ResolveSkin(GUI.skin);
                                GUILayout.BeginArea(new Rect(16, 16, 360, 420), GUIContent.none, GUI.skin.window);
                                try
                                {
                                    GUILayout.Space(ToolTheme.HeaderHeight);
                                    if (eventType is EventType.MouseDown or EventType.MouseUp)
                                    {
                                        Debug.Log($"[EmissionInput] content {Event.current.type} at {Event.current.mousePosition}");
                                    }
                                    typeof(TerrainDebugWindow).GetMethod("DrawContent", BindingFlags.Instance | BindingFlags.NonPublic)!
                                        .Invoke(_window, null);
                                }
                                finally
                                {
                                    GUILayout.EndArea();
                                }
                            }
                            finally
                            {
                                GUI.EndGroup();
                                popParent.Invoke(null, null);
                            }
                            if (eventType is EventType.MouseDown or EventType.MouseUp)
                            {
                                Debug.Log($"[EmissionInput] after {Event.current.type}, hot={GUIUtility.hotControl}");
                            }
                            if (eventType == EventType.Layout)
                            {
                                layout.Invoke(null, new object[] { (float)target.width, (float)target.height });
                            }
                            else if (eventType == EventType.Repaint)
                            {
                                Repaints++;
                            }
                        }
                        finally
                        {
                            GL.PopMatrix();
                        }
                    }
                    finally
                    {
                        endContainer.Invoke(null, null);
                    }
                }
            }
            finally
            {
                Debug.Log($"[EmissionUI] Visible={_window!.Visible}, draw events={_window.DrawEvents}, rect={_window.Rect}, screen={Screen.width}x{Screen.height}");
                eventTypeResolver.SetValue(null, previousResolver);
                copyEvent.Invoke(master, new object[] { savedMaster });
                Event.current = master;
                RenderTexture.active = previous;
                ((IDisposable)state).Dispose();
            }
        }

        private void OnGUI()
        {
            ToolWindows.Draw();
            if (Event.current.type == EventType.Repaint)
            {
                Repaints++;
            }
        }

        private void OnDestroy()
        {
            if (_window == null)
            {
                return;
            }
            ToolWindows.Unregister(_window);
            _window.Dispose();
            ToolWindows.Enabled = false;
        }
    }
}
