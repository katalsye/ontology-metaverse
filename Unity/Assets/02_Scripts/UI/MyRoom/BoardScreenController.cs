using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// 4-7. BoardScreen 컨트롤러
/// 방명록 댓글 목록 + 입력/전송
/// </summary>
public class BoardScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private ScrollView commentList;
    private VisualElement emptyState;
    private TextField inputComment;

    private void OnEnable()
    {
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
        // TODO: Firestore에서 방명록 댓글 가져오기

        var comments = new List<CommentData>
        {
            new CommentData("이지은", "방 너무 예쁘다! 🏠", "10분 전"),
            new CommentData("박서준", "오늘도 열심히 했네~", "1시간 전"),
            new CommentData("최유진", "나도 이 가구 갖고 싶어", "3시간 전"),
        };

        if (comments.Count == 0)
        {
            emptyState.AddToClassList("empty-state--visible");
            commentList.style.display = DisplayStyle.None;
            return;
        }

        emptyState.RemoveFromClassList("empty-state--visible");
        commentList.style.display = DisplayStyle.Flex;

        foreach (var c in comments)
        {
            commentList.contentContainer.Add(CreateCommentCard(c));
        }
    }

    private VisualElement CreateCommentCard(CommentData data)
    {
        var card = new VisualElement();
        card.AddToClassList("comment-card");

        // 아바타
        var avatar = new VisualElement();
        avatar.AddToClassList("comment-avatar");
        var emoji = new Label("😊");
        emoji.AddToClassList("comment-avatar-emoji");
        avatar.Add(emoji);

        // 본문
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

    private void OnSendClicked()
    {
        string text = inputComment.value.Trim();
        if (string.IsNullOrEmpty(text)) return;

        Debug.Log($"[Board] 댓글 전송: {text}");

        // TODO: Firestore에 댓글 저장

        // 즉시 UI에 추가
        var myNickname = PlayerPrefs.GetString("nickname", "나");
        var newComment = new CommentData(myNickname, text, "방금 전");
        var card = CreateCommentCard(newComment);

        // 맨 위에 추가
        if (commentList.contentContainer.childCount > 0)
            commentList.contentContainer.Insert(0, card);
        else
            commentList.contentContainer.Add(card);

        // 빈 상태 해제
        emptyState.RemoveFromClassList("empty-state--visible");
        commentList.style.display = DisplayStyle.Flex;

        // 입력 초기화
        inputComment.value = "";
    }
}

public class CommentData
{
    public string nickname;
    public string text;
    public string timeAgo;

    public CommentData(string nickname, string text, string timeAgo)
    {
        this.nickname = nickname;
        this.text = text;
        this.timeAgo = timeAgo;
    }
}
