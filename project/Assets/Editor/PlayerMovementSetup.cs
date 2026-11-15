using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>기존 참조를 보존하며 F-P01 Player 구성과 검증용 벽을 기존 프리팹에 연결</summary>
public static class PlayerMovementSetup
{
    /// <summary>명령행에서 프리팹 연결 후 기존 Play Mode 검사와 이동 검사를 함께 시작</summary>
    public static void RunChecks()
    {
        Run();
        StartupFlowChecks.Run();
    }

    /// <summary>기존 씬을 다시 만들지 않고 Player·Map 프리팹의 F-P01 연결만 갱신</summary>
    [MenuItem("Tools/Chrono/Connect Player Movement")]
    public static void Run()
    {
        // 기존 Player 프리팹의 이동·방향·무기 구성 연결
        string playerPath = "Assets/Prefabs/Startup/Player.prefab";
        GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            Ensure<PlayerBodyController>(player);
            CharacterRendering rendering = Ensure<CharacterRendering>(player);
            AttackController attack = Ensure<AttackController>(player);
            Rigidbody2D body = Ensure<Rigidbody2D>(player);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Ensure<BoxCollider2D>(player).size = new Vector2(0.65f, 1f);
            rendering.BodySprite = player.transform.Find("Visual").GetComponent<SpriteRenderer>();
            rendering.FacingMarker = Marker(player.transform, "FacingMarker", new Vector2(0.15f, 0.15f), Color.white, 3);
            Transform pivot = Child(player.transform, "WeaponPivot");
            Transform motion = Child(pivot, "WeaponMotion");
            Transform weapon = pivot.Find("WeaponVisual");
            if (weapon != null) weapon.SetParent(motion, false);
            else weapon = motion.Find("WeaponVisual");
            if (weapon == null)
            {
                weapon = Marker(motion, "WeaponVisual", new Vector2(0.7f, 0.1f), new Color(1f, 0.9f, 0.45f), 2);
                weapon.localPosition = new Vector3(0.55f, 0f, 0f);
            }
            attack.WeaponPivot = pivot;
            attack.WeaponMotion = motion;
            attack.WeaponAnimator = Ensure<Animator>(pivot.gameObject);
            attack.WeaponAnimator.runtimeAnimatorController = PlayerWeaponAnimationSetup.EnsureAssets();
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }

        // 기존 Map 프리팹에 충돌 검사용 외벽 추가
        string mapPath = "Assets/Prefabs/Startup/Map.prefab";
        GameObject map = PrefabUtility.LoadPrefabContents(mapPath);
        try
        {
            Wall(map.transform, "WallLeft", new Vector2(-9.25f, 0f), new Vector2(0.5f, 8.5f));
            Wall(map.transform, "WallRight", new Vector2(9.25f, 0f), new Vector2(0.5f, 8.5f));
            Wall(map.transform, "WallTop", new Vector2(0f, 4.25f), new Vector2(18.5f, 0.5f));
            Wall(map.transform, "WallBottom", new Vector2(0f, -4.25f), new Vector2(18.5f, 0.5f));
            PrefabUtility.SaveAsPrefabAsset(map, mapPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(map);
        }

        // 변경 에셋 저장 후 독립 입력·HUD·카메라 연결
        AssetDatabase.SaveAssets();
        ConnectSharedControllers();
        Debug.Log("[PlayerMovementSetup] Player input/body/facing/weapon and four blocking walls connected.");
    }

    /// <summary>기존 Player에서 입력을 분리하고 MainWorld에 입력·HUD·카메라를 각각 한 번 배치</summary>
    [MenuItem("Tools/Chrono/Connect Shared Controllers")]
    public static void ConnectSharedControllers()
    {
        // 미저장 편집 보호와 작업 종료 후 돌아갈 씬 보관
        Scene originalScene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1 || originalScene.isDirty || string.IsNullOrEmpty(originalScene.path))
            throw new System.InvalidOperationException("Controller setup requires one saved, unmodified scene outside Play Mode.");
        string originalPath = originalScene.path;

        // Player 수명과 분리할 입력 프리팹 생성 또는 갱신
        string inputPath = "Assets/Prefabs/Startup/PlayerInput.prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(inputPath) != null;
        GameObject inputRoot = exists ? PrefabUtility.LoadPrefabContents(inputPath) : new GameObject("PlayerInput");
        try
        {
            PlayerInputController controller = Ensure<PlayerInputController>(inputRoot);
            ConnectInputActions(inputRoot, controller);
            PrefabUtility.SaveAsPrefabAsset(inputRoot, inputPath);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(inputRoot);
            else Object.DestroyImmediate(inputRoot);
        }

        // 기존 Player 프리팹에서 중복 입력 컴포넌트 제거
        string playerPath = "Assets/Prefabs/Startup/Player.prefab";
        GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            PlayerInputController oldController = player.GetComponent<PlayerInputController>();
            if (oldController != null) Object.DestroyImmediate(oldController);
            PlayerInput oldInput = player.GetComponent<PlayerInput>();
            if (oldInput != null) Object.DestroyImmediate(oldInput);
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }

        // MainWorld에 공유 객체를 한 번씩 배치하고 원래 씬 복원
        try
        {
            Scene world = EditorSceneManager.OpenScene("Assets/Scenes/MainWorld.unity");
            foreach (Camera templateCamera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (templateCamera.GetComponent<GameCamera>() == null)
                    templateCamera.gameObject.SetActive(false);
            if (Object.FindFirstObjectByType<UIController>() == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Startup/GameUI.prefab"));
            if (Object.FindFirstObjectByType<GameCamera>() == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Startup/WorldCamera.prefab"));
            if (Object.FindFirstObjectByType<PlayerInputController>() == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(inputPath));
            EditorSceneManager.SaveScene(world);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorSceneManager.OpenScene(originalPath);
        }
        Debug.Log("[PlayerMovementSetup] Independent MainWorld input, UI and GameCamera connected.");
    }

    /// <summary>Input Actions의 이동·커서·대시·공격을 PlayerInput UnityEvents로 연결</summary>
    public static void ConnectInputActions(GameObject player, PlayerInputController controller)
    {
        // 액션 에셋·기본 맵·장치 조합 설정
        PlayerInput input = Ensure<PlayerInput>(player);
        input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        input.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
        input.defaultActionMap = "Player";
        input.defaultControlScheme = "Keyboard&Mouse";
        input.neverAutoSwitchControlSchemes = true;

        // 입력 콜백을 프리팹에 보존할 UnityEvent로 연결
        PlayerInput.ActionEvent move = new PlayerInput.ActionEvent(input.actions.FindAction("Player/Move", true));
        PlayerInput.ActionEvent aim = new PlayerInput.ActionEvent(input.actions.FindAction("Player/Look", true));
        PlayerInput.ActionEvent dash = new PlayerInput.ActionEvent(input.actions.FindAction("Player/Dash", true));
        PlayerInput.ActionEvent attack = new PlayerInput.ActionEvent(input.actions.FindAction("Player/Attack", true));
        PlayerInput.ActionEvent draw = new PlayerInput.ActionEvent(input.actions.FindAction("Player/Draw", true));
        PlayerInput.ActionEvent slash = new PlayerInput.ActionEvent(input.actions.FindAction("Player/Slash", true));
        PlayerInput.ActionEvent drawAim = new PlayerInput.ActionEvent(input.actions.FindAction("Draw/Look", true));
        PlayerInput.ActionEvent drawCancel = new PlayerInput.ActionEvent(input.actions.FindAction("Draw/Toggle", true));
        PlayerInput.ActionEvent drawPoint = new PlayerInput.ActionEvent(input.actions.FindAction("Draw/Point", true));
        PlayerInput.ActionEvent drawReset = new PlayerInput.ActionEvent(input.actions.FindAction("Draw/Reset", true));
        PlayerInput.ActionEvent drawConfirm = new PlayerInput.ActionEvent(input.actions.FindAction("Draw/Confirm", true));
        UnityEventTools.AddPersistentListener(move, controller.HandleOnMove); // 이동 액션 값을 입력 컨트롤러에 전달
        UnityEventTools.AddPersistentListener(aim, controller.HandleOnAim); // 커서 액션 값을 입력 컨트롤러에 전달
        UnityEventTools.AddPersistentListener(dash, controller.HandleOnDash); // 대시 누름 요청을 입력 컨트롤러에 전달
        UnityEventTools.AddPersistentListener(attack, controller.HandleOnAttack); // 공격 버튼 유지·해제를 입력 컨트롤러에 전달
        UnityEventTools.AddPersistentListener(draw, controller.HandleOnDraw); // 일반 Shift를 Draw 진입으로 전달
        UnityEventTools.AddPersistentListener(slash, controller.HandleOnSlash); // 일반 F의 수정 상호작용 우선·Slash 공통 누름 전달
        UnityEventTools.AddPersistentListener(drawAim, controller.HandleOnAim); // Draw 커서를 일반 이동 없이 전달
        UnityEventTools.AddPersistentListener(drawCancel, controller.HandleOnDraw); // Draw Shift를 편집 취소로 전달
        UnityEventTools.AddPersistentListener(drawPoint, controller.HandleOnDrawPoint); // Draw 클릭을 점 배치로 전달
        UnityEventTools.AddPersistentListener(drawReset, controller.HandleOnDrawReset); // Draw R을 점 초기화로 전달
        UnityEventTools.AddPersistentListener(drawConfirm, controller.HandleOnDrawConfirm); // Draw F를 경로 확정으로 전달
        input.actionEvents = new[] { move, aim, dash, attack, draw, slash, drawAim, drawCancel, drawPoint, drawReset, drawConfirm };
    }

    /// <summary>이미 있는 Component를 재사용하며 없는 구성만 추가</summary>
    private static T Ensure<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    /// <summary>같은 이름의 기존 자식을 재사용해 중복 생성 방지</summary>
    private static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }
        return child;
    }

    /// <summary>최종 아트 전에 방향·무기·벽을 확인할 Unity 기본 Sprite를 연결</summary>
    private static Transform Marker(Transform parent, string name, Vector2 size, Color color, int order)
    {
        Transform marker = Child(parent, name);
        SpriteRenderer renderer = Ensure<SpriteRenderer>(marker.gameObject);
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        renderer.color = color;
        renderer.sortingOrder = order;
        marker.localScale = new Vector3(size.x / renderer.sprite.bounds.size.x, size.y / renderer.sprite.bounds.size.y, 1f);
        return marker;
    }

    /// <summary>현재 임시 방의 가장자리에 표시와 실제 이동 차단 Collider를 함께 연결</summary>
    private static void Wall(Transform parent, string name, Vector2 position, Vector2 size)
    {
        Transform wall = Child(parent, name);
        wall.localPosition = position;
        Ensure<BoxCollider2D>(wall.gameObject).size = size;
        Marker(wall, "Visual", size, new Color(0.25f, 0.3f, 0.4f), 0);
    }
}
