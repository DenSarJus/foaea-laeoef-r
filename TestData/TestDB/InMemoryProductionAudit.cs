﻿using FOAEA3.Model;
using FOAEA3.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TestData.TestDB
{
    public class InMemoryProductionAudit : IProductionAuditRepository
    {
        public string CurrentSubmitter { get; set; }
        public string UserId { get; set; }

        public async Task InsertAsync(string processName, string description, string audience, DateTime? completedDate = null)
        {
            await Task.Run(() =>
            {
                InMemData.ProductionAuditData.Add($"{processName}({DateTime.Now}): {description}");
            });
        }

        public async Task InsertAsync(ProductionAuditData productionAuditData)
        {
            await Task.Run(() => { });
            throw new NotImplementedException();
        }
    }
}
