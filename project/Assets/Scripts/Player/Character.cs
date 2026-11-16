using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Player와 Boss의 공통 수치·생존 상태와 최종 피해 허용 판단</summary>
public class Character : MonoBehaviour
{
    public float HP = 100f; // 현재 체력과 HUD 표시의 기준값
    public float maxHP = 100f; // 체력 초기화의 상한과 HUD의 최대 체력 표시값
    public bool IsDead; // 현재 캐릭터의 사망 여부
    public bool IsInitialized; // Spawner의 체력·생존 상태 초기화 완료 여부
    public float basicSP = 4f; // 최초 이동속도와 이동 모션의 기본 재생 배율 기준값
    public float SP = 4f; // 현재 이동에 적용할 초당 월드 거리
    public float basicAD = 10f; // 기본 공격력의 초기 튜닝값
    public float AD = 10f; // 일반 공격 피해를 계산할 현재 공격력
    public float basicAS = 1f; // 각성 해제·부활에서 되돌릴 기본 공격속도
    public float AS = 1f; // 공격 애니메이션·판정 시점·공격 사이 간격의 진행 배율
    public bool IsCutScene // 컷씬 상태와 진입 순간의 행동 중단 통지를 함께 제공
    {
        get => isCutScene;
        set
        {
            if (isCutScene == value) return;
            isCutScene = value;
            if (value) OnCutScene?.Invoke();
        }
    }
    public virtual bool IsInvulnerable { get => isInvulnerable; set => isInvulnerable = value; } // 외부 보호와 파생 행동의 전면 무적을 합칠 조회 창구
    public virtual bool IsDodge => false; // 파생 캐릭터가 제공할 Hitbox 피해 회피 상태
    public virtual bool IsHitProtected => false; // 피격 뒤 Hitbox 피해만 막는 보호 상태이며 대시·전면 무적과 구분
    public event Action<DamageInstance> OnDamaged; // 체력이 감소한 뒤 표시·후속 처리를 알릴 이벤트
    public event Action OnDie; // 체력이 처음 0에 도달했을 때 행동을 즉시 정리할 이벤트
    public event Action OnRevive; // 기본 체력·생존 상태 복원 뒤 구독자가 자기 수명을 초기화할 사건
    public event Action OnCutScene; // 컷씬에 진입한 순간 진행 중 행동의 중단을 알릴 이벤트

    [SerializeField, FormerlySerializedAs("IsCutScene")] private bool isCutScene; // 기존 씬·프리팹의 컷씬 상태를 보존할 실제 값
    [SerializeField, FormerlySerializedAs("IsInvulnerable")] private bool isInvulnerable; // 외부에서 요청한 전면 무적의 원본
    private CharacterBuff buffController; // 자기 프리팹 수명 동안 효과 요청을 전달할 컴포넌트

    /// <summary>파생 캐릭터 초기화 전에 자기 효과 컴포넌트를 한 번 확보</summary>
    protected virtual void Awake()
    {
        buffController = GetComponent<CharacterBuff>();
    }

    /// <summary>Spawner가 전달한 체력을 적용하고 초기화 완료 상태 설정</summary>
    public void InitializeHealth(float health)
    {
        // 최대 체력과 저장 체력의 유효 범위 확인
        if (float.IsNaN(maxHP) || float.IsInfinity(maxHP) || maxHP <= 0f)
            throw new InvalidOperationException($"{name}: maxHP must be a positive finite value.");
        if (float.IsNaN(health) || float.IsInfinity(health) || health < 0f)
            throw new InvalidOperationException($"{name}: saved HP must be a nonnegative finite value.");

        // 체력 상한을 적용하고 생존·준비 상태 확정
        HP = Mathf.Min(health, maxHP);
        IsDead = HP <= 0f;
        IsInitialized = true;
    }

    /// <summary>현재 수신 가능 상태를 확인하고 체력을 감소시킨 뒤 피해·사망 통지</summary>
    public virtual bool ApplyDamage(DamageInstance damage)
    {
        // 사망·전면 무적과 Hitbox 전용 회피를 구분해 피해 허용 확인
        if (!isActiveAndEnabled || !IsInitialized || IsDead || IsCutScene || IsInvulnerable || (damage.IsHitboxDamage && (IsDodge || IsHitProtected)))
            return false;
        if (float.IsNaN(damage.Amount) || float.IsInfinity(damage.Amount) || damage.Amount <= 0f || float.IsNaN(HP) || float.IsInfinity(HP) || HP <= 0f)
            return false;

        // 체력을 먼저 확정해 수신 이벤트에서 최신 생존 상태를 조회
        HP = Mathf.Max(0f, HP - damage.Amount);
        bool diedThisHit = HP <= 0f;
        IsDead = diedThisHit;
        OnDamaged?.Invoke(damage);
        if (diedThisHit)
            OnDie?.Invoke();
        return true;
    }

    /// <summary>생존한 캐릭터를 최대 체력까지 회복</summary>
    public bool Heal(float amount)
    {
        if (!isActiveAndEnabled || !IsInitialized || IsDead || !IsFinite(amount) || amount <= 0f || !IsFinite(HP) || !IsFinite(maxHP))
            return false;
        HP = Mathf.Min(maxHP, HP + amount);
        return true;
    }

    /// <summary>자기 효과 컴포넌트에 정해진 각성 효과 요청</summary>
    public bool ApplyBuff(BuffType type)
    {
        return buffController != null && buffController.Apply(type);
    }

    /// <summary>자기 효과와 같은 처리에서 예약된 재적용 제거</summary>
    public void RemoveBuff(BuffType type)
    {
        if (buffController != null) buffController.Remove(type);
    }

    /// <summary>같은 인스턴스의 기본 생존·수치 복원 뒤 부활만 통지</summary>
    public bool Revive()
    {
        if (!isActiveAndEnabled || !IsDead) return false;
        InitializeHealth(maxHP);
        AD = basicAD;
        AS = basicAS;
        SP = basicSP;
        IsInvulnerable = false;
        IsCutScene = false;
        OnRevive?.Invoke();
        return true;
    }

    /// <summary>체력 계산에 사용할 값이 유한한지 확인</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
