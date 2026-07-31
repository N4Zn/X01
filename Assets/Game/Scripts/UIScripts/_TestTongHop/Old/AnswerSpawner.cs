namespace TestTongHop.Old
{
    using UnityEngine;
    using TMPro;
    using System.Collections.Generic;

    public class AnswerSpawner : MonoBehaviour
    {
        public GameObject        answerPrefab;
        public Transform         answerParent;
        public FloatingAnswerOrbit orbit;
        public TMP_Text          questionText;
        public CSVQuestionLoader loader;

        void Start() => SpawnAnswers(loader.questions[0]);

        public void SpawnAnswers(QuestionData q)
        {
            questionText.text = q.question;

            foreach (Transform child in answerParent)
                Destroy(child.gameObject);

            int answerCount = 1 + q.wrongAnswers.Count;

            var answers   = new List<string> { q.correct };
            var wrongPool = new List<string>(q.wrongAnswers);

            while (answers.Count < answerCount && wrongPool.Count > 0)
            {
                int rand = Random.Range(0, wrongPool.Count);
                answers.Add(wrongPool[rand]);
                wrongPool.RemoveAt(rand);
            }

            for (int i = 0; i < answers.Count; i++)
            {
                int rand  = Random.Range(i, answers.Count);
                string t  = answers[i]; answers[i] = answers[rand]; answers[rand] = t;
            }

            orbit.buttons.Clear();

            for (int i = 0; i < answers.Count; i++)
            {
                GameObject obj = Instantiate(answerPrefab, answerParent);
                obj.GetComponentInChildren<TMP_Text>().text = answers[i];
                orbit.buttons.Add(obj.GetComponent<RectTransform>());
            }

            orbit.activeButtonCount = answers.Count;
            orbit.UpdateButtons();
        }
    }
}
