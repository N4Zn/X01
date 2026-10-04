using System.Collections;
using UnityEngine;

/// <summary>Factory tra EffectType → IVisualEffect + helper chạy effect trên 1 RectTransform.
/// Đây là ĐIỂM DUY NHẤT cần sửa khi thêm effect mới vào thư viện (cùng với capabilities.json —
/// xem ghi chú ở IVisualEffect.cs).</summary>
public static class EffectLibrary
{
    public static IVisualEffect Create(EffectType type)
    {
        switch (type)
        {
            case EffectType.Punch:     return new PunchEffect();
            case EffectType.Shake:     return new ShakeEffect();
            case EffectType.FadeOut:   return new FadeOutEffect();
            case EffectType.FlyOff:    return new FlyOffEffect();
            case EffectType.FlyTo:     return new FlyToEffect();
            case EffectType.FlyToStay: return new FlyToStayEffect();
            case EffectType.Breathing: return new BreathingEffect();
            default:                   return null;
        }
    }

    /// <summary>Chạy effect (nếu type != None) trên target, trả về Coroutine đang chạy (null nếu
    /// type == None hoặc target == null) để caller tự StopCoroutine khi cần (vd Breathing).</summary>
    public static Coroutine Play(MonoBehaviour runner, RectTransform target, EffectType type, EffectParams p, System.Action onDone = null)
    {
        if (type == EffectType.None || target == null || runner == null) { onDone?.Invoke(); return null; }
        var fx = Create(type);
        if (fx == null) { onDone?.Invoke(); return null; }
        return runner.StartCoroutine(RunAndNotify(fx, target, p, runner, onDone));
    }

    static IEnumerator RunAndNotify(IVisualEffect fx, RectTransform target, EffectParams p, MonoBehaviour runner, System.Action onDone)
    {
        yield return fx.Play(target, p, runner);
        onDone?.Invoke();
    }
}
