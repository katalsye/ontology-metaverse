using UnityEngine;
using TMPro;

public class AuthTestUI : MonoBehaviour
{
    public TMP_InputField emailField;
    public TMP_InputField passwordField;
    public EmailAuthManager authManager;

    public void OnRegisterButton()
    {
        authManager.Register(emailField.text, passwordField.text);
    }

    public void OnLoginButton()
    {
        authManager.Login(emailField.text, passwordField.text);
    }

    public void OnLogoutButton()
    {
        authManager.Logout();
    }
}