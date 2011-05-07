﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Humpback.ConfigurationOptions;

namespace Humpback.Parts {
    public class SQLDatabaseProvider : IDatabaseProvider {

        private ConfigurationOptions.Configuration _configuration;
        private Settings _settings;
        private ISqlFormatter _sql_formatter;

        private SqlConnection GetOpenConnection() {
            var rv = new SqlConnection(_settings.ConnectionString());
            rv.Open();
            return rv;
        }
        public SQLDatabaseProvider(Configuration configuration, Settings settings, ISqlFormatter sql_formatter) {
            _configuration = configuration;
            _settings = settings;
            _sql_formatter = sql_formatter;
        }

        public void UpdateMigrationVersion(int number) {
            ExecuteCommand(_sql_formatter.sqlUpdateSchemaInfo(number));
        }

        public int ExecuteUpCommand(dynamic up) {
            var sql = _sql_formatter.GenerateSQLUp(up);
            foreach (var s in sql) {
                ExecuteCommand(s);
            }
            return sql.Length;
        }
        public int ExecuteDownCommand(dynamic down) {
            var sql = _sql_formatter.GenerateSQLDown(down);
            foreach (var s in sql) {
                ExecuteCommand(s);
            }
            return sql.Length;
        }
        public int ExecuteCommand(string command) {
            if(string.IsNullOrWhiteSpace(command)) {
                return 0;
            }
            using (var connection = GetOpenConnection()) {
                using (var cmd = connection.CreateCommand()) {
                    cmd.CommandText = command;
                    if (_configuration.Verbose) {
                        Console.WriteLine("Executing SQL: " + command);
                    }
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public int GetMigrationVersion() {
            try {
                using (var connection = GetOpenConnection()) {
                    using (var cmd = connection.CreateCommand()) {
                        cmd.CommandText = _sql_formatter.sqlGetSchemaInfo;
                        var reader = cmd.ExecuteReader();
                        if (reader.HasRows && reader.Read()) {
                            var rv = reader.GetInt32(0);
                            reader.Close();
                            return rv;
                        }

                        reader.Close();
                        using (var cmd_init = connection.CreateCommand()) {
                            cmd_init.CommandText = _sql_formatter.sqlInitializeSchemaInfo;
                            cmd.ExecuteNonQuery();
                        }
                        return 0;

                    }
                }
            } catch {
                EnsureSchemaInfo();
                return 0;
            }
        }


        private void EnsureSchemaInfo() {
            try {
                ExecuteCommand(_sql_formatter.sqlGetSchemaInfo);
            } catch {
                ExecuteCommand(_sql_formatter.sqlCreateSchemaInfoTable);
                ExecuteCommand(_sql_formatter.sqlInitializeSchemaInfo);
            }
        }


    }
}
