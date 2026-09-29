namespace TestTongHop.Old
{
    using UnityEngine;
    using UnityEngine.UI;

    public class CircleRaycastImage : Image
    {
        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 localPoint);

            Rect rect   = rectTransform.rect;
            float radius = rect.width * 0.5f;
            return Vector2.Distance(localPoint, rect.center) <= radius;
        }
    }
}
