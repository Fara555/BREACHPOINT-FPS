using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HierarchyToolkit
{
    [FilePath("UserSettings/HierarchyToolkitPreferences.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class Preferences : ScriptableSingleton<Preferences>
    {
        public bool enabled = true, colors = true, icons = true, folders = true;
        public bool sceneSelector = true, navigation = true, componentMap = true, componentEditor = true;
        public bool activation = true, shortcuts = true, rules = true, lines = true, zebra = true, minimal;
        public float tint = .18f;
        public int maxComponents = 6;
        public List<KeyBinding> bindings = Defaults();
        public static List<KeyBinding> Defaults() => new List<KeyBinding> {
            new KeyBinding("Default parent", KeyCode.D), new KeyBinding("Toggle active", KeyCode.A), new KeyBinding("Focus", KeyCode.F),
            new KeyBinding("Delete", KeyCode.X), new KeyBinding("Expand recursive", KeyCode.E), new KeyBinding("Expand only", KeyCode.E, true),
            new KeyBinding("Collapse all", KeyCode.E, true, true) };
        public void Commit() { Save(true); ToolkitEvents.Notify(); }
        public void Reset()
        {
            enabled = colors = icons = folders = sceneSelector = navigation = componentMap = componentEditor = activation = shortcuts = rules = lines = zebra = true;
            minimal = false; tint = .18f; maxComponents = 6; bindings = Defaults(); Commit();
        }
    }

}
