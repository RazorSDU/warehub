﻿using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using ZstdSharp;
using warehub.services;

namespace warehub.db
{
    public class CRUDService
    {
        private readonly MySqlConnection _connection;

        // Primary constructor accepting MySqlConnection instance from DbConnection
        public CRUDService(MySqlConnection connection) => _connection = connection;

        /// <summary>
        /// Inserts a new entry into the specified table.
        /// </summary>
        public bool Create(string table, Dictionary<string, object> parameters)
        {
            try
            {
                var columns = string.Join(", ", parameters.Keys);
                var values = string.Join(", ", parameters.Keys.Select(k => $"@{k}"));
                string query = $"INSERT INTO {table} ({columns}) VALUES ({values})";

                return ExecuteNonQuery(query, parameters, "Item created successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create Error: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Reads entries from the specified table with optional filtering.
        /// </summary>
        public (bool, List<Dictionary<string, object>>) Read(string table, Dictionary<string, object> parameters)
        {
            try
            {
                string whereClause = parameters.Any()
                    ? "WHERE " + string.Join(" AND ", parameters.Keys.Select(k => $"{k} = @{k}"))
                    : "";

                string query = $"SELECT * FROM {table} {whereClause}";
                Console.WriteLine($"Generated Query: {query}");

                return ExecuteQuery(query, parameters, "Data retrieved successfully.", table);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Read Error: {ex.Message}");
                return (false, null);
            }
        }


        /// <summary>
        /// Updates an existing entry in the specified table.
        /// </summary>
        public bool Update(string table, Dictionary<string, object> parameters, string idColumn, object idValue)
        {
            try
            {
                var setClause = string.Join(", ", parameters.Keys.Select(k => $"{k} = @{k}"));
                string query = $"UPDATE {table} SET {setClause} WHERE {idColumn} = @{idColumn}";

                parameters[idColumn] = idValue;
                return ExecuteNonQuery(query, parameters, "Item updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Error: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Deletes an entry from the specified table.
        /// </summary>
        public bool Delete(string table, string idColumn, object idValue)
        {
            try
            {
                string query = $"DELETE FROM {table} WHERE {idColumn} = @{idColumn}";
                var parameters = new Dictionary<string, object> { { idColumn, idValue } };

                return ExecuteNonQuery(query, parameters, "Item deleted successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete Error: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Executes a non-query command such as INSERT, UPDATE, or DELETE.
        /// </summary>
        private bool ExecuteNonQuery(string query, Dictionary<string, object> parameters, string successMessage, bool commitTransaction = true)
        {
            MySqlTransaction transaction = null;

            try
            {
                transaction = _connection.BeginTransaction();

                using (var command = new MySqlCommand(query, _connection, transaction))
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue($"@{param.Key}", param.Value);
                    }

                    int affectedRows = command.ExecuteNonQuery();
                    if (affectedRows > 0 && commitTransaction)
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.Rollback();
                    }

                    return affectedRows > 0;
                }
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                Console.WriteLine($"SQL Error: {ex.Message}\nQuery: {query}");
                return false;
            }
            finally
            {
                transaction?.Dispose();
            }
        }



        /// <summary>
        /// Executes a query command and GETS results as a list of dictionaries.
        /// </summary>
        private (bool, List<Dictionary<string, object>>) ExecuteQuery(string query, Dictionary<string, object> parameters, string successMessage, string tableName)
        {
            var results = new List<Dictionary<string, object>>();
            bool status = false;

            try
            {
                if (_connection.State != System.Data.ConnectionState.Open)
                {
                    throw new InvalidOperationException("Database connection is not open.");
                }

                // Fetch the type mapping for the specified table
                if (!TableTypeRegistry.TableColumnMappings.TryGetValue(tableName, out var columnTypeMapping))
                {
                    throw new InvalidOperationException($"No type mapping found for table: {tableName}");
                }

                using (var command = new MySqlCommand(query, _connection))
                {
                    // Add parameters to the command
                    foreach (var param in parameters)
                    {
                        var value = param.Key == "id" ? GuidService.GuidToString((Guid)param.Value) : param.Value;
                        command.Parameters.AddWithValue($"@{param.Key}", value);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string columnName = reader.GetName(i);
                                object value = reader.GetValue(i);

                                // Convert the value based on the column type mapping
                                if (columnTypeMapping.TryGetValue(columnName, out var targetType))
                                {
                                    value = ConvertToType(value, targetType);
                                }

                                row[columnName] = value;
                            }
                            results.Add(row);
                        }
                    }

                    Console.WriteLine(successMessage);
                    status = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SQL Error: {ex.Message}\nQuery: {query}");
            }

            return (status, results);
        }


        public static class TableTypeRegistry
        {
            public static readonly Dictionary<string, Dictionary<string, Type>> TableColumnMappings = new()
            {
                {
                    "products", // Table name
                    new Dictionary<string, Type>
                    {
                        { "id", typeof(Guid) },
                        { "name", typeof(string) },
                        { "price", typeof(decimal) }
                    }
                },
                {
                    "test_table",
                    new Dictionary<string, Type>
                    {
                        { "id", typeof(Guid) },
                        { "name", typeof(string) }
                    }
                },
                // Add mappings for additional tables as needed
                {
                    "another_table",
                    new Dictionary<string, Type>
                    {
                        { "column1", typeof(int) },
                        { "column2", typeof(DateTime) }
                    }
                }
            };
        }

        private object ConvertToType(object value, Type targetType)
        {
            if (value == DBNull.Value)
                return null;

            if (targetType == typeof(Guid))
            {
                return Guid.Parse(value.ToString());
            }

            return Convert.ChangeType(value, targetType);
        }

    }
}
