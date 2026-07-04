using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using AriyAI.ERP.Api.Models;
using ExcelDataReader;

using Microsoft.EntityFrameworkCore;

namespace AriyAI.ERP.Api.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ErpDbContext context)
        {
            context.Database.EnsureCreated();

            using (var connection = context.Database.GetDbConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ChatSessions (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Title TEXT NOT NULL,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        );
                        CREATE TABLE IF NOT EXISTS ChatMessages (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ChatSessionId INTEGER NOT NULL,
                            Sender TEXT NOT NULL,
                            Text TEXT NOT NULL,
                            Sql TEXT NULL,
                            Data TEXT NULL,
                            Chart TEXT NULL,
                            Timestamp TEXT NOT NULL,
                            FOREIGN KEY(ChatSessionId) REFERENCES ChatSessions(Id) ON DELETE CASCADE
                        );
                    ";
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
