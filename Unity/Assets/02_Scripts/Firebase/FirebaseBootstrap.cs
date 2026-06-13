using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firebase 의존성 초기화를 앱 전체에서 단 한 번만 수행하는 진입점.
/// 모든 Firebase Manager는 Awake()에서 RunWhenReady(Init)을 호출해
/// Firebase 초기화가 끝난 뒤에야 FirebaseAuth/FirebaseFirestore.DefaultInstance에 접근하도록 한다.
///
/// 호출 순서가 보장되지 않는 MonoBehaviour Start() / Awake() 경합 문제 해결용
/// (이전: UserManager.Start()가 GoogleFirebaseLogin의 CheckAndFix보다 먼저 실행되어
///   InvalidOperationException 발생).
///
/// 사용 패턴:
///   void Awake()
///   {
///       FirebaseBootstrap.RunWhenReady(Init);
///   }
///   private void Init()
///   {
///       auth = FirebaseAuth.DefaultInstance;
///       db   = FirebaseFirestore.DefaultInstance;
///   }
/// </summary>
public static class FirebaseBootstrap
{
    public static bool IsReady { get; private set; }

    private static bool initStarted;
    private static readonly List<Action> pendingCallbacks = new List<Action>();

    /// <summary>
    /// Firebase 초기화 완료 후 callback을 실행한다.
    /// 이미 초기화가 끝났으면 즉시 동기 호출하고, 아니면 큐에 쌓아둔다.
    /// 메인 스레드에서만 호출할 것.
    /// </summary>
    public static void RunWhenReady(Action callback)
    {
        if (callback == null) return;

        if (IsReady)
        {
            callback();
            return;
        }

        pendingCallbacks.Add(callback);
        EnsureInit();
    }

    /// <summary>
    /// 초기화를 시작한다. 호출이 여러 번 들어와도 실제 CheckAndFixDependenciesAsync는 한 번만 실행됨.
    /// </summary>
    private static void EnsureInit()
    {
        if (initStarted) return;
        initStarted = true;

        Debug.Log("[FirebaseBootstrap] CheckAndFixDependenciesAsync 시작");
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("[FirebaseBootstrap] 초기화 실패: " + task.Exception);
                Crashlytics.LogException(task.Exception);
                initStarted = false; // 재시도 허용
                return;
            }

            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError("[FirebaseBootstrap] 의존성 미충족: " + task.Result);
                Crashlytics.LogException(new Exception("Firebase 의존성 미충족: " + task.Result));
                initStarted = false; // 재시도 허용
                return;
            }

            IsReady = true;

            // 오프라인 → 온라인 sync: 다른 콜백이 Firestore 인스턴스를 사용하기 전에
            // (사용 후에는 PersistenceEnabled 변경 시 InvalidOperationException 발생) 가장 먼저 설정.
            // Editor에서 Domain Reload 끈 채로 Play 재시작하면 이전 세션의 Firestore 인스턴스가
            // 그대로 살아있어 "이미 사용됨" 예외가 나는데, 이 경우 설정은 첫 세션에 이미 적용된
            // 상태이므로 무해함 — 경고만 남기고 진행한다.
            try
            {
                FirebaseFirestoreSettings settings = FirebaseFirestore.DefaultInstance.Settings;
                settings.PersistenceEnabled = true;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogWarning("[FirebaseBootstrap] Firestore 설정 변경 불가(이미 사용 중인 인스턴스 — 무해함): " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[FirebaseBootstrap] Firestore 설정 실패: " + ex);
            }

            Debug.Log($"[FirebaseBootstrap] 초기화 완료, 대기 콜백 {pendingCallbacks.Count}건 실행");

            // 콜백 처리 중 RunWhenReady가 재귀 호출되어도 안전하도록 스왑
            var toRun = new List<Action>(pendingCallbacks);
            pendingCallbacks.Clear();

            foreach (var cb in toRun)
            {
                try { cb(); }
                catch (Exception ex)
                {
                    Debug.LogError("[FirebaseBootstrap] 콜백 예외: " + ex);
                    Crashlytics.LogException(ex);
                }
            }
        });
    }

    /// <summary>
    /// 도메인 리로드가 꺼져있는 Unity 환경에서 Play 재시작 시 static 상태를 리셋한다.
    /// (Project Settings > Editor > Enter Play Mode Options 옵션 대응)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsReady = false;
        initStarted = false;
        pendingCallbacks.Clear();
    }
}
