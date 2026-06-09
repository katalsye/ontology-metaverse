/// <summary>
/// 방명록 댓글 데이터 모델
/// </summary>
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