namespace TestTongHop.Old
{
    using UnityEngine;
    using System.Collections.Generic;

    public class FloatingAnswerOrbit : MonoBehaviour
    {
        public List<RectTransform> buttons;
        public float radius      = 250f;
        public float rotateSpeed = 30f;

        [Range(1, 5)]
        public int activeButtonCount = 5;

        float _currentAngle;

        void Start()  => UpdateButtons();
        void Update() { _currentAngle += rotateSpeed * Time.deltaTime; ArrangeButtons(); }

        public void UpdateButtons()
        {
            buttons.RemoveAll(item => item == null);
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].gameObject.SetActive(i < activeButtonCount);
            ArrangeButtons();
        }

        void ArrangeButtons()
        {
            if (activeButtonCount <= 0) return;
            float step = 360f / activeButtonCount;
            int visible = 0;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (!buttons[i].gameObject.activeSelf) continue;
                float angle = _currentAngle + visible * step;
                float rad   = angle * Mathf.Deg2Rad;
                buttons[i].anchoredPosition = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
                visible++;
            }
        }
    }
}
