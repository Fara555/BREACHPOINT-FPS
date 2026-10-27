using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HierarchyToolkit
{
    [FilePath("ProjectSettings/HierarchyToolkitSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ProjectSettings : ScriptableSingleton<ProjectSettings>
    {
        public bool enabled = true;
        public Palette palette = new Palette();
        public void Commit() { Save(true); ToolkitEvents.Notify(); }
    }

}
