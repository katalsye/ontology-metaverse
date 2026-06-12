using UnityEngine;

/// <summary>
/// BootScene에서 결정한 진입 화면을 MainScene의 ScreenManager에 전달.
/// </summary>
public static class AppRouter
{
    public static string EntryScreen { get; private set; } = "onboarding";

    public static void ResolveEntryScreen()
    {
        var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        bool isProperlyLoggedIn = user != null && !user.IsAnonymous;
        bool hasOnboarding = PlayerPrefs.HasKey("onboarding_complete");
        bool hasNickname = PlayerPrefs.HasKey("nickname");
        bool setupComplete = hasOnboarding && hasNickname;

        if (isProperlyLoggedIn)
            EntryScreen = "myroom";
        else if (setupComplete)
            EntryScreen = "login";
        else
            EntryScreen = "onboarding";
    }
}
