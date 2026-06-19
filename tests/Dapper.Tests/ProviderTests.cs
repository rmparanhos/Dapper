using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper.ProviderTools;
using Xunit;

namespace Dapper.Tests
{
    public class ProviderTests
    {
        [Fact]
        public void BulkCopy_SystemDataSqlClient()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            using var conn = new System.Data.SqlClient.SqlConnection();
            Test<System.Data.SqlClient.SqlBulkCopy>(conn);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void BulkCopy_MicrosoftDataSqlClient()
        {
            using var conn = new Microsoft.Data.SqlClient.SqlConnection();
            Test<Microsoft.Data.SqlClient.SqlBulkCopy>(conn);
        }

#if MSSQLCLIENT
        [Fact]
        public void BulkCopy_WriteToServer_ForwardsSettings()
        {
            var provider = new MicrosoftSqlClientProvider();
            using var conn = (Microsoft.Data.SqlClient.SqlConnection)provider.GetOpenConnection();

            using var bcp = BulkCopy.Create(conn);
            bcp.EnableStreaming = true;
            bcp.BatchSize = 500;
            bcp.BulkCopyTimeout = 60;

            var raw = (Microsoft.Data.SqlClient.SqlBulkCopy)bcp.Wrapped;
            Assert.True(raw.EnableStreaming);
            Assert.Equal(500, raw.BatchSize);
            Assert.Equal(60, raw.BulkCopyTimeout);
        }

        [Fact]
        public void BulkCopy_WriteToServer_InsertsRows()
        {
            var provider = new MicrosoftSqlClientProvider();
            using var conn = (Microsoft.Data.SqlClient.SqlConnection)provider.GetOpenConnection();
            conn.Execute("CREATE TABLE #bcp_test (Id INT NOT NULL, Name NVARCHAR(100) NOT NULL)");

            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Rows.Add(1, "Alice");
            dt.Rows.Add(2, "Bob");
            dt.Rows.Add(3, "Carol");

            using (var bcp = BulkCopy.Create(conn))
            {
                bcp.DestinationTableName = "#bcp_test";
                bcp.WriteToServer(dt);
            }

            var count = conn.ExecuteScalar<int>("SELECT COUNT(1) FROM #bcp_test");
            Assert.Equal(3, count);
        }

        [Fact]
        public async Task BulkCopy_WriteToServerAsync_InsertsRows()
        {
            var provider = new MicrosoftSqlClientProvider();
            using var conn = (Microsoft.Data.SqlClient.SqlConnection)provider.GetOpenConnection();
            conn.Execute("CREATE TABLE #bcp_test_async (Id INT NOT NULL, Name NVARCHAR(100) NOT NULL)");

            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Rows.Add(1, "Alice");
            dt.Rows.Add(2, "Bob");
            dt.Rows.Add(3, "Carol");

            using (var bcp = BulkCopy.Create(conn))
            {
                bcp.DestinationTableName = "#bcp_test_async";
                await bcp.WriteToServerAsync(dt);
            }

            var count = conn.ExecuteScalar<int>("SELECT COUNT(1) FROM #bcp_test_async");
            Assert.Equal(3, count);
        }
#endif

        [Fact]
        public void ClientId_SystemDataSqlClient()
            => TestClientId<SystemSqlClientProvider>();

        [Fact]
        public void ClearPool_SystemDataSqlClient()
            => ClearPool<SystemSqlClientProvider>();

        [Fact]
        public void ClearAllPools_SystemDataSqlClient()
            => ClearAllPools<SystemSqlClientProvider>();

#if MSSQLCLIENT
        [Fact]
        public void ClientId_MicrosoftDataSqlClient()
            => TestClientId<MicrosoftSqlClientProvider>();

        [Fact]
        public void ClearPool_MicrosoftDataSqlClient()
            => ClearPool<MicrosoftSqlClientProvider>();

        [Fact]
        public void ClearAllPools_MicrosoftDataSqlClient()
            => ClearAllPools<MicrosoftSqlClientProvider>();
#endif

        private static void TestClientId<T>()
             where T : SqlServerDatabaseProvider, new()
        {
            var provider = new T();
            using var conn = provider.GetOpenConnection();
            Assert.True(conn.TryGetClientConnectionId(out var id));
            Assert.NotEqual(Guid.Empty, id);
        }

        private static void ClearPool<T>()
     where T : SqlServerDatabaseProvider, new()
        {
            var provider = new T();
            using var conn = provider.GetOpenConnection();
            Assert.True(conn.TryClearPool());
        }

        private static void ClearAllPools<T>()
     where T : SqlServerDatabaseProvider, new()
        {
            var provider = new T();
            using var conn = provider.GetOpenConnection();
            Assert.True(conn.TryClearAllPools());
        }

        private static void Test<T>(DbConnection connection)
        {
            using var bcp = BulkCopy.TryCreate(connection);
            Assert.NotNull(bcp);
            Assert.IsType<T>(bcp.Wrapped);
            bcp.EnableStreaming = true;
        }

        [Theory]
        [InlineData(51000, 51000, true)]
        [InlineData(51000, 43, false)]
        public void DbNumber_SystemData(int create, int test, bool result)
            => Test<SystemSqlClientProvider>(create, test, result);

#if MSSQLCLIENT
        [Theory]
        [InlineData(51000, 51000, true)]
        [InlineData(51000, 43, false)]
        public void DbNumber_MicrosoftData(int create, int test, bool result)
            => Test<MicrosoftSqlClientProvider>(create, test, result);
#endif

        private static void Test<T>(int create, int test, bool result)
            where T : SqlServerDatabaseProvider, new()
        {
            var provider = new T();
            
            using var conn = provider.GetOpenConnection();

            try
            {
                conn.Execute("throw @create, 'boom', 1;", new { create });
                Assert.False(true);
            }
            catch(DbException err)
            {
                Assert.Equal(result, err.IsNumber(test));
            }
        }
    }
}
