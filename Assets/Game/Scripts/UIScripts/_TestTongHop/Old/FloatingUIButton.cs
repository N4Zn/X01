namespace TestTongHop.Old
{
    using UnityEngine;
    using UnityEngine.UI;
    using DG.Tweening;

    public class OrbitUI : MonoBehaviour
    {
        RectTransform _rect;
        Image         _img;

        [SerializeField] float radius        = 300f;
        [SerializeField] float speed         = 300f;
        [SerializeField] float breathingScale = 1.05f;

        float _angle;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
            _img  = GetComponent<Image>();
            _img.alphaHitTestMinimumThreshold = 0.1f;
            _angle = Random.Range(0f, 360f);

            _rect.DOScale(breathingScale, 1f)
                 .SetEase(Ease.InOutSine)
                 .SetLoops(-1, LoopType.Yoyo);

            GetComponent<Button>().onClick.AddListener(OnClickEffect);
        }

        void Update()
        {
            _angle += speed * Time.deltaTime;
            float rad = _angle * Mathf.Deg2Rad;
            _rect.anchoredPosition = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
        }

        void OnClickEffect()
            => _rect.DOPunchScale(Vector3.one * 0.2f, 0.2f, 5, 0.5f);
    }
}
