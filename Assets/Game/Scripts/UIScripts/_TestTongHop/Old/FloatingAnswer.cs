namespace TestTongHop.Old
{
    using UnityEngine;
    using TMPro;

    public class FloatingAnswer : MonoBehaviour
    {
        public int value = 7;
        public TextMeshPro textMesh;
        public float speed = 3f;

        Rigidbody2D _rb;

        void Start()
        {
            textMesh.text = value.ToString();
            _rb = GetComponent<Rigidbody2D>();
            _rb.linearVelocity = Random.insideUnitCircle.normalized * speed;
        }

        void OnMouseDown() => Debug.Log("Clicked: " + value);
    }
}
