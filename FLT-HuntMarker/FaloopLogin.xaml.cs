using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace FLT_HuntMarker
{
    /// <summary>
    /// Faloop account input, saved with DPAPI (current Windows user only)
    /// </summary>
    public partial class FaloopLogin : Window
    {
        public string Username => textUsername.Text;
        public string Password => textPassword.Password;
        public string Session => textSession.Password.Trim().Trim('"');

        public FaloopLogin(string username)
        {
            InitializeComponent();

            textUsername.Text = username ?? string.Empty;
        }

        private void buttonLogin_Click(object sender, RoutedEventArgs e)
        {
            // id / pw or session id
            if ((String.IsNullOrEmpty(Username) || String.IsNullOrEmpty(Password)) && String.IsNullOrEmpty(Session))
                return;

            if (checkboxRemember.IsChecked == true)
                Save(Username, Password, Session);
            else
                Clear();

            DialogResult = true;
        }

        // Saved account, null if none
        public static (string username, string password, string session)? Load()
        {
            try
            {
                if (!File.Exists(CONFIG.FALOOP_FILE))
                    return null;

                string[] lines = File.ReadAllLines(CONFIG.FALOOP_FILE);

                return (lines[0], Decrypt(lines[1]), lines.Length > 2 ? Decrypt(lines[2]) : string.Empty);
            }
            catch
            {
                return null;
            }
        }

        public static void Clear()
        {
            if (File.Exists(CONFIG.FALOOP_FILE))
                File.Delete(CONFIG.FALOOP_FILE);
        }

        // Session may be renewed by server, keep it up to date
        public static void Save(string username, string password, string session)
        {
            File.WriteAllLines(CONFIG.FALOOP_FILE, new[] { username, Encrypt(password), Encrypt(session) });
        }

        private static string Encrypt(string text)
        {
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(text ?? string.Empty), null, DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encrypted);
        }

        private static string Decrypt(string text)
        {
            byte[] decrypted = ProtectedData.Unprotect(Convert.FromBase64String(text), null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
