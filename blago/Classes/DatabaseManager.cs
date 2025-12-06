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
        public static string database = "childrens_orphanage";

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
        // ДОБАВИТЬ В КЛАСС DatabaseManager:

        private static Dictionary<string, bool> _userAdminCache = new Dictionary<string, bool>();

        /// <summary>
        /// Проверяет, является ли пользователь администратором через таблицу UserAdmins
        /// </summary>
        public static bool CheckUserIsAdmin(string username)
        {
            try
            {
                // Проверяем кэш
                if (_userAdminCache.ContainsKey(username))
                    return _userAdminCache[username];

                using (SqlConnection conn = CreateNewConnection())
                {
                    conn.Open();

                    // Проверяем в таблице UserAdmins
                    string query = @"
                SELECT COUNT(*) 
                FROM UserAdmins 
                WHERE Username = @username AND IsAdmin = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        bool isAdmin = count > 0;

                        // Сохраняем в кэш
                        _userAdminCache[username] = isAdmin;

                        return isAdmin;
                    }
                }
            }
            catch (Exception)
            {
                // Если таблицы нет или ошибка, проверяем системные роли
                return CheckSystemAdminRights(username);
            }
        }

        /// <summary>
        /// Проверяет системные роли пользователя
        /// </summary>
        private static bool CheckSystemAdminRights(string username)
        {
            try
            {
                using (SqlConnection conn = CreateNewConnection())
                {
                    conn.Open();

                    // Проверяем роли базы данных
                    string query = @"
                SELECT 
                    CASE 
                        WHEN EXISTS (
                            SELECT 1 
                            FROM sys.database_role_members rm
                            JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
                            JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
                            WHERE u.name = @username 
                            AND r.name IN ('db_owner', 'db_securityadmin', 'db_accessadmin')
                        ) THEN 1
                        ELSE 0
                    END";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        object result = cmd.ExecuteScalar();
                        return result != null && Convert.ToInt32(result) == 1;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Обновляет кэш администраторов
        /// </summary>
        public static void ClearAdminCache()
        {
            _userAdminCache.Clear();
        }

        /// <summary>
        /// Устанавливает пользователя как администратора
        /// </summary>
        public static void SetUserAsAdmin(string username, bool isAdmin)
        {
            if (_userAdminCache.ContainsKey(username))
                _userAdminCache[username] = isAdmin;
            else
                _userAdminCache.Add(username, isAdmin);
        }

        // В метод Login ДОБАВИТЬ после успешной авторизации:
        // В классе DatabaseManager ВОЗВРАЩАЕМ старую версию Login
        public static bool Login(string username, string password)
        {
            try
            {
                string connectionString = BuildConnectionString(username, password);

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, является ли пользователь администратором
                    // Старый проверенный способ
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

        // Добавляем ЭТОТ метод (работает от текущего подключения)
        public static bool IsUserAdminInDatabase(string username)
        {
            try
            {
                using (SqlConnection conn = CreateNewConnection())
                {
                    conn.Open();

                    // Способ 1: Проверяем таблицу UserAdmins
                    try
                    {
                        string query = @"
                    SELECT COUNT(*) 
                    FROM UserAdmins 
                    WHERE Username = @username AND IsAdmin = 1";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@username", username);
                            int count = Convert.ToInt32(cmd.ExecuteScalar());
                            if (count > 0) return true;
                        }
                    }
                    catch
                    {
                        // Таблицы может не существовать
                    }

                    // Способ 2: Проверяем системные роли от имени текущего пользователя
                    string query2 = @"
                SELECT 
                    CASE 
                        WHEN EXISTS (
                            SELECT 1 
                            FROM sys.database_role_members rm
                            JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
                            JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
                            WHERE u.name = @username 
                            AND r.name IN ('db_owner', 'db_securityadmin', 'db_accessadmin')
                        ) THEN 1
                        ELSE 0
                    END";
                    //111

                    using (SqlCommand cmd = new SqlCommand(query2, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        object result = cmd.ExecuteScalar();
                        return result != null && Convert.ToInt32(result) == 1;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
