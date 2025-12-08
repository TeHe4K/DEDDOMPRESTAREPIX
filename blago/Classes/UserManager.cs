using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace blago.Classes
{
    public static class UserManager
    {
        // ============================
        // MODELS
        // ============================

        public class User
        {
            public int UserId { get; set; }
            public string Username { get; set; }
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

        // ============================
        // SQL HELPERS
        // ============================

        private static int ExecuteNonQuery(SqlConnection conn, string sql, params SqlParameter[] p)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                if (p != null) cmd.Parameters.AddRange(p);
                return cmd.ExecuteNonQuery();
            }
        }

        private static object ExecuteScalar(SqlConnection conn, string sql, params SqlParameter[] p)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                if (p != null) cmd.Parameters.AddRange(p);
                return cmd.ExecuteScalar();
            }
        }

        private static SqlDataReader ExecuteReader(SqlConnection conn, string sql, params SqlParameter[] p)
        {
            SqlCommand cmd = new SqlCommand(sql, conn);
            if (p != null) cmd.Parameters.AddRange(p);
            return cmd.ExecuteReader();
        }

        // ============================
        // BASIC USER OPERATIONS
        // ============================

        public static int GetUserIdByUsername(string username)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                object result = ExecuteScalar(conn,
                    "SELECT UserId FROM db_Users WHERE Username=@u",
                    new SqlParameter("@u", username));

                if (result == null)
                    throw new Exception("UserId не найден для: " + username);

                return Convert.ToInt32(result);
            }
        }

        public static List<User> GetAllUsers()
        {
            List<User> list = new List<User>();

            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                using (SqlDataReader r = ExecuteReader(conn,
                    @"SELECT UserId, Username, FullName, IsActive, CreatedDate, SqlLoginName, DatabaseUserName 
                      FROM db_Users ORDER BY Username"))
                {
                    while (r.Read())
                    {
                        list.Add(new User
                        {
                            UserId = (int)r["UserId"],
                            Username = r["Username"].ToString(),
                            FullName = r["FullName"].ToString(),
                            IsActive = (bool)r["IsActive"],
                            CreatedDate = (DateTime)r["CreatedDate"],
                            SqlLoginName = r["SqlLoginName"].ToString(),
                            DatabaseUserName = r["DatabaseUserName"].ToString()
                        });
                    }
                }
            }

            return list;
        }

        public static User GetUserById(int userId)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                using (SqlDataReader r = ExecuteReader(conn,
                    @"SELECT UserId, Username, FullName, IsActive, CreatedDate, SqlLoginName, DatabaseUserName
                      FROM db_Users WHERE UserId=@id",
                    new SqlParameter("@id", userId)))
                {
                    if (r.Read())
                    {
                        return new User
                        {
                            UserId = (int)r["UserId"],
                            Username = r["Username"].ToString(),
                            FullName = r["FullName"].ToString(),
                            IsActive = (bool)r["IsActive"],
                            CreatedDate = (DateTime)r["CreatedDate"],
                            SqlLoginName = r["SqlLoginName"].ToString(),
                            DatabaseUserName = r["DatabaseUserName"].ToString()
                        };
                    }
                    return null;
                }
            }
        }

        public static bool UpdateUser(int userId, string fullName, bool isActive)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                return ExecuteNonQuery(conn,
                    @"UPDATE db_Users SET FullName=@f, IsActive=@a WHERE UserId=@id",
                    new SqlParameter("@f", fullName),
                    new SqlParameter("@a", isActive),
                    new SqlParameter("@id", userId)) > 0;
            }
        }

        // ============================
        // PASSWORD + SECURITY
        // ============================

        public static bool IsValidSqlPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8) return false;

            return Regex.IsMatch(password, "[A-Z]") &&
                   Regex.IsMatch(password, "[a-z]") &&
                   Regex.IsMatch(password, @"\d") &&
                   Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>/?]");
        }

        public static string GenerateSecurePassword()
        {
            const string U = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string L = "abcdefghijklmnopqrstuvwxyz";
            const string D = "0123456789";
            const string S = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            Random rnd = new Random();
            StringBuilder b = new StringBuilder();

            b.Append(U[rnd.Next(U.Length)]);
            b.Append(L[rnd.Next(L.Length)]);
            b.Append(D[rnd.Next(D.Length)]);
            b.Append(S[rnd.Next(S.Length)]);

            string all = U + L + D + S;

            while (b.Length < 12)
                b.Append(all[rnd.Next(all.Length)]);

            return new string(b.ToString().OrderBy(c => rnd.Next()).ToArray());
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        // ============================
        // LOGIN + DATABASE USER OPERATIONS
        // ============================

        public static bool SqlLoginExists(string login)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                object r = ExecuteScalar(conn,
                    @"USE master; SELECT COUNT(*) FROM sys.server_principals 
                      WHERE name=@n AND type IN ('S','U')",
                    new SqlParameter("@n", login));
                return Convert.ToInt32(r) > 0;
            }
        }

        public static bool DatabaseUserExists(string name)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                object r = ExecuteScalar(conn,
                    @"SELECT COUNT(*) FROM sys.database_principals 
                      WHERE name=@n AND type IN ('S','U')",
                    new SqlParameter("@n", name));
                return Convert.ToInt32(r) > 0;
            }
        }

        public static bool CreateSqlServerLogin(string login, string password)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                if (SqlLoginExists(login)) return true;

                string q = $"CREATE LOGIN [{login}] WITH PASSWORD='{password.Replace("'", "''")}'";
                ExecuteNonQuery(conn, q);
                return true;
            }
        }

        public static bool CreateDatabaseUser(string login)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                if (DatabaseUserExists(login)) return true;

                string sql = $@"
                    CREATE USER [{login}] FOR LOGIN [{login}];
                    ALTER ROLE [db_datareader] ADD MEMBER [{login}];";

                ExecuteNonQuery(conn, sql);
                return true;
            }
        }

        public static bool DeleteSqlServerUser(string login)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string sql = $@"
                    IF EXISTS (SELECT * FROM sys.database_principals WHERE name='{login}')
                        DROP USER [{login}];

                    USE master;
                    IF EXISTS (SELECT * FROM sys.server_principals WHERE name='{login}')
                        DROP LOGIN [{login}];";

                ExecuteNonQuery(conn, sql);
                return true;
            }
        }

        // ============================
        // CREATE USER FULL PIPELINE
        // ============================

        public static bool CreateUserWithSqlLogin(string username, string password, string fullName, out string finalPassword)
        {
            if (!IsValidSqlPassword(password))
                password = GenerateSecurePassword();

            finalPassword = password;

            CreateSqlServerLogin(username, password);
            CreateDatabaseUser(username);

            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                return ExecuteNonQuery(conn,
                    @"INSERT INTO db_Users(Username, PasswordHash, FullName, SqlLoginName, DatabaseUserName)
                      VALUES(@u,@p,@f,@l,@d)",
                    new SqlParameter("@u", username),
                    new SqlParameter("@p", HashPassword(password)),
                    new SqlParameter("@f", fullName),
                    new SqlParameter("@l", username),
                    new SqlParameter("@d", username)) > 0;
            }
        }

        // ============================
        // DELETE USER (TABLE + SQL)
        // ============================

        public static bool DeleteUser(int userId)
        {
            return DeleteUserWithSqlLogin(userId);
        }

        public static bool DeleteUserWithSqlLogin(int userId)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string username = null;

                using (SqlDataReader r = ExecuteReader(conn,
                    "SELECT Username FROM db_Users WHERE UserId=@id",
                    new SqlParameter("@id", userId)))
                {
                    if (r.Read())
                        username = r["Username"].ToString();
                }

                if (username == null) return false;

                ExecuteNonQuery(conn,
                    "DELETE FROM db_Users WHERE UserId=@id",
                    new SqlParameter("@id", userId));

                DeleteSqlServerUser(username);
                return true;
            }
        }

        // ============================
        // USER PERMISSIONS
        // ============================

        public static List<TablePermission> GetAllTablePermissions(int userId)
        {
            List<TablePermission> list = new List<TablePermission>();

            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                // Получаем ВСЕ таблицы
                List<string> tables = new List<string>();
                using (SqlDataReader r = ExecuteReader(conn,
                    @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
                      WHERE TABLE_TYPE='BASE TABLE'
                      AND TABLE_NAME NOT LIKE 'sys%'
                      AND TABLE_NAME NOT LIKE 'MS%'
                      AND TABLE_NAME NOT IN ('db_Users','UserTablePermissions')
                      ORDER BY TABLE_NAME"))
                {
                    while (r.Read())
                        tables.Add(r["TABLE_NAME"].ToString());
                }

                // Получаем права
                foreach (string t in tables)
                {
                    TablePermission p = new TablePermission { TableName = t };

                    using (SqlDataReader r = ExecuteReader(conn,
                        @"SELECT CanView, CanEdit, CanDelete, CanAdd 
                          FROM UserTablePermissions 
                          WHERE UserId=@u AND TableName=@t",
                        new SqlParameter("@u", userId),
                        new SqlParameter("@t", t)))
                    {
                        if (r.Read())
                        {
                            p.CanView = (bool)r["CanView"];
                            p.CanEdit = (bool)r["CanEdit"];
                            p.CanDelete = (bool)r["CanDelete"];
                            p.CanAdd = (bool)r["CanAdd"];
                        }
                    }

                    list.Add(p);
                }
            }

            return list;
        }

        public static bool SaveUserTablePermission(int userId, TablePermission p)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string sql =
@"IF EXISTS (SELECT 1 FROM UserTablePermissions WHERE UserId=@u AND TableName=@t)
    UPDATE UserTablePermissions 
        SET CanView=@v, CanEdit=@e, CanDelete=@d, CanAdd=@a
    WHERE UserId=@u AND TableName=@t
ELSE
    INSERT INTO UserTablePermissions (UserId, TableName, CanView, CanEdit, CanDelete, CanAdd)
    VALUES (@u,@t,@v,@e,@d,@a)";

                ExecuteNonQuery(conn, sql,
                    new SqlParameter("@u", userId),
                    new SqlParameter("@t", p.TableName),
                    new SqlParameter("@v", p.CanView),
                    new SqlParameter("@e", p.CanEdit),
                    new SqlParameter("@d", p.CanDelete),
                    new SqlParameter("@a", p.CanAdd));

                return true;
            }
        }

        public static void ApplyTablePermission(string username, TablePermission p)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string sql =
                    (p.CanView ? "GRANT SELECT" : "REVOKE SELECT") +
                    $" ON [{p.TableName}] TO [{username}];" +

                    (p.CanAdd ? "GRANT INSERT" : "REVOKE INSERT") +
                    $" ON [{p.TableName}] TO [{username}];" +

                    (p.CanEdit ? "GRANT UPDATE" : "REVOKE UPDATE") +
                    $" ON [{p.TableName}] TO [{username}];" +

                    (p.CanDelete ? "GRANT DELETE" : "REVOKE DELETE") +
                    $" ON [{p.TableName}] TO [{username}];";

                ExecuteNonQuery(conn, sql);
            }
        }

        // ============================
        // ADMIN TABLE (LOCAL)
        // ============================

        public static bool IsAdminInLocalTable(string username)
        {
            CreateAdminTableIfNotExists();

            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                object r = ExecuteScalar(conn,
                    "SELECT COUNT(*) FROM UserAdmins WHERE Username=@u",
                    new SqlParameter("@u", username));
                return Convert.ToInt32(r) > 0;
            }
        }

        public static bool AddToAdmins(string username, string grantedBy = "system")
        {
            CreateAdminTableIfNotExists();

            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string sql =
@"IF NOT EXISTS (SELECT * FROM UserAdmins WHERE Username=@u)
    INSERT INTO UserAdmins (Username, GrantedBy) VALUES (@u,@g)
ELSE
    UPDATE UserAdmins SET IsAdmin=1, GrantedDate=GETDATE(), GrantedBy=@g
    WHERE Username=@u";

                ExecuteNonQuery(conn, sql,
                    new SqlParameter("@u", username),
                    new SqlParameter("@g", grantedBy));
            }

            return true;
        }

        private static void CreateAdminTableIfNotExists()
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string sql =
@"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='UserAdmins')
BEGIN
    CREATE TABLE UserAdmins(
        Id INT PRIMARY KEY IDENTITY(1,1),
        Username NVARCHAR(128) UNIQUE NOT NULL,
        IsAdmin BIT DEFAULT 1,
        GrantedDate DATETIME DEFAULT GETDATE(),
        GrantedBy NVARCHAR(128)
    );
    INSERT INTO UserAdmins(Username) VALUES ('admin');
END";

                ExecuteNonQuery(conn, sql);
            }
        }

        // ============================
        // DATABASE ROLES
        // ============================

        public static bool GrantDatabaseRole(string username, string role)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string sql =
$@"ALTER ROLE [{role}] ADD MEMBER [{username}];";

                ExecuteNonQuery(conn, sql);
                return true;
            }
        }

        public static bool RevokeDatabaseRole(string username, string role)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                string sql =
$@"ALTER ROLE [{role}] DROP MEMBER [{username}];";

                ExecuteNonQuery(conn, sql);
                return true;
            }
        }

        public static List<string> GetDatabaseRoles()
        {
            List<string> list = new List<string>();

            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                using (SqlDataReader r = ExecuteReader(conn,
                    @"SELECT name FROM sys.database_principals 
                      WHERE type='R' AND is_fixed_role=1 ORDER BY name"))
                {
                    while (r.Read())
                        list.Add(r["name"].ToString());
                }
            }

            if (list.Count == 0)
            {
                list.AddRange(new[]
                {
                    "db_datareader","db_datawriter","db_ddladmin",
                    "db_accessadmin","db_securityadmin",
                    "db_backupoperator","db_owner"
                });
            }

            return list;
        }

        // ============================
        // ADMIN CHECK
        // ============================

        public static bool IsUserAdmin(string username)
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    object r = ExecuteScalar(conn,
                        @"SELECT COUNT(*) FROM sys.database_role_members rm
                          JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
                          JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
                          WHERE u.name=@u AND r.name IN ('db_owner','db_securityadmin','db_accessadmin')",
                        new SqlParameter("@u", username));

                    if (Convert.ToInt32(r) > 0) return true;
                }
            }
            catch { }

            return IsAdminInLocalTable(username);
        }
    }
}
