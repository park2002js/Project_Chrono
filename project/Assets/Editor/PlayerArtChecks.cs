using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>최종 Player 시트·방향별 대시 잔상·사망 문구 순서의 실제 Play Mode 검사</summary>
public static class PlayerArtChecks
{
    /// <summary>연결된 세션 Player에서 아트 재생과 생명주기 순서를 확인</summary>
    public static IEnumerator Run(Action<string> passed)
    {
        GameManager manager = GameManager.Instance;
        PlayerCharacter player = manager.CurrentPlayer;
        CharacterRendering rendering = player.GetComponent<CharacterRendering>();
        PlayerSpriteAnimationSet animationSet = rendering.AnimationSet;
        AttackController attack = player.GetComponent<AttackController>();
        PlayerSpawner spawner = player.Spawner;
        PlayerInputController input = PlayerInputController.Instance;
        UI_Death view = UIController.Instance.GetComponent<UI_Death>();
        bool blocked = input.BlockInput;
        int deathCount = SaveManager.Current.Get<int>("DeathCount");
        float health = player.HP;
        TilemapRenderer[] tiles = manager.Stage.Map.GetComponentsInChildren<TilemapRenderer>(true);
        Material[] tileMaterials = new Material[tiles.Length];
        for (int i = 0; i < tiles.Length; i++) tileMaterials[i] = tiles[i].sharedMaterial;
        Material playerMaterial = rendering.BodySprite.sharedMaterial;
        try
        {
            // 실제 시트 규격·최신 시간과 첫 정지 자세가 적용된 프리팹 계약 확인
            Require(animationSet != null && animationSet.GetState(PlayerSpriteAnimationState.IdleLeftFront).Frames.Length == 3
                && animationSet.GetState(PlayerSpriteAnimationState.IdleLeftBack).Frames.Length == 4
                && animationSet.GetState(PlayerSpriteAnimationState.MoveLeftFront).Frames.Length == 6
                && animationSet.GetState(PlayerSpriteAnimationState.MoveLeftBack).Frames.Length == 6
                && animationSet.GetState(PlayerSpriteAnimationState.Death).Frames.Length == 6
                && animationSet.GetState(PlayerSpriteAnimationState.Spawn).Frames.Length == 6,
                "The final eight Player sheets must provide their complete animation frames.");
            Require(Mathf.Approximately(animationSet.GetState(PlayerSpriteAnimationState.IdleLeftFront).FramesPerSecond, 5f)
                && Mathf.Approximately(animationSet.GetState(PlayerSpriteAnimationState.MoveLeftFront).FramesPerSecond, 10f)
                && rendering.BodySprite.sprite.pixelsPerUnit == 16f && rendering.WeaponSprite.sprite.rect.width == 7f
                && rendering.WeaponSprite.sprite.rect.height == 29f && attack.WeaponAnimator != null
                && attack.WeaponAnimator.runtimeAnimatorController != null && attack.WeaponMotion != null,
                "Player Sprite states and weapon Animator must use the connected art and clips.");
            player.ResetMotion();
            input.BlockInput = true;
            int first = rendering.FrameIndex;
            yield return Until(() => rendering.FrameIndex != first, "idle Sprite advance"); // 연결한 정지 시트의 실제 다음 프레임 대기
            passed("Player art uses complete final sheets at 16 PPU and advances idle at configured 0.2-second frame intervals");

            // 실제 허용 API가 이동 전에 자세를 고정하고 조준 변경에도 같은 대시 Sprite 유지
            Vector2[] directions = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) };
            PlayerSpriteAnimationState[] dashStates = { PlayerSpriteAnimationState.DashLeftFront, PlayerSpriteAnimationState.DashRightFront,
                PlayerSpriteAnimationState.DashLeftBack, PlayerSpriteAnimationState.DashRightBack };
            for (int index = 0; index < directions.Length; index++)
            {
                player.ResetMotion();
                player.SetMoveInput(directions[index]);
                Require(player.ApplyDash(), "A ready Player must accept each directional dash.");
                Sprite pose = animationSet.GetState(dashStates[index]).Frames[0];
                Require(rendering.BodySprite.sprite == pose && rendering.FacingIndex == index
                    && rendering.BodySprite.flipX == (index % 2 == 1), "Dash pose must match movement before its first displacement.");
                rendering.SetFacing(-directions[index]);
                Require(rendering.BodySprite.sprite == pose && rendering.FacingIndex == index, "Cursor changes must not replace the active dash pose.");
                yield return null;
                Require(rendering.ActiveAfterimages > 0, "Directional dash must leave a reusable Sprite afterimage.");
                yield return Until(() => !player.IsDashing, "dash completion"); // 이동 완료 뒤 다음 방향의 독립 대시 준비
            }
            player.ResetMotion();
            yield return Until(() => rendering.ActiveAfterimages == 0, "afterimage fade"); // 마지막 잔상 수명이 끝나 실제 표시가 꺼질 때까지 대기
            passed("Four actual dash directions select fixed mirrored poses before movement, leave Sprite afterimages and expire them");

            // 실제 사망은 전반 모션·빠른 전복·엎드림을 지난 뒤 문구와 같은 객체 부활 진행
            Require(player.ApplyDamage(new DamageInstance(null, player.HP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false)),
                "A ready Player must accept the lifecycle validation damage.");
            Require(spawner.IsRespawning && rendering.IsDeathAnimation && rendering.FrameIndex == 0 && !view.MessageVisible
                && !rendering.WeaponSprite.enabled, "Death must begin with frame one and hidden text/weapon.");
            Require(tiles.Length > 0 && rendering.BodySprite.sharedMaterial == playerMaterial, "The integrated world must have Tilemaps and preserve the Player material.");
            for (int i = 0; i < tiles.Length; i++)
                Require(tiles[i].sharedMaterial == view.GrayscaleMaterial, "Death must desaturate every environment Tilemap as well as world Sprites.");
            yield return Until(() => rendering.FrameIndex >= 1, "slow death frames"); // 첫 느린 자세가 실제 시간으로 넘어갈 때까지 대기
            Require(rendering.LifecycleElapsed >= 0.2f && !view.MessageVisible, "Death's first pose must remain slow before later collapse.");
            yield return Until(() => rendering.FrameIndex == 5, "fallen pose"); // 빠른 전복을 지나 최종 엎드림 프레임 대기
            Require(!view.MessageVisible && rendering.BodySprite.sprite == animationSet.GetState(PlayerSpriteAnimationState.Death).Frames[5] && !rendering.BodySprite.flipX,
                "The final fixed-direction fallen pose must precede the death message.");
            yield return Until(() => view.MessageVisible, "death message after hold"); // 최종 자세 유지가 끝난 뒤 문구가 열릴 때까지 대기
            Require(player.IsDead && rendering.FrameIndex == 5, "Death text must keep the Player in the fallen pose.");
            yield return Until(() => spawner.IsSpawning, "checkpoint spawn animation"); // 동일 인스턴스의 체크포인트 복귀·기상 시작 대기
            Require(!player.IsDead && rendering.IsLifecycleAnimation && !rendering.IsDeathAnimation && !view.MessageVisible
                && Vector2.Distance(player.transform.position, spawner.SpawnPoint.position) < 0.01f,
                "Revival must start the fixed-direction Spawn sheet at the cached checkpoint.");
            yield return Until(() => !spawner.IsRespawning && !spawner.IsSpawning, "spawn completion"); // 기상 마지막 자세와 입력 복구 완료 대기
            Require(manager.CurrentPlayer == player && !rendering.IsLifecycleAnimation && rendering.WeaponSprite.enabled
                && !view.IsVisible && !manager.IsDeathPaused && SaveManager.Current.Get<int>("DeathCount") == deathCount + 1,
                "One death must restore the same Player, sword and game services after Spawn completes.");
            for (int i = 0; i < tiles.Length; i++)
                Require(tiles[i].sharedMaterial == tileMaterials[i], "Revival must restore each Tilemap's original material.");
            passed("Real death plays slow then fast frames, holds fallen before text, and revives the same Player through Spawn at its checkpoint");
        }
        finally
        {
            // 검사 실패나 완료 뒤 입력·생존·저장 횟수를 원상 복구
            spawner.CancelLifecycle();
            if (player.IsDead) player.Revive();
            player.HP = health;
            player.ResetMotion();
            spawner.playerSpawn();
            input.BlockInput = blocked;
            SaveManager.Current.Set("DeathCount", deathCount);
            SaveManager.Save();
        }
    }

    /// <summary>실제 시간의 제한을 두고 다음 Play Mode 프레임에서 결과 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = Time.realtimeSinceStartupAsDouble + 6d;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new InvalidOperationException("Timed out waiting for " + description);
            yield return null;
        }
    }

    /// <summary>통합 실행기에 실패 원인을 전달할 최소 계약 검사</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
