using BenchmarkDotNet.Attributes;
using Dapper.ProviderTools;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Dapper.Tests.Performance
{
    [BenchmarkCategory("BulkInsert")]
    public class BulkInsertBenchmarks : BenchmarkBase
    {
        private static readonly int[] BatchSizes = [100, 1_000, 10_000];

        private List<Post> _rows = null!;

        [Params(100, 1_000, 10_000)]
        public int RowCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BaseSetup();
            _connection.Execute("""
                IF OBJECT_ID('tempdb..#BulkInsertBench') IS NOT NULL DROP TABLE #BulkInsertBench;
                CREATE TABLE #BulkInsertBench (
                    Id            INT NOT NULL,
                    Text          NVARCHAR(MAX) NULL,
                    CreationDate  DATETIME2 NOT NULL,
                    LastChangeDate DATETIME2 NOT NULL,
                    Counter1 INT NULL, Counter2 INT NULL, Counter3 INT NULL
                );
                """);

            var now = DateTime.UtcNow;
            _rows = [];
            for (int i = 0; i < 10_000; i++)
            {
                _rows.Add(new Post
                {
                    Id = i + 1,
                    Text = $"Post {i}",
                    CreationDate = now,
                    LastChangeDate = now,
                    Counter1 = i % 100,
                    Counter2 = i % 50,
                    Counter3 = i % 10,
                });
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _connection.Execute("DROP TABLE IF EXISTS #BulkInsertBench");
        }

        [IterationSetup]
        public void TruncateTable()
        {
            _connection.Execute("TRUNCATE TABLE #BulkInsertBench");
        }

        private DataTable BuildDataTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Text", typeof(string));
            dt.Columns.Add("CreationDate", typeof(DateTime));
            dt.Columns.Add("LastChangeDate", typeof(DateTime));
            dt.Columns.Add("Counter1", typeof(int));
            dt.Columns.Add("Counter2", typeof(int));
            dt.Columns.Add("Counter3", typeof(int));

            for (int i = 0; i < RowCount; i++)
            {
                var p = _rows[i];
                dt.Rows.Add(p.Id, p.Text, p.CreationDate, p.LastChangeDate,
                    p.Counter1 ?? (object)DBNull.Value,
                    p.Counter2 ?? (object)DBNull.Value,
                    p.Counter3 ?? (object)DBNull.Value);
            }
            return dt;
        }

        [Benchmark(Baseline = true, Description = "SqlBulkCopy (raw)")]
        public void SqlBulkCopyRaw()
        {
            using var bcp = new SqlBulkCopy(_connection);
            bcp.DestinationTableName = "#BulkInsertBench";
            bcp.EnableStreaming = true;
            bcp.WriteToServer(BuildDataTable());
        }

        [Benchmark(Description = "BulkCopy (ProviderTools)")]
        public void DapperProviderToolsBulkCopy()
        {
            using var bcp = BulkCopy.Create(_connection);
            bcp.DestinationTableName = "#BulkInsertBench";
            bcp.EnableStreaming = true;
            bcp.WriteToServer(BuildDataTable());
        }

        [Benchmark(Description = "SqlBulkCopy (raw) async")]
        public async Task SqlBulkCopyRawAsync()
        {
            using var bcp = new SqlBulkCopy(_connection);
            bcp.DestinationTableName = "#BulkInsertBench";
            bcp.EnableStreaming = true;
            await bcp.WriteToServerAsync(BuildDataTable());
        }

        [Benchmark(Description = "BulkCopy (ProviderTools) async")]
        public async Task DapperProviderToolsBulkCopyAsync()
        {
            using var bcp = BulkCopy.Create(_connection);
            bcp.DestinationTableName = "#BulkInsertBench";
            bcp.EnableStreaming = true;
            await bcp.WriteToServerAsync(BuildDataTable());
        }

        [Benchmark(Description = "Multi-row INSERT (Dapper)")]
        public void MultiRowInsertDapper()
        {
            var subset = _rows.GetRange(0, RowCount);
            _connection.Execute("""
                INSERT INTO #BulkInsertBench (Id, Text, CreationDate, LastChangeDate, Counter1, Counter2, Counter3)
                VALUES (@Id, @Text, @CreationDate, @LastChangeDate, @Counter1, @Counter2, @Counter3)
                """, subset);
        }
    }
}
