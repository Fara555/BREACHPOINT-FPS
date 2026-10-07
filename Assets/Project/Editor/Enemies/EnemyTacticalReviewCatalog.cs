using System;
using System.Collections.Generic;

namespace Breachpoint.Editor.Enemies
{
    // Global scenario IDs remain stable for automation; the UI displays focused groups.
    internal static class EnemyTacticalReviewCatalog
    {
        internal static readonly string[] GroupNames = { "Combat movement", "Tactical AI", "Crouch transitions", "Stationary death", "Moving Hit / death" };
        internal static readonly int[][] Cases = BuildCases();
        internal static readonly string[][] Names = BuildNames();

        private static int[][] BuildCases()
        {
            var moving = new List<int> { 42, 43 };
            for (int index = 45; index < EnemyValidationRunner.HumanTacticalNames.Length; index++) moving.Add(index);
            return new[] { Range(0, 12), Range(12, 20), Range(32, 6), new[] { 38, 39, 40, 41, 44 }, moving.ToArray() };
        }
        private static int[] Range(int start, int count)
        {
            var result = new int[count];
            for (int index = 0; index < count; index++) result[index] = start + index;
            return result;
        }
        private static string[][] BuildNames()
        {
            var result = new string[Cases.Length][];
            for (int group = 0; group < Cases.Length; group++)
            {
                result[group] = new string[Cases[group].Length];
                for (int index = 0; index < Cases[group].Length; index++) result[group][index] = EnemyValidationRunner.HumanTacticalNames[Cases[group][index]];
            }
            return result;
        }
        internal static int GroupFor(int scenario)
        {
            for (int group = 0; group < Cases.Length; group++)
                if (Array.IndexOf(Cases[group], scenario) >= 0) return group;
            throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        internal static int Step(int scenario, int direction)
        {
            int[] cases = Cases[GroupFor(scenario)];
            int index = (Array.IndexOf(cases, scenario) + direction) % cases.Length;
            if (index < 0) index += cases.Length;
            return cases[index];
        }
    }
}
