namespace TestTongHop.Old
{
    using System.Collections.Generic;

    [System.Serializable]
    public class QuestionData
    {
        public string question;
        public string correct;
        public List<string> wrongAnswers = new List<string>();
    }
}
