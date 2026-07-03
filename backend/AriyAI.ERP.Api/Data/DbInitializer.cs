using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using AriyAI.ERP.Api.Models;
using ExcelDataReader;

namespace AriyAI.ERP.Api.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ErpDbContext context)
        {
            context.Database.EnsureCreated();
        }
    }
}
