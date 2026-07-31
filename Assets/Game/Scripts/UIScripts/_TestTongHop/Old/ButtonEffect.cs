namespace TestTongHop.Old
{
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    using System.Collections;

    public class AnswerButtonEffect : MonoBehaviour, IPointerClickHandler
    {
        [Header("Image")]
        public Image    targetImage;
        public Sprite[] randomSprites;

        [Header("Breathing")]
        public float minBreathScale = 0.03f;
        public float maxBreathScale = 0.12f;
        public float minBreathSpeed = 1f;
        public float maxBreathSpeed = 5f;

        [Header("Rotate")]
        public float minRotateSpeed = -120f;
        public float maxRotateSpeed =  120f;

        [Header("Click")]
        public float clickScale    = 1.3f;
        public float clickDuration = 0.15f;

        [Header("Size")]
        public float minSize = 0.8f;
        public float maxSize = 1.3f;

        [Header("References")]
        public Transform rotatingVisual;

        float _breathScale, _breathSpeed, _rotateSpeed, _phaseOffset;
        Vector3 _baseScale;
        bool _clicked;

        void Start()
        {
            float size = Random.Range(minSize, maxSize);
            transform.localScale = Vector3.one * size;
            _baseScale   = transform.localScale;
            _breathScale = Random.Range(minBreathScale, maxBreathScale);
            _breathSpeed = Random.Range(minBreathSpeed, maxBreathSpeed);
            _rotateSpeed = Random.Range(minRotateSpeed, maxRotateSpeed);
            _phaseOffset = Random.Range(0f, 100f);

            if (randomSprites.Length > 0)
                targetImage.sprite = randomSprites[Random.Range(0, randomSprites.Length)];
        }

        void Update()
        {
            if (_clicked) return;
            float scale = 1 + Mathf.Sin(Time.time * _breathSpeed + _phaseOffset) * _breathScale;
            transform.localScale = _baseScale * scale;
            rotatingVisual.Rotate(0, 0, _rotateSpeed * Time.deltaTime);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_clicked) return;
            StartCoroutine(ClickAnimation());
        }

        IEnumerator ClickAnimation()
        {
            _clicked = true;
            Vector3 big = _baseScale * clickScale;
            float t = 0;
            while (t < clickDuration) { t += Time.deltaTime; transform.localScale = Vector3.Lerp(_baseScale, big, t / clickDuration); yield return null; }
            t = 0;
            while (t < clickDuration) { t += Time.deltaTime; transform.localScale = Vector3.Lerp(big, Vector3.zero, t / clickDuration); yield return null; }
            var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0; cg.blocksRaycasts = false;
        }
    }
}
