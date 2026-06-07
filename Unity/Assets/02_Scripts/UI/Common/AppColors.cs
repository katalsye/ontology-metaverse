using UnityEngine;

/// <summary>
/// AppColors — C# 스크립트에서 사용하는 컬러 상수
/// USS 토큰과 동일한 값. USS에서 처리 못하는 경우에만 사용.
/// </summary>
public static class AppColors
{
    // ── 배경 ──
    public static readonly Color32 Bg        = new Color32(0xFC, 0xF5, 0xEB, 0xFF);
    public static readonly Color32 BgSoft    = new Color32(0xF9, 0xE6, 0xDC, 0xFF);
    public static readonly Color32 BgWarm    = new Color32(0xF6, 0xDF, 0xCD, 0xFF);
    public static readonly Color32 Card      = new Color32(0xFF, 0xFC, 0xF9, 0xFF);
    public static readonly Color32 CardSoft  = new Color32(0xF9, 0xF4, 0xED, 0xFF);

    // ── 텍스트 ──
    public static readonly Color32 Ink       = new Color32(0x31, 0x26, 0x20, 0xFF);
    public static readonly Color32 InkMuted  = new Color32(0x7A, 0x6E, 0x68, 0xFF);
    public static readonly Color32 InkFaint  = new Color32(0xAB, 0xA2, 0x9E, 0xFF);

    // ── 액센트 ──
    public static readonly Color32 Rose      = new Color32(0xF1, 0x87, 0x85, 0xFF);
    public static readonly Color32 RoseSoft  = new Color32(0xFF, 0xD8, 0xD3, 0xFF);
    public static readonly Color32 Mint      = new Color32(0x92, 0xD5, 0xB7, 0xFF);
    public static readonly Color32 MintSoft  = new Color32(0xD3, 0xF4, 0xE4, 0xFF);
    public static readonly Color32 Butter    = new Color32(0xF5, 0xD2, 0x89, 0xFF);
    public static readonly Color32 ButterSoft= new Color32(0xFC, 0xEC, 0xCC, 0xFF);
    public static readonly Color32 Lav       = new Color32(0xBA, 0xAE, 0xE2, 0xFF);
    public static readonly Color32 LavSoft   = new Color32(0xE8, 0xE3, 0xFD, 0xFF);
    public static readonly Color32 Sky       = new Color32(0x94, 0xCD, 0xE9, 0xFF);
    public static readonly Color32 SkySoft   = new Color32(0xD7, 0xEF, 0xFB, 0xFF);

    // ── 기타 ──
    public static readonly Color32 Line      = new Color32(0xDE, 0xD5, 0xD0, 0xFF);
    public static readonly Color32 LineFaint = new Color32(0xEC, 0xE6, 0xE3, 0xFF);
    public static readonly Color32 Red       = new Color32(0xE8, 0x58, 0x54, 0xFF);
    public static readonly Color   Overlay   = new Color(0.157f, 0.110f, 0.047f, 0.55f);
}
