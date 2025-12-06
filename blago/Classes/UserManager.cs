using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace blago.Classes
{
    public class UserManager
    {
        public class User
        {
            public int UserId { get; set; }
            public string Username { get; set; }
            public string Password { get; set; } // Добавляем поле для пароля
            public string FullName { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
            public string SqlLoginName { get; set; }
            public string DatabaseUserName { get; set; }
        }

        public class TablePermission
        {
            public string TableName { get; set; }
            public bool CanView { get; set; }
            public bool CanEdit { get; set; }
            public bool CanDelete { get; set; }
            public bool CanAdd { get; set; }
        }

        // ========== ДОБАВЛЯЮ НЕДОСТАЮЩИЕ МЕТОДЫ ==========

        // 1. Метод GetAllTablePermissions
        public static int GetUserIdByUsername(string username)
{
    using (SqlConnection conn = DatabaseManager.CreateNewConnection())
    {
        conn.Open();
        string sql = "SELECT UserId FROM db_Users WHERE Username = @u";

        SqlCommand cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", username);

        object result = cmd.ExecuteScalar();

        if (result == null)
            throw new Exception("UserId не найден для пользователя: " + username);

        return Convert.ToInt32(result);
    }
}


        public static List<TablePermission> GetAllTablePermissions(int userId)
        {
            List<TablePermission> permissions = new List<TablePermission>();
           

            try
            {
                // Сначала получаем все таблицы (исключая системные и таблицы пользователей)
                string tablesQuery = @"
                    SELECT TABLE_NAME 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    AND TABLE_NAME NOT LIKE 'sys%'
                    AND TABLE_NAME NOT LIKE 'MS%'
                    AND TABLE_NAME NOT IN ('db_Users', 'UserTablePermissions')
                    ORDER BY TABLE_NAME";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();


                    // Получаем список всех таблиц
                    List<string> allTables = new List<string>();
                    using (SqlCommand cmd = new SqlCommand(tablesQuery, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                allTables.Add(reader["TABLE_NAME"].ToString());
                            }
                        }
                    }

                    // Для каждой таблицы получаем права пользователя
                    foreach (string tableName in allTables)
                    {
                        // Получаем права из таблицы UserTablePermissions
                        string permissionQuery = @"
                            SELECT CanView, CanEdit, CanDelete, CanAdd 
                            FROM UserTablePermissions 
                            WHERE UserId = @userId AND TableName = @tableName";

                        TablePermission permission = new TablePermission
                        {
                            TableName = tableName,
                            CanView = false,
                            CanEdit = false,
                            CanDelete = false,
                            CanAdd = false
                        };

                        using (SqlCommand cmd = new SqlCommand(permissionQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@tableName", tableName);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    permission.CanView = Convert.ToBoolean(reader["CanView"]);
                                    permission.CanEdit = Convert.ToBoolean(reader["CanEdit"]);
                                    permission.CanDelete = Convert.ToBoolean(reader["CanDelete"]);
                                    permission.CanAdd = Convert.ToBoolean(reader["CanAdd"]);
                                }
                            }
                        }

                        permissions.Add(permission);
                    }
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки возвращаем пустой список
                Console.WriteLine($"Ошибка при получении прав доступа: {ex.Message}");
            }

            return permissions;
        }

        // 2. Метод DeleteUser (альтернативное название для DeleteUserWithSqlLogin)
        public static bool DeleteUser(int userId)
        {
            return DeleteUserWithSqlLogin(userId);
        }

        // 3. Метод SaveUserTablePermission
        public static bool SaveUserTablePermission(int userId, TablePermission permission)
        {
            try
            {
                string query = @"
                    IF EXISTS (SELECT 1 FROM UserTablePermissions WHERE UserId = @userId AND TableName = @tableName)
                    BEGIN
                        UPDATE UserTablePermissions 
                        SET CanView = @canView, 
                            CanEdit = @canEdit, 
                            CanDelete = @canDelete, 
                            CanAdd = @canAdd
                        WHERE UserId = @userId AND TableName = @tableName
                    END
                    ELSE
                    BEGIN
                        INSERT INTO UserTablePermissions (UserId, TableName, CanView, CanEdit, CanDelete, CanAdd)
                        VALUES (@userId, @tableName, @canView, @canEdit, @canDelete, @canAdd)
                    END";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@tableName", permission.TableName);
                        cmd.Parameters.AddWithValue("@canView", permission.CanView);
                        cmd.Parameters.AddWithValue("@canEdit", permission.CanEdit);
                        cmd.Parameters.AddWithValue("@canDelete", permission.CanDelete);
                        cmd.Parameters.AddWithValue("@canAdd", permission.CanAdd);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0 || true; // Возвращаем true даже если запись уже существует
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении прав доступа: {ex.Message}");
                return false;
            }
        }

        // ========== ОСТАЛЬНЫЕ МЕТОДЫ (ваш существующий код) ==========

        // Проверка сложности пароля для SQL Server
        public static bool IsValidSqlPassword(string password)
        {
            // SQL Server требует пароли определенной сложности
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            // Проверяем наличие хотя бы одного символа каждого типа
            bool hasUpper = Regex.IsMatch(password, "[A-Z]");
            bool hasLower = Regex.IsMatch(password, "[a-z]");
            bool hasDigit = Regex.IsMatch(password, @"\d");
            bool hasSpecial = Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        // Генерация безопасного пароля для SQL Server
        public static string GenerateSecurePassword()
        {
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            Random random = new Random();
            StringBuilder password = new StringBuilder();

            // Гарантируем наличие каждого типа символов
            password.Append(upper[random.Next(upper.Length)]);
            password.Append(lower[random.Next(lower.Length)]);
            password.Append(digits[random.Next(digits.Length)]);
            password.Append(special[random.Next(special.Length)]);

            // Добавляем случайные символы до длины 12
            string allChars = upper + lower + digits + special;
            for (int i = 4; i < 12; i++)
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Перемешиваем символы
            return new string(password.ToString().ToCharArray().OrderBy(x => random.Next()).ToArray());
        }

        // Хэширование пароля для хранения в нашей таблице
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Создание SQL Server Login
        public static bool CreateSqlServerLogin(string loginName, string password, bool isWindowsAuth = false)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    if (isWindowsAuth)
                    {
                        // Для Windows аутентификации
                        string query = $"CREATE LOGIN [{loginName}] FROM WINDOWS";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Для SQL Server аутентификации
                        string query = $"CREATE LOGIN [{loginName}] WITH PASSWORD = '{password.Replace("'", "''")}'";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    return true;
                }
            }
            catch (SqlException ex)
            {
                // Уже существует
                if (ex.Number == 15025 || ex.Message.Contains("already exists"))
                    return true;

                throw new Exception($"Не удалось создать SQL Login: {ex.Message}");
            }
        }

        // Создание Database User из Login
        public static bool CreateDatabaseUser(string loginName, string databaseUserName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(databaseUserName))
                    databaseUserName = loginName;

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    // Создаем пользователя в текущей базе данных
                    string query = $@"
                        USE [{conn.Database}];
                        CREATE USER [{databaseUserName}] FOR LOGIN [{loginName}];
                        ALTER ROLE [db_datareader] ADD MEMBER [{databaseUserName}];";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (SqlException ex)
            {
                // Уже существует
                if (ex.Number == 15023 || ex.Message.Contains("already exists"))
                    return true;

                throw new Exception($"Не удалось создать Database User: {ex.Message}");
            }
        }

        // Удаление SQL Server Login и Database User
        public static bool DeleteSqlServerUser(string loginName)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    // Получаем имя базы данных
                    string dbName = conn.Database;

                    // Удаляем пользователя из базы данных
                    string dropUserQuery = $@"
                        USE [{dbName}];
                        IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '{loginName}')
                        BEGIN
                            DROP USER [{loginName}];
                        END";

                    // Удаляем логин
                    string dropLoginQuery = $@"
                        USE [master];
                        IF EXISTS (SELECT * FROM sys.server_principals WHERE name = '{loginName}')
                        BEGIN
                            DROP LOGIN [{loginName}];
                        END";

                    using (SqlCommand cmd = new SqlCommand(dropUserQuery + dropLoginQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось удалить SQL пользователя: {ex.Message}");
            }
        }

        // Проверка существования SQL Login
        public static bool SqlLoginExists(string loginName)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string query = @"
                        USE [master];
                        SELECT COUNT(*) 
                        FROM sys.server_principals 
                        WHERE name = @loginName AND type IN ('S', 'U')";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@loginName", loginName);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Проверка существования Database User
        public static bool DatabaseUserExists(string userName)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string query = @"
                        SELECT COUNT(*) 
                        FROM sys.database_principals 
                        WHERE name = @userName AND type IN ('S', 'U')";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userName", userName);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Создание пользователя с SQL Login и Database User
        public static bool CreateUserWithSqlLogin(string username, string password, string fullName, out string generatedPassword)
        {
            generatedPassword = null;

            try
            {
                // Генерируем безопасный пароль, если не предоставлен
                if (string.IsNullOrEmpty(password) || !IsValidSqlPassword(password))
                {
                    generatedPassword = GenerateSecurePassword();
                    password = generatedPassword;
                }
                else
                {
                    generatedPassword = password;
                }

                // Создаем SQL Server Login
                if (!SqlLoginExists(username))
                {
                    CreateSqlServerLogin(username, password);
                }

                // Создаем Database User
                if (!DatabaseUserExists(username))
                {
                    CreateDatabaseUser(username);
                }

                // Сохраняем в нашу таблицу Users
                string query = @"
                    INSERT INTO db_Users (Username, PasswordHash, FullName, SqlLoginName, DatabaseUserName) 
                    VALUES (@username, @passwordHash, @fullName, @sqlLoginName, @databaseUserName)";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@passwordHash", HashPassword(password));
                        cmd.Parameters.AddWithValue("@fullName", fullName);
                        cmd.Parameters.AddWithValue("@sqlLoginName", username);
                        cmd.Parameters.AddWithValue("@databaseUserName", username);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при создании пользователя: {ex.Message}");
            }
        }

        // Получение всех пользователей
        public static List<User> GetAllUsers()
        {
            List<User> users = new List<User>();

            try
            {
                string query = @"
                    SELECT UserId, Username, FullName, IsActive, CreatedDate, SqlLoginName, DatabaseUserName 
                    FROM db_Users 
                    ORDER BY Username";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new User
                                {
                                    UserId = Convert.ToInt32(reader["UserId"]),
                                    Username = reader["Username"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    IsActive = Convert.ToBoolean(reader["IsActive"]),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                                    SqlLoginName = reader["SqlLoginName"]?.ToString(),
                                    DatabaseUserName = reader["DatabaseUserName"]?.ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке пользователей: {ex.Message}");
            }

            return users;
        }

        // Удаление пользователя вместе с SQL Login
        public static bool DeleteUserWithSqlLogin(int userId)
        {
            try
            {
                // Сначала получаем информацию о пользователе
                string getQuery = "SELECT Username, SqlLoginName FROM db_Users WHERE UserId = @userId";
                string username = "";
                string sqlLoginName = "";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(getQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                username = reader["Username"].ToString();
                                sqlLoginName = reader["SqlLoginName"]?.ToString();
                            }
                        }
                    }

                    // Удаляем из нашей таблицы
                    string deleteQuery = "DELETE FROM db_Users WHERE UserId = @userId";
                    using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Удаляем SQL Login и Database User
                            if (!string.IsNullOrEmpty(sqlLoginName))
                            {
                                DeleteSqlServerUser(sqlLoginName);
                            }
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при удалении пользователя: {ex.Message}");
            }
        }

        // Изменение пароля SQL Login
        public static bool ChangeSqlLoginPassword(string loginName, string newPassword)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string query = $@"
                        USE [master];
                        ALTER LOGIN [{loginName}] WITH PASSWORD = '{newPassword.Replace("'", "''")}'";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Обновляем хэш в нашей таблице
                    string updateQuery = @"
                        UPDATE db_Users 
                        SET PasswordHash = @passwordHash 
                        WHERE Username = @username OR SqlLoginName = @loginName";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@passwordHash", HashPassword(newPassword));
                        cmd.Parameters.AddWithValue("@username", loginName);
                        cmd.Parameters.AddWithValue("@loginName", loginName);
                        cmd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при изменении пароля: {ex.Message}");
            }
        }
        // 1111
        public static bool GrantDatabaseRole(string userName, string roleName)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string query = $@"
                        USE [{conn.Database}];
                        ALTER ROLE [{roleName}] ADD MEMBER [{userName}];";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при назначении роли: {ex.Message}");
            }
        }

        // Отзыв ролей у пользователя
        public static bool RevokeDatabaseRole(string userName, string roleName)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string query = $@"
                        USE [{conn.Database}];
                        ALTER ROLE [{roleName}] DROP MEMBER [{userName}];";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при отзыве роли: {ex.Message}");
            }
        }

        // Получение списка доступных ролей
        public static List<string> GetDatabaseRoles()
        {
            List<string> roles = new List<string>();

            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string query = @"
                        SELECT name 
                        FROM sys.database_principals 
                        WHERE type = 'R' 
                        AND is_fixed_role = 1
                        ORDER BY name";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                roles.Add(reader["name"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Возвращаем основные роли по умолчанию
                roles = new List<string>
                {
                    "db_datareader",
                    "db_datawriter",
                    "db_ddladmin",
                    "db_accessadmin",
                    "db_securityadmin",
                    "db_backupoperator",
                    "db_owner"
                };
            }

            return roles;
        }

        // ========== ДОПОЛНИТЕЛЬНЫЕ МЕТОДЫ ДЛЯ УДОБСТВА ==========

        // Проверка, существует ли пользователь в нашей таблице
        public static bool UserExistsInDb(string username)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM db_Users WHERE Username = @username";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Получение пользователя по ID
        public static User GetUserById(int userId)
        {
            try
            {
                string query = @"
                    SELECT UserId, Username, FullName, IsActive, CreatedDate, SqlLoginName, DatabaseUserName 
                    FROM db_Users 
                    WHERE UserId = @userId";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new User
                                {
                                    UserId = Convert.ToInt32(reader["UserId"]),
                                    Username = reader["Username"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    IsActive = Convert.ToBoolean(reader["IsActive"]),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                                    SqlLoginName = reader["SqlLoginName"]?.ToString(),
                                    DatabaseUserName = reader["DatabaseUserName"]?.ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении пользователя: {ex.Message}");
            }

            return null;
        }

        // Обновление информации о пользователе
        public static bool UpdateUser(int userId, string fullName, bool isActive)
        {
            try
            {
                string query = @"
                    UPDATE db_Users 
                    SET FullName = @fullName, 
                        IsActive = @isActive 
                    WHERE UserId = @userId";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@fullName", fullName);
                        cmd.Parameters.AddWithValue("@isActive", isActive);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обновлении пользователя: {ex.Message}");
            }
        }
        // Метод с повторными попытками назначения роли
        public static bool GrantDatabaseRoleWithRetry(string userName, string roleName, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return GrantDatabaseRole(userName, roleName);
                }
                catch (SqlException ex) when (ex.Number == 15151) // User does not exist
                {
                    if (i == maxRetries - 1) throw;
                    System.Threading.Thread.Sleep(500 * (i + 1)); // Ждем перед повторной попыткой
                }
            }
            return false;
        }

        // Метод для получения имени сервера
        private static string GetServerName()
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    return conn.DataSource;
                }
            }
            catch
            {
                return "localhost";
            }
        }
        // Проверка, является ли пользователь администратором
        public static bool IsUserAdmin(string username)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    // Проверяем роли пользователя в базе данных
                    string query = @"
                SELECT COUNT(*) 
                FROM sys.database_role_members rm
                JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
                JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
                WHERE u.name = @username 
                AND r.name IN ('db_owner', 'db_securityadmin', 'db_accessadmin')";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception)
            {
                // Если не можем проверить роли, проверяем по нашей таблице
                return IsAdminInLocalTable(username);
            }
        }

        // Проверка в локальной таблице
        public static bool IsAdminInLocalTable(string username)
        {
            try
            {
                // Создадим таблицу для хранения администраторов
                CreateAdminTableIfNotExists();

                string query = "SELECT COUNT(*) FROM UserAdmins WHERE Username = @username";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Создание таблицы для администраторов
        private static void CreateAdminTableIfNotExists()
        {
            try
            {
                string query = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserAdmins')
            BEGIN
                CREATE TABLE UserAdmins (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    Username NVARCHAR(128) UNIQUE NOT NULL,
                    IsAdmin BIT DEFAULT 1,
                    GrantedDate DATETIME DEFAULT GETDATE(),
                    GrantedBy NVARCHAR(128)
                );
                
                -- Добавляем администратора по умолчанию
                INSERT INTO UserAdmins (Username) VALUES ('admin');
            END";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибку создания таблицы
            }
        }

        // Добавление пользователя в список администраторов
        public static bool AddToAdmins(string username, string grantedBy = "system")
        {
            try
            {
                CreateAdminTableIfNotExists();

                string query = @"
            IF NOT EXISTS (SELECT * FROM UserAdmins WHERE Username = @username)
            BEGIN
                INSERT INTO UserAdmins (Username, GrantedBy) 
                VALUES (@username, @grantedBy);
            END
            ELSE
            BEGIN
                UPDATE UserAdmins 
                SET IsAdmin = 1, GrantedDate = GETDATE(), GrantedBy = @grantedBy
                WHERE Username = @username;
            END";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@grantedBy", grantedBy);
                        cmd.ExecuteNonQuery();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении администратора: {ex.Message}");
                return false;
            }
        }
        public static void ApplyTablePermission(string username, TablePermission p)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                // SELECT
                string selectSql = p.CanView
                    ? $"GRANT SELECT ON [{p.TableName}] TO [{username}];"
                    : $"REVOKE SELECT ON [{p.TableName}] FROM [{username}];";

                // INSERT
                string insertSql = p.CanAdd
                    ? $"GRANT INSERT ON [{p.TableName}] TO [{username}];"
                    : $"REVOKE INSERT ON [{p.TableName}] FROM [{username}];";

                // UPDATE
                string updateSql = p.CanEdit
                    ? $"GRANT UPDATE ON [{p.TableName}] TO [{username}];"
                    : $"REVOKE UPDATE ON [{p.TableName}] FROM [{username}];";

                // DELETE
                string deleteSql = p.CanDelete
                    ? $"GRANT DELETE ON [{p.TableName}] TO [{username}];"
                    : $"REVOKE DELETE ON [{p.TableName}] FROM [{username}];";

                using (SqlCommand cmd = new SqlCommand(
                     selectSql + insertSql + updateSql + deleteSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

    }
}
