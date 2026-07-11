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
                        CREATE TABLE IF NOT EXISTS Emails (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            MessageId TEXT NOT NULL,
                            Sender TEXT NOT NULL,
                            Recipient TEXT NOT NULL,
                            Subject TEXT NOT NULL,
                            Body TEXT NOT NULL,
                            AttachmentsJson TEXT NOT NULL DEFAULT '[]',
                            ReceivedAt TEXT NOT NULL,
                            IsRead INTEGER NOT NULL DEFAULT 0,
                            IsDeleted INTEGER NOT NULL DEFAULT 0
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS IX_Emails_MessageId ON Emails (MessageId);
                        CREATE INDEX IF NOT EXISTS IX_Emails_IsDeleted ON Emails (IsDeleted);
                    ";
                    command.ExecuteNonQuery();
                }
            }

            // Seed mock emails for local UI process demo if empty
            if (!context.Emails.Any())
            {
                context.Emails.AddRange(new List<Email>
                {
                    new Email
                    {
                        MessageId = "mock-msg-1",
                        Sender = "Sanjay S <ssanjay1742004@gmail.com>",
                        Recipient = "sanjay.personal987@gmail.com",
                        Subject = "enquiry",
                        Body = @"Dear Sir/Madam,

Please send your best pricing for the below-listed items:
Brand Part Code Description Qty Unit
ABB A16-30-10 ABB Contactor A16-30-10 16A 3P 4 PCS
Siemens 5SL6332-7 Siemens MCB 32A 3P C Curve 2 PCS
Schneider NSYCRN33200 Schneider CRN Enclosure 300×300×200mm 1 PCS
Generic DIN-35-1M DIN Rail 35mm Standard 1 Meter 15 PCS
savio Pulley Savio Timing Belt Pulley 15T 10 PCS

Best regards,
Abraham George
Procurement Manager, Apex Automation Corp",
                        AttachmentsJson = "[]",
                        ReceivedAt = DateTime.Parse("2026-07-09T15:51:00"),
                        IsRead = false,
                        IsDeleted = false
                    },
                    new Email
                    {
                        MessageId = "mock-msg-2",
                        Sender = "Uma Maheshwari <uma.m@ariyaitech.com>",
                        Recipient = "sanjay.personal987@gmail.com",
                        Subject = "Fwd: RFQ for Spares List (PDF Attachment)",
                        Body = @"---------- Forwarded message ----------
From: Uma Maheshwari <uma.m@ariyaitech.com>
Date: Wed, Jul 8, 2026 at 5:58 PM
Subject: RFQ for Spares List (PDF Attachment)
To: Naren Procurement <naren.procure@manjunatha.com>

Dear Team,

Please review the attached PDF spares listing and provide your competitive quotation.

Thanks,
Uma Maheshwari
CEO, Ariyaitech Solutions",
                        AttachmentsJson = "[]",
                        ReceivedAt = DateTime.Parse("2026-07-08T17:58:00"),
                        IsRead = false,
                        IsDeleted = false
                    },
                    new Email
                    {
                        MessageId = "mock-msg-3",
                        Sender = "Augustine Cruzmuthu <augustine@gmail.com>",
                        Recipient = "sanjay.personal987@gmail.com",
                        Subject = "Enquiry for items",
                        Body = @"Hi,

Please check and send a quote for the following items:
1. Proximity Sensor 3-wire NPN - 5 NOS
2. Solid State Relay 25A - 2 NOS

Regards,
Augustine",
                        AttachmentsJson = "[]",
                        ReceivedAt = DateTime.Parse("2026-07-08T16:43:00"),
                        IsRead = false,
                        IsDeleted = false
                    },
                    new Email
                    {
                        MessageId = "mock-msg-4",
                        Sender = "Augustine Cruzmuthu <augustine@gmail.com>",
                        Recipient = "sanjay.personal987@gmail.com",
                        Subject = "Give me Enquiry data",
                        Body = @"Hello,

Can you share the previous enquiry data for Premier Cotton Textiles?

Thanks,
Augustine",
                        AttachmentsJson = "[]",
                        ReceivedAt = DateTime.Parse("2026-07-08T16:39:00"),
                        IsRead = false,
                        IsDeleted = false
                    }
                });
                context.SaveChanges();
            }
        }
    }
}
