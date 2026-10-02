using System;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [Flags]
    public enum EnemyDebugCategory
    {
        None = 0, State = 1, Perception = 2, Decision = 4, Squad = 8, Cover = 16,
        Navigation = 32, Combat = 64, Animation = 128, Death = 256, Reset = 512, Warning = 1024, All = 2047
    }
    [Serializable]
    public sealed class EnemyDebugSettings
    {
        public bool Enabled;
        public EnemyDebugCategory Logs;
        public bool Labels;
        public bool FieldOfView;
        public bool Hearing;
        public bool SightAndFire;
        public bool Memory;
        public bool Path;
        public bool Cover;
        public bool Candidates;
        public bool SquadLinks;
    }
}