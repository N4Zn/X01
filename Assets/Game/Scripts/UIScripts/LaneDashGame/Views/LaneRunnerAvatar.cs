using System.Collections;
using UnityEngine;

/// <summary>
/// Nhân vật 3D 1 bên — chỉ đổi làn theo trục X (trượt nhanh), Z giữ cố định
/// (LaneDashConfigData.playerZ), Y giữ cố định (playerGroundY) trừ hiệu ứng nhún.
///
/// "Chạy tại chỗ" mô phỏng bằng hiệu ứng nhún nhẹ (bob) theo trục Y — CHỈ dùng khi nhân vật là
/// primitive không có animation thật. Khi đã gắn Animator+Controller (model Humanoid chạy thật,
/// xem LaneDashSceneBuilder.CreatePlayerAvatar) thì clip Run tự có chuyển động lên xuống theo
/// nhịp chân riêng — cộng thêm bob sin ở đây (tần số 8Hz, không đồng bộ với nhịp chân trong clip)
/// sẽ đè lên animation thật, gây cảm giác hình bị "vênh/giật" liên tục. Không ảnh hưởng logic va
/// chạm vì LaneTrack chỉ so sánh làn hiện tại (CurrentLane), không đọc vị trí thật của avatar.
/// </summary>
public class LaneRunnerAvatar : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] Renderer characterRenderer;
    [SerializeField] float bobAmplitude = 0.15f;
    [SerializeField] float bobSpeed = 8f;

    Color _baseColor;
    float _baseVisualY;
    bool _useBob;
    Coroutine _slideRoutine;
    Coroutine _stunRoutine;
    Coroutine _boostRoutine;
    Coroutine _flyRoutine;

    public int CurrentLane { get; private set; }
    public bool IsStunned { get; private set; }

    void Awake()
    {
        if (characterRenderer) _baseColor = characterRenderer.material.color;
        var animator = GetComponent<Animator>();
        _useBob = animator == null || animator.runtimeAnimatorController == null;
    }

    void Start() => _baseVisualY = transform.position.y;

    void Update()
    {
        if (!_useBob) return;
        float bob = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobAmplitude;
        var pos = transform.position;
        transform.position = new Vector3(pos.x, _baseVisualY + bob, pos.z);
    }

    // ── Position ──────────────────────────────────────────────────────────────

    /// <summary>Teleport không animation — dùng lúc reset ván chơi.</summary>
    public void SetPosition(int lane, float x, float y, float z)
    {
        CurrentLane = lane;
        _baseVisualY = y;
        transform.position = new Vector3(x, y, z);
    }

    public void SlideToLane(int lane, float x, float duration)
    {
        if (lane == CurrentLane) return;
        CurrentLane = lane;
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideCoroutine(x, duration));
    }

    IEnumerator SlideCoroutine(float targetX, float duration)
    {
        float fromX = transform.position.x;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float x = Mathf.Lerp(fromX, targetX, t);
            var pos = transform.position;
            transform.position = new Vector3(x, pos.y, pos.z);
            yield return null;
        }
        var final = transform.position;
        transform.position = new Vector3(targetX, final.y, final.z);
    }

    // ── Stun ──────────────────────────────────────────────────────────────────

    /// <summary>Va vật cản: chớp đỏ + khoá đổi làn trong duration giây (mô phỏng "chậm lại").</summary>
    public void PlayStun(float duration)
    {
        if (_stunRoutine != null) StopCoroutine(_stunRoutine);
        _stunRoutine = StartCoroutine(StunCoroutine(duration));
    }

    IEnumerator StunCoroutine(float duration)
    {
        IsStunned = true;
        SetColor(Color.red);
        yield return new WaitForSeconds(duration);
        SetColor(_baseColor);
        IsStunned = false;
    }

    // ── Vật phẩm đặc biệt ─────────────────────────────────────────────────────

    /// <summary>Cưỡi ngựa: chớp vàng nâu trong duration giây — không khoá đổi làn (chỉ tốc độ đổi,
    /// xử lý ở LaneTrack).</summary>
    public void PlayBoost(float duration)
    {
        if (_boostRoutine != null) StopCoroutine(_boostRoutine);
        _boostRoutine = StartCoroutine(TintCoroutine(new Color(0.90f, 0.70f, 0.20f), duration));
    }

    /// <summary>Bay bằng tên lửa: chớp xanh dương trong duration giây — không khoá đổi làn (bất tử
    /// với vật cản xử lý ở LaneTrack).</summary>
    public void PlayFly(float duration)
    {
        if (_flyRoutine != null) StopCoroutine(_flyRoutine);
        _flyRoutine = StartCoroutine(TintCoroutine(new Color(0.45f, 0.80f, 1f), duration));
    }

    IEnumerator TintCoroutine(Color tint, float duration)
    {
        SetColor(tint);
        yield return new WaitForSeconds(duration);
        SetColor(_baseColor);
    }

    void SetColor(Color c)
    {
        if (characterRenderer) characterRenderer.material.color = c;
    }
}
