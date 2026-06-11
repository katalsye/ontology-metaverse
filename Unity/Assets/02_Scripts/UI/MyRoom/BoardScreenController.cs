using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Linq;

/// <summary>
/// 4-7. BoardScreen 컨트롤러
/// 방명록 댓글 목록 + 입력/전송
/// Firestore: guestbooks/{ownerUid}/comments
/// </summary>
public class BoardScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private FirebaseAuth auth;
    private FirebaseFirestore db;

    private VisualElement root;
    private ScrollView commentList;
    private VisualElement emptyState;
    private TextField inputComment;

    private string ownerUid;

    private void OnEnable()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;

        ownerUid = PlayerPrefs.GetString("visiting_user_id",
            auth?.CurrentUser?.UserId ?? "");

        root = uiDocument.rootVisualElement;

        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoBack();
        root.Q<Button>("btn-send").clicked += OnSendClicked;

        commentList = root.Q<ScrollView>("comment-list");
        emptyState = root.Q("empty-state");
        inputComment = root.Q<TextField>("input-comment");

        LoadComments();
    }

    private void LoadComments()
    {
        if (string.IsNullOrEmpty(ownerUid)) return;

        db.Collection("guestbooks")
            .Document(ownerUid)
            .Collection("comments")
            .OrderByDescending("createdAt")
            .Limit(50)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("[Board] 댓글 로드 실패: " + task.Exception);
                    return;
                }

                commentList.contentContainer.Clear();

                var snapshot = task.Result;
                var docs = snapshot.Documents.ToList();

                if (docs.Count == 0)
                {
                    emptyState.AddToClassList("empty-state--visible");
                    commentList.style.display = DisplayStyle.None;
                    return;
                }

                emptyState.RemoveFromClassList("empty-state--visible");
                commentList.style.display = DisplayStyle.Flex;

                foreach (var doc in docs)
                {
                    string nickname = doc.ContainsField("authorNickname")
                        ? doc.GetValue<string>("authorNickname") : "?";
                    string text = doc.ContainsField("text")
                        ? doc.GetValue<string>("text") : "";
                    string timeAgo = doc.ContainsField("createdAt")
                        ? FormatTimeAgo(doc.GetValue<Timestamp>("createdAt")) : "";

                    commentList.contentContainer.Add(
                        CreateCommentCard(new CommentData(nickname, text, timeAgo)));
                }
            });
    }

    private void OnSendClicked()
    {
        string text = inputComment.value.Trim();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(ownerUid)) return;

        var sendBtn = root.Q<Button>("btn-send");
        sendBtn.SetEnabled(false);

        string myUid = auth?.CurrentUser?.UserId ?? "";
        string myNickname = PlayerPrefs.GetString("nickname",
            auth?.CurrentUser?.DisplayName ?? "나");

        var data = new Dictionary<string, object>
        {
            { "authorUid",      myUid },
            { "authorNickname", myNickname },
            { "text",           text },
            { "createdAt",      FieldValue.ServerTimestamp },
        };

        db.Collection("guestbooks")
            .Document(ownerUid)
            .Collection("comments")
            .AddAsync(data)
            .ContinueWithOnMainThread(task =>
            {
                sendBtn.SetEnabled(true);

                if (task.IsFaulted)
                {
                    Debug.LogError("[Board] 댓글 저장 실패: " + task.Exception);
                    return;
                }

                var card = CreateCommentCard(new CommentData(myNickname, text, "방금 전"));

                if (commentList.contentContainer.childCount > 0)
                    commentList.contentContainer.Insert(0, card);
                else
                    commentList.contentContainer.Add(card);

                emptyState.RemoveFromClassList("empty-state--visible");
                commentList.style.display = DisplayStyle.Flex;

                inputComment.value = "";
            });
    }

    private string FormatTimeAgo(Timestamp ts)
    {
        var diff = System.DateTime.UtcNow - ts.ToDateTime();

        if (diff.TotalMinutes < 1)  return "방금 전";
        if (diff.TotalHours   < 1)  return $"{(int)diff.TotalMinutes}분 전";
        if (diff.TotalDays    < 1)  return $"{(int)diff.TotalHours}시간 전";
        return $"{(int)diff.TotalDays}일 전";
    }

    private VisualElement CreateCommentCard(CommentData data)
    {
        var card = new VisualElement();
        card.AddToClassList("comment-card");

        var avatar = new VisualElement();
        avatar.AddToClassList("comment-avatar");
        var emoji = new VisualElement();
        emoji.AddToClassList("avatar-image-default");
        avatar.Add(emoji);

        var body = new VisualElement();
        body.AddToClassList("comment-body");

        var header = new VisualElement();
        header.AddToClassList("comment-header");

        var name = new Label(data.nickname);
        name.AddToClassList("comment-name");

        var time = new Label(data.timeAgo);
        time.AddToClassList("comment-time");

        header.Add(name);
        header.Add(time);

        var text = new Label(data.text);
        text.AddToClassList("comment-text");

        body.Add(header);
        body.Add(text);

        card.Add(avatar);
        card.Add(body);

        return card;
    }
}