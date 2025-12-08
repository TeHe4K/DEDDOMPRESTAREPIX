using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace blago.Classes
{
    public static class DatabaseManager
    {
        private static string sqlServerIp = "DESKTOP-MB0MPSO\\SQLEXPRESS";
        public static string database { get; set; }

        private static string currentUsername;
        private static string currentPassword;
        private static SqlConnection currentConnection;

        private static bool isAdmin;
        private static Dictionary<string, bool> _adminCache = new Dictionary<string, bool>();

        public static bool Login(string username, string password)
        {
            try
            {
                string connStr = BuildConnectionString(username, password);

                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    isAdmin = CheckSqlAdmin(conn);
                }

                currentUsername = username;
                currentPassword = password;
                currentConnection = new SqlConnection(connStr);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Logout()
        {
            if (currentConnection != null)
            {
                if (currentConnection.State == ConnectionState.Open)
                    currentConnection.Close();

                currentConnection.Dispose();
                currentConnection = null;
            }

            currentUsername = null;
            currentPassword = null;
        }

        public static SqlConnection GetConnection()
        {
            if (currentConnection == null)
                throw new InvalidOperationException("Нет активной сессии");

            if (currentConnection.State != ConnectionState.Open)
                currentConnection.Open();

            return currentConnection;
        }

        public static SqlConnection CreateNewConnection()
        {
            if (currentUsername == null)
                throw new InvalidOperationException("Сначала авторизуйтесь");

            return new SqlConnection(BuildConnectionString(currentUsername, currentPassword));
        }

        public static bool IsUserLoggedIn()
        {
            return currentConnection != null && currentUsername != null;
        }

        public static string GetCurrentUsername() => currentUsername;
        public static string GetCurrentPassword() => currentPassword;
        public static bool IsAdmin() => isAdmin;

        private static string BuildConnectionString(string username, string password)
        {
            return $"Server={sqlServerIp};Database={database};User Id={username};Password={password};" +
                   $"TrustServerCertificate=True;Integrated Security=False;MultipleActiveResultSets=True;Network Library=DBMSSOCN;";
        }

        private static bool CheckSqlAdmin(SqlConnection conn)
        {
            try
            {
                string query = @"
                    SELECT 
                        CASE
                            WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1
                            WHEN IS_SRVROLEMEMBER('securityadmin') = 1 THEN 1
                            WHEN IS_SRVROLEMEMBER('serveradmin') = 1 THEN 1
                            ELSE 0
                        END";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckUserIsAdmin(string username)
        {
            try
            {
                if (_adminCache.ContainsKey(username))
                    return _adminCache[username];

                using (SqlConnection conn = CreateNewConnection())
                {
                    conn.Open();

                    if (CheckAdminTable(conn, username))
                        return Cache(username, true);

                    if (CheckDatabaseRoles(conn, username))
                        return Cache(username, true);

                    return Cache(username, false);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool Cache(string user, bool value)
        {
            _adminCache[user] = value;
            return value;
        }

        private static bool CheckAdminTable(SqlConnection conn, string username)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM UserAdmins WHERE Username=@u AND IsAdmin=1";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckDatabaseRoles(SqlConnection conn, string username)
        {
            try
            {
                string query = @"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM sys.database_role_members rm
                        JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
                        JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
                        WHERE u.name = @u AND r.name IN ('db_owner','db_securityadmin','db_accessadmin')
                    ) THEN 1 ELSE 0 END";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
            catch
            {
                return false;
            }
        }

        public static DataTable GetTable(string tableName)
        {
            using (SqlConnection conn = CreateNewConnection())
            {
                conn.Open();
                SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * FROM [{tableName}]", conn);
                DataTable table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }
    }
}
