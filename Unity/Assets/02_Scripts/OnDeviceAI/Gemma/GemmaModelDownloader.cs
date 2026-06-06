using System;
using System.IO;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Storage;
using UnityEngine;

namespace OntologyMetaverse.OnDeviceAI.Gemma
{
    /// <summary>
    /// Firebase Storage에서 Gemma 모델 파일(.task)을 디바이스 persistentDataPath로 다운로드.
    ///
    /// 동작:
    ///   1. persistentDataPath에 이미 동일 파일 존재 + 크기가 Firebase Storage 메타데이터의 size와 일치하면 스킵.
    ///   2. 아니면 GetFileAsync로 다운로드 (진행률 콜백).
    ///   3. 완료 후 GemmaOnDeviceManager.InitWithModelPath(absolutePath) 호출.
    ///
    /// Inspector:
    ///   - storagePath: Firebase Storage 안의 모델 경로 (예: "models/gemma-3n-E2B-it-int4.task")
    ///   - gemmaManager: 같은 GameObject의 GemmaOnDeviceManager 드래그
    ///
    /// 전제: FirebaseBootstrap 초기화 완료 + Firebase Auth 로그인 상태
    /// (Storage 보안 규칙에서 인증된 사용자만 read 허용한다고 가정).
    /// </summary>
    public class GemmaModelDownloader : MonoBehaviour
    {
        [Header("Firebase Storage 안의 모델 경로")]
        public string storagePath = "models/gemma-3n-E2B-it-int4.task";

        [Header("로컬 저장 파일명 (persistentDataPath 기준)")]
        public string localFileName = "gemma-3n-E2B-it-int4.task";

        [Header("다운로드 완료 후 자동 초기화할 매니저")]
        public GemmaOnDeviceManager gemmaManager;

        // 외부 UI가 구독할 수 있는 콜백
        public event Action<float> OnProgress;          // 0.0 ~ 1.0
        public event Action<string> OnComplete;         // 절대 경로
        public event Action<string> OnError;            // 에러 메시지

        public bool IsDownloading { get; private set; }
        public bool IsAlreadyLocal { get; private set; }

        void Awake()
        {
            // FirebaseBootstrap은 Auth 모듈 init만 보장. 실제 사용자 로그인은 비동기로 별도 완료됨.
            // Storage 보안 규칙이 request.auth != null 요구하므로 CurrentUser가 살아있을 때까지 대기.
            FirebaseBootstrap.RunWhenReady(WaitForAuthThenDownload);
        }

        /// <summary>
        /// Auth 로그인이 끝났는지 확인 → 끝났으면 즉시 다운로드, 아니면 StateChanged 구독해서 대기.
        /// 자동 로그인 OR 사용자 수동 로그인 둘 다 처리됨.
        /// </summary>
        private void WaitForAuthThenDownload()
        {
            var auth = FirebaseAuth.DefaultInstance;
            if (auth.CurrentUser != null)
            {
                Debug.Log($"[GemmaDownloader] 이미 로그인 상태 (uid={auth.CurrentUser.UserId}) → 다운로드 시작");
                StartDownload();
                return;
            }

            Debug.Log("[GemmaDownloader] 로그인 대기 중... StateChanged 구독");
            auth.StateChanged += OnAuthStateChanged;
        }

        private void OnAuthStateChanged(object sender, EventArgs e)
        {
            var auth = FirebaseAuth.DefaultInstance;
            if (auth.CurrentUser == null) return;       // 로그아웃 이벤트는 무시
            if (IsDownloading || IsAlreadyLocal) return; // 이미 진행 중/완료면 중복 방지

            // 한 번만 발동시키고 구독 해제 (재로그인/토큰 갱신 시 재시도 방지)
            auth.StateChanged -= OnAuthStateChanged;

            Debug.Log($"[GemmaDownloader] 로그인 완료 감지 (uid={auth.CurrentUser.UserId}) → 다운로드 시작");
            StartDownload();
        }

        void OnDestroy()
        {
            // 씬 전환 시 구독 leak 방지
            try { FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged; } catch { }
        }

        public void StartDownload()
        {
            if (IsDownloading)
            {
                Debug.LogWarning("[GemmaDownloader] 이미 다운로드 진행 중");
                return;
            }

            string localPath = Path.Combine(Application.persistentDataPath, localFileName);

            // 1. 메타데이터 조회 후 로컬 캐시 검증
            StorageReference reference = FirebaseStorage.DefaultInstance.GetReference(storagePath);
            reference.GetMetadataAsync().ContinueWithOnMainThread(metaTask =>
            {
                if (metaTask.IsFaulted)
                {
                    string msg = $"메타데이터 조회 실패: {metaTask.Exception?.Message}";
                    Debug.LogError("[GemmaDownloader] " + msg);
                    OnError?.Invoke(msg);
                    return;
                }

                long remoteSize = metaTask.Result.SizeBytes;
                Debug.Log($"[GemmaDownloader] 원격 모델 크기: {remoteSize:N0} bytes");

                if (File.Exists(localPath))
                {
                    long localSize = new FileInfo(localPath).Length;
                    if (localSize == remoteSize)
                    {
                        Debug.Log($"[GemmaDownloader] 로컬 캐시 사용: {localPath}");
                        IsAlreadyLocal = true;
                        OnProgress?.Invoke(1.0f);
                        OnComplete?.Invoke(localPath);
                        gemmaManager?.InitWithModelPath(localPath);
                        return;
                    }
                    else
                    {
                        Debug.LogWarning($"[GemmaDownloader] 로컬 크기 불일치 ({localSize} vs {remoteSize}) → 재다운로드");
                        File.Delete(localPath);
                    }
                }

                DownloadFile(reference, localPath, remoteSize);
            });
        }

        private void DownloadFile(StorageReference reference, string localPath, long expectedSize)
        {
            IsDownloading = true;
            Debug.Log($"[GemmaDownloader] 다운로드 시작 → {localPath}");

            // GetFileAsync는 진행률 콜백을 IProgress<DownloadState>로 받음.
            int lastLoggedPercent = -1;
            var progress = new System.Progress<DownloadState>(state =>
            {
                if (state.TotalByteCount > 0)
                {
                    float p = (float)state.BytesTransferred / state.TotalByteCount;
                    OnProgress?.Invoke(p);
                    // 1% 단위 로그 (한 번 찍은 percent는 다시 안 찍음)
                    int percent = Mathf.FloorToInt(p * 100);
                    if (percent != lastLoggedPercent)
                    {
                        lastLoggedPercent = percent;
                        Debug.Log($"[GemmaDownloader] 진행 {percent}% ({state.BytesTransferred:N0}/{state.TotalByteCount:N0})");
                    }
                }
            });

            reference.GetFileAsync(localPath, progress).ContinueWithOnMainThread(task =>
            {
                IsDownloading = false;

                if (task.IsFaulted)
                {
                    string msg = $"다운로드 실패: {task.Exception?.Message}";
                    Debug.LogError("[GemmaDownloader] " + msg);
                    OnError?.Invoke(msg);
                    return;
                }

                if (!File.Exists(localPath))
                {
                    OnError?.Invoke("다운로드 완료 신호 받았지만 파일 없음");
                    return;
                }

                long actualSize = new FileInfo(localPath).Length;
                if (actualSize != expectedSize)
                {
                    string msg = $"크기 불일치: 기대 {expectedSize}, 실제 {actualSize}";
                    Debug.LogError("[GemmaDownloader] " + msg);
                    OnError?.Invoke(msg);
                    return;
                }

                Debug.Log($"[GemmaDownloader] 다운로드 완료 ({actualSize:N0} bytes) → {localPath}");
                OnProgress?.Invoke(1.0f);
                OnComplete?.Invoke(localPath);

                // 매니저 자동 초기화
                gemmaManager?.InitWithModelPath(localPath);
            });
        }
    }
}
