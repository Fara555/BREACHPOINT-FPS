using System;

namespace Breachpoint.Editor.Enemies
{
    internal static class EnemyAnimationAssetPaths
    {
        internal const string AdamRoot = "Assets/Project/Art/Enemies/Adam";
        internal const string Config = AdamRoot + "/Config";
        internal const string Animations = AdamRoot + "/Animations";
        internal const string Source = Animations + "/Source";
        internal const string Derived = Animations + "/Derived";

        internal static string DerivedClip(string fileName)
        {
            string group = fileName.EndsWith("InPlace.anim", StringComparison.Ordinal) ? "Turns" :
                fileName.EndsWith("Recoil.anim", StringComparison.Ordinal) ? "Recoil" :
                fileName == "RifleRaise.anim" || fileName == "RifleLower.anim" ? "Readiness" : "Actions";
            return Derived + "/" + group + "/" + fileName;
        }
    }
}
