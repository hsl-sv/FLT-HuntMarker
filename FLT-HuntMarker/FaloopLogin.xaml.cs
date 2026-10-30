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

        public FaloopLogin(string username)
        {
            InitializeComponent();

            textUsername.Text = username ?? string.Empty;
        }

        private void buttonLogin_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(Username) || String.IsNullOrEmpty(Password))
                return;

            if (checkboxRemember.IsChecked == true)
                Save(Username, Password);
            else
                Clear();

            DialogResult = true;
        }

        // Saved account, null if none
        public static (string username, string password)? Load()
        {
            try
            {
                if (!File.Exists(CONFIG.FALOOP_FILE))
                    return null;

                string[] lines = File.ReadAllLines(CONFIG.FALOOP_FILE);
                byte[] password = ProtectedData.Unprotect(Convert.FromBase64String(lines[1]), null, DataProtectionScope.CurrentUser);

                return (lines[0], Encoding.UTF8.GetString(password));
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

        private static void Save(string username, string password)
        {
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);

            File.WriteAllLines(CONFIG.FALOOP_FILE, new[] { username, Convert.ToBase64String(encrypted) });
        }
    }
}
