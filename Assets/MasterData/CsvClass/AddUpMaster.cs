using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MasterData
{
    // Master data for AddUp game questions
    // Format: A + B = C, one of A/B is hidden, player picks from 3 choices
    public class AddUpMaster : ScriptableObject
    {
        [HideInInspector]
        public List<Param> param = new List<Param>();

        [System.SerializableAttribute]
        public class Param
        {
            public int question_id;
            public int number_a;
            public int number_b;
            public string hidden_position; // "a" or "b"
            public int answer_1;
            public int answer_2;
            public int answer_3;
            public int correct_answer; // 1-3
            public string image_type;
            public int difficulty;
        }
    }
}
