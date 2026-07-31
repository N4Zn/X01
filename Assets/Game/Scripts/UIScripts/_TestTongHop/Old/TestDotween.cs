namespace TestTongHop.Old
{
    using UnityEngine;
    using DG.Tweening;

    public class TestTween : MonoBehaviour
    {
        void Start()
        {
            transform.DOScale(1.2f, 0.5f).SetLoops(-1, LoopType.Yoyo);
        }
    }
}
