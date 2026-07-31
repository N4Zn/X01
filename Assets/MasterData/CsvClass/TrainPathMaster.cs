using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MasterData
{
    // Master data for TrainPath game path templates
    // Each param defines a path through the 3x3 grid
    public class TrainPathMaster : ScriptableObject
    {
        [HideInInspector]
        public List<Param> param = new List<Param>();

        [System.SerializableAttribute]
        public class Param
        {
            public int template_id;
            public string entry;      // Entry point "row:col" (e.g., "-1:1")
            public string path;       // Path cells "r:c|r:c|..." (e.g., "0:1|1:1|2:1")
            public string exit;       // Exit point "row:col" (e.g., "3:1")
            public int difficulty;    // 1=easy, 2=medium, 3=hard
        }
    }
}
