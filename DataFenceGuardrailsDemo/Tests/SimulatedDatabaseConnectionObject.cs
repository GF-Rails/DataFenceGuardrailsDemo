// DataFenceComponents Copyright (c) 2026 All rights reserved.

using System.Collections;
using System.Data;
using System.Data.Common;

namespace Demo.Tests
{
    /// <summary>DEMO ONLY: a fake in-memory connection so the demos run without SQL Server. Replace it with your own connection object (for example a SqlConnection) and give it to DatabaseGuardrails only.</summary>
    internal sealed class SimulatedDatabaseConnectionObject : DbConnection
    {
        public DataTable Data { get; set; }
        public SimulatedDatabaseConnectionObject(DataTable data) => Data = data;

        // Default fake rows, so a demo can use the connection without building a table. The column names are only
        // the shape of the fake result; no guardrail reads them. One row holds a card number for ResultGuardrails to mask.
        public SimulatedDatabaseConnectionObject()
        {
            var data = new DataTable();
            data.Columns.Add("SalesOrderID", typeof(int));
            data.Columns.Add("Comment", typeof(string));
            data.Rows.Add(1, "card 4111 1111 1111 1111");
            data.Rows.Add(2, "first order");
            data.Rows.Add(3, "second order");
            data.Rows.Add(4, "gift wrapped");
            data.Rows.Add(5, "express shipping");
            data.Rows.Add(6, "customer pickup");
            data.Rows.Add(7, "backordered");
            data.Rows.Add(8, "fine");
            Data = data;
        }
        public bool Executed { get; set; }
        public bool Throw { get; set; }
        /// <summary>Simulated server time per query. A command whose CommandTimeout is shorter than this fails like a real timeout.</summary>
        public TimeSpan Delay { get; set; }
        /// <summary>The last SQL text that reached the database.</summary>
        public string? LastSql { get; set; }
        /// <summary>The CommandTimeout (seconds) of the last command that reached the database.</summary>
        public int LastCommandTimeout { get; set; }
        /// <summary>Number of commands that reached the database.</summary>
        public int ExecutionCount { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "demo";
        public override string DataSource => "memory";
        public override string ServerVersion => "1";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new SimulatedDatabaseCommand(this, Data);
    }

    [System.Diagnostics.DebuggerNonUserCode]
    internal sealed class SimulatedDatabaseCommand : DbCommand
    {
        private readonly SimulatedDatabaseConnectionObject _owner;
        private readonly DataTable _data;
        private readonly SimulatedDatabaseParameterCollection _params = new();
        public SimulatedDatabaseCommand(SimulatedDatabaseConnectionObject owner, DataTable data) { _owner = owner; _data = data; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get => _owner; set { } }
        protected override DbParameterCollection DbParameterCollection => _params;
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new Microsoft.Data.SqlClient.SqlParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (_owner.Throw) throw new InvalidOperationException("database is down");
            _owner.LastSql = CommandText;
            _owner.LastCommandTimeout = CommandTimeout;
            _owner.ExecutionCount++;
            if (_owner.Delay > TimeSpan.Zero && CommandTimeout > 0 && _owner.Delay > TimeSpan.FromSeconds(CommandTimeout))
                throw new TimeoutException($"Execution Timeout Expired after {CommandTimeout} second(s).");
            _owner.Executed = true;
            return _data.CreateDataReader();
        }
    }

    internal sealed class SimulatedDatabaseParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _list = new();
        public override int Count => _list.Count;
        public override object SyncRoot => this;
        public override int Add(object value) { _list.Add((DbParameter)value); return _list.Count - 1; }
        public override void AddRange(Array values) { foreach (object v in values) Add(v); }
        public override void Clear() => _list.Clear();
        public override bool Contains(object value) => _list.Contains((DbParameter)value);
        public override bool Contains(string value) => _list.Any(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => _list.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _list.GetEnumerator();
        public override int IndexOf(object value) => _list.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _list.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _list.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _list.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _list.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _list.RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _list[index];
        protected override DbParameter GetParameter(string parameterName) => _list[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _list[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _list[IndexOf(parameterName)] = value;
    }
}
