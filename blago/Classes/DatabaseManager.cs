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
        // Настройки подключения (можно вынести в конфиг при необходимости)
        private static string sqlServerIp = "WIN-Q9DJ17TRB0K\\DOMPRESTARELIX";
        private static string database = "childrens_orphanage";

        // Текущие учетные данные пользователя
        private static string currentUsername;
        private static string currentPassword;

        // Текущее подключение
        private static SqlConnection currentConnection;

        /// <summary>
        /// Выполняет авторизацию пользователя и сохраняет учетные данные
        /// </summary>
        /// <param name="username">Имя пользователя SQL</param>
        /// <param name="password">Пароль SQL</param>
        /// <returns>True если авторизация успешна</returns>
        //public static bool Login(string username, string password)
        //{
        //    try
        //    {
        //        string connectionString = BuildConnectionString(username, password);

        //        using (SqlConnection conn = new SqlConnection(connectionString))
        //        {
        //            conn.Open();

        //            // Сохраняем учетные данные и создаем новое подключение для дальнейшего использования
        //            currentUsername = username;
        //            currentPassword = password;
        //            currentConnection = new SqlConnection(connectionString);

        //            conn.Close();
        //            return true;
        //        }
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        /// <summary>
        /// Получает открытое подключение к базе данных
        /// </summary>
        /// <returns>Открытое SqlConnection</returns>
        public static SqlConnection GetConnection()
        {
            if (currentConnection == null)
                throw new InvalidOperationException("Пользователь не авторизован. Сначала выполните вход.");

            if (currentConnection.State != System.Data.ConnectionState.Open)
                currentConnection.Open();

            return currentConnection;
        }

        /// <summary>
        /// Создает новое подключение с текущими учетными данными
        /// </summary>
        /// <returns>Новое SqlConnection (не открытое)</returns>
        public static SqlConnection CreateNewConnection()
        {
            if (string.IsNullOrEmpty(currentUsername) || string.IsNullOrEmpty(currentPassword))
                throw new InvalidOperationException("Пользователь не авторизован. Сначала выполните вход.");

            string connectionString = BuildConnectionString(currentUsername, currentPassword);
            return new SqlConnection(connectionString);
        }

        /// <summary>
        /// Проверяет, авторизован ли пользователь
        /// </summary>
        public static bool IsUserLoggedIn()
        {
            return currentConnection != null && !string.IsNullOrEmpty(currentUsername);
        }

        /// <summary>
        /// Возвращает имя текущего пользователя
        /// </summary>
        public static string GetCurrentUsername()
        {
            return currentUsername;
        }
        public static string GetCurrentPassword()
        {
            return currentPassword;
        }

        /// <summary>
        /// Закрывает подключение и очищает учетные данные
        /// </summary>
        public static void Logout()
        {
            if (currentConnection != null)
            {
                if (currentConnection.State == System.Data.ConnectionState.Open)
                    currentConnection.Close();

                currentConnection.Dispose();
                currentConnection = null;
            }

            currentUsername = null;
            currentPassword = null;
        }

        /// <summary>
        /// Строит строку подключения
        /// </summary>
        private static string BuildConnectionString(string username, string password)
        {
            return $"Server={sqlServerIp};" +
                   $"Database={database};" +
                   $"User Id={username};" +
                   $"Password={password};" +
                   $"TrustServerCertificate=True;" +
                   $"Integrated Security=false;" +
                   $"MultipleActiveResultSets=true;" +
                   $"Network Library=DBMSSOCN;";
        }
        private static bool isAdmin;

        /// <summary>
        /// Авторизация с проверкой прав администратора через системные таблицы SQL Server
        /// </summary>
        public static bool Login(string username, string password)
        {
            try
            {
                string connectionString = BuildConnectionString(username, password);

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, является ли пользователь администратором
                    isAdmin = CheckIfAdminInSQLServer(conn, username);

                    // Сохраняем учетные данные
                    currentUsername = username;
                    currentPassword = password;
                    currentConnection = new SqlConnection(connectionString);

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Проверяет, является ли пользователь администратором через системные роли SQL Server
        /// </summary>
        private static bool CheckIfAdminInSQLServer(SqlConnection conn, string username)
        {
            try
            {
                // Способ 1: Проверяем, есть ли у пользователя роль sysadmin
                string query = @"
                SELECT 
                    CASE 
                        WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1
                        WHEN IS_SRVROLEMEMBER('securityadmin') = 1 THEN 1
                        WHEN IS_SRVROLEMEMBER('serveradmin') = 1 THEN 1
                        ELSE 0
                    END AS IsAdmin";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    object result = cmd.ExecuteScalar();
                    return result != null && Convert.ToInt32(result) == 1;
                }
            }
            catch
            {
                // Если не работает, пробуем другой способ
                return CheckDatabaseAdminRights(conn, username);
            }
        }

        /// <summary>
        /// Проверяем права администратора базы данных
        /// </summary>
        private static bool CheckDatabaseAdminRights(SqlConnection conn, string username)
        {
            try
            {
                // Проверяем, является ли пользователь владельцем базы данных
                string query = @"
                SELECT 
                    CASE 
                        WHEN IS_MEMBER('db_owner') = 1 THEN 1
                        WHEN IS_MEMBER('db_accessadmin') = 1 THEN 1
                        WHEN IS_MEMBER('db_securityadmin') = 1 THEN 1
                        ELSE 0
                    END AS IsDBA";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    object result = cmd.ExecuteScalar();
                    return result != null && Convert.ToInt32(result) == 1;
                }
            }
            catch
            {
                return false;
            }

        }

        /// <summary>
        /// Проверяет, является ли текущий пользователь администратором
        /// </summary>
        public static bool IsAdmin()
        {
            return isAdmin;
        }
    }
}
