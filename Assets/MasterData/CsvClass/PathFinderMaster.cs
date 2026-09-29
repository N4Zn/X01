using UnityEngine;
using System.Collections.Generic;

namespace MasterData
{
    /// <summary>
    /// Master data for PathFinder game questions.
    /// Each question has 3 paths (left/middle/right), one correct, two blocked.
    /// </summary>
    public class PathFinderMaster : ScriptableObject
    {
        [HideInInspector]
        public List<Param> param = new List<Param>();

        [System.Serializable]
        public class Param
        {
            public int question_id;
            public string correct_path;     // "left", "middle", "right"
            public string obstacle_left;    // "none", "rock", "tree", "water", "wall", "hole"
            public string obstacle_middle;  // same options
            public string obstacle_right;   // same options
            public string destination;      // "house", "school", "park", "zoo", "market", "library"
            public string theme;            // "forest", "city", "ocean", "mountain", "desert"
            public int difficulty;          // 1=easy, 2=medium, 3=hard
        }
    }
}
