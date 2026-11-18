﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    public partial class AddEmployeeMutationIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_AuditLogs_EmployeeId", table: "AuditLogs");
            migrationBuilder.AddColumn<string>(name: "RequestFingerprint", table: "AuditLogs", type: "varchar(64)", unicode: false, maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(name: "RequestKey", table: "AuditLogs", type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Latin1_General_100_BIN2");
            migrationBuilder.AddColumn<string>(name: "ResponseJson", table: "AuditLogs", type: "nvarchar(max)", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_AuditLogs_EmployeeId_RequestKey", table: "AuditLogs", columns: new[] { "EmployeeId", "RequestKey" }, unique: true, filter: "[RequestKey] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_AuditLogs_EmployeeId_RequestKey", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "RequestFingerprint", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "RequestKey", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "ResponseJson", table: "AuditLogs");
            migrationBuilder.CreateIndex(name: "IX_AuditLogs_EmployeeId", table: "AuditLogs", column: "EmployeeId");
        }
    }
}
