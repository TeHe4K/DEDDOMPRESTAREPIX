using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace blago.Classes
{
    public static class DatabaseManager
    {
        private static string server = "127.0.0.1";   // OpenServerPanel
        private static string database = "elderly_care_home";      // название твоей базы
        private static string username;
        private static string password;

        private static bool loggedIn = false;
        private static bool isAdminUser = false;

        private static string BuildConnection()
        {
            return $"Server={server};Database={database};Uid={username};Pwd={password};Charset=utf8;";
        }

        public static bool Login(string user, string pass)
        {
            username = user;
            password = pass;

            try
            {
                using (var conn = new MySqlConnection(BuildConnection()))
                {
                    conn.Open();

                    // Таблица пользователей должна быть в MySQL
                    string sql = "SELECT isAdmin FROM users WHERE username=@u AND password=@p";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", password);

                        var result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            loggedIn = true;
                            isAdminUser = Convert.ToInt32(result) == 1;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static MySqlConnection CreateNewConnection()
        {
            return new MySqlConnection(BuildConnection());
        }

        public static bool IsUserLoggedIn() => loggedIn;

        public static bool IsAdmin() => isAdminUser;
    }
}
