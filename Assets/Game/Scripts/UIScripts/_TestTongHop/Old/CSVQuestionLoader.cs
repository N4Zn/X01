namespace TestTongHop.Old
{
    using UnityEngine;
    using System.Collections.Generic;

    public class CSVQuestionLoader : MonoBehaviour
    {
        public TextAsset csvFile;
        public List<QuestionData> questions = new List<QuestionData>();

        void Awake()
        {
            LoadCSV();
        }

        void LoadCSV()
        {
            string[] lines = csvFile.text.Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] cols = lines[i].Split(',');
                QuestionData q = new QuestionData();
                q.question = cols[0];
                q.correct  = cols[1];

                for (int j = 2; j < cols.Length; j++)
                {
                    if (!string.IsNullOrEmpty(cols[j]))
                        q.wrongAnswers.Add(cols[j]);
                }

                questions.Add(q);
            }
        }
    }
}
