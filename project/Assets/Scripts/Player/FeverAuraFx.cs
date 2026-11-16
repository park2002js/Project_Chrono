using UnityEngine;

/// <summary>자기 각성의 시작·종료 사건에 맞춰 몸에서 피어오르는 황금 오라 재생</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterBuff))]
[DefaultExecutionOrder(40)]
public class FeverAuraFx : MonoBehaviour
{
    public ParticleSystem[] Emitters; // 각성 중 함께 재생할 몸 뒤 불길과 앞쪽 불씨
    public bool IsPlaying { get; private set; } // 마지막 각성 사건을 반영한 자기 오라의 재생 상태

    private CharacterBuff buff; // Player 수명 동안 재사용할 자기 각성 상태 원본

    /// <summary>Buff 준비 순서 뒤 자기 상태 원본과 직렬화한 입자 연결 확인</summary>
    private void Awake()
    {
        // 자기 컴포넌트만 한 번 확보하고 준비되지 않은 표시 연결은 즉시 알림
        buff = GetComponent<CharacterBuff>();
        bool ready = buff != null && Emitters != null && Emitters.Length > 0;
        if (ready) foreach (ParticleSystem emitter in Emitters) ready &= emitter != null;
        if (!ready)
        {
            Debug.LogError(name + ": Connect Fever aura emitters and CharacterBuff.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        // 재활성 중 놓친 상태를 한 번 맞춘 뒤 전환 사건만 수신
        buff.OnFeverChanged += SetAura; // 실제 각성 진입·해제와 오라 재생 연결
        SetAura(buff.IsFever);
    }

    private void OnDisable()
    {
        // Player 제거·비활성화에서 자기 표시와 구독만 정리
        if (buff != null) buff.OnFeverChanged -= SetAura; // 비활성 표시로 향하는 각성 알림 해제
        SetAura(false);
    }

    /// <summary>각성 시작은 기존 입자를 재생하고 해제는 남은 입자까지 즉시 정리</summary>
    private void SetAura(bool active)
    {
        if (active && IsPlaying) return;
        IsPlaying = active;
        if (Emitters == null) return;
        foreach (ParticleSystem emitter in Emitters)
        {
            if (emitter == null) continue;
            if (active) emitter.Play(false);
            else emitter.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
