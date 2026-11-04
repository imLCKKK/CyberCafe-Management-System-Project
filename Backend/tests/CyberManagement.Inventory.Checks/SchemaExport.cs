using System.Text;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class SchemaExport
{
    public static void Run(bool checkOnly)
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Backend", "CyberManagement.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Run inside the repository.");
        using var context = new ApplicationDbContextFactory().CreateDbContext([]);
        if (context.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Model differs from migrations; coordinate a new migration before export.");
        var model = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var tables = model.Tables.OrderBy(table => table.Name).ToArray();
        var text = new StringBuilder();
        text.AppendLine("// Generated from the CyberManagement SQL Server EF model; do not edit manually.");
        foreach (var table in tables)
        {
            text.AppendLine($"Table {table.Schema ?? "dbo"}.{table.Name} {{");
            foreach (var column in table.Columns)
            {
                var settings = new List<string> { column.IsNullable ? "null" : "not null" };
                if (table.PrimaryKey?.Columns.Count == 1 && table.PrimaryKey.Columns.Contains(column)) settings.Add("pk");
                if (column.PropertyMappings.Any(mapping => mapping.Property.FindAnnotation("SqlServer:ValueGenerationStrategy")?.Value?.ToString() == "IdentityColumn")) settings.Add("increment");
                if (column.DefaultValueSql is not null) settings.Add($"default: `{column.DefaultValueSql}`");
                else if (column.DefaultValue is not null) settings.Add("default: `" + Convert.ToString(column.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) + "`");
                text.AppendLine($"  {column.Name} {column.StoreType} [{string.Join(", ", settings)}]");
            }
            text.AppendLine("  indexes {");
            if (table.PrimaryKey?.Columns.Count > 1)
                text.AppendLine($"    ({string.Join(", ", table.PrimaryKey.Columns.Select(column => column.Name))}) [pk]");
            foreach (var index in table.Indexes.OrderBy(index => index.Name))
                text.AppendLine($"    ({string.Join(", ", index.Columns.Select(column => column.Name))}) [name: '{index.Name}'{(index.IsUnique ? ", unique" : "")}]");
            text.AppendLine("  }");
            text.AppendLine("}");
        }
        foreach (var table in tables)
            foreach (var fk in table.ForeignKeyConstraints.OrderBy(fk => fk.Name))
            {
                static string Columns(IEnumerable<IColumn> columns) => "(" + string.Join(", ", columns.Select(column => column.Name)) + ")";
                text.AppendLine($"Ref: {table.Schema ?? "dbo"}.{table.Name}.{Columns(fk.Columns)} > {fk.PrincipalTable.Schema ?? "dbo"}.{fk.PrincipalTable.Name}.{Columns(fk.PrincipalColumns)}");
            }
        var sql = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        WriteOrCheck("schema.dbml", text.ToString());
        WriteOrCheck("CyberManagement.sql", sql);
        Console.WriteLine($"{(checkOnly ? "Verified" : "Exported")} SQL/DBML: {tables.Length} tables; {context.Database.GetMigrations().Count()} migrations; no SQL connection opened.");

        void WriteOrCheck(string name, string content)
        {
            var directory = Path.Combine(root.FullName, "Backend", "database");
            var path = Path.Combine(directory, name);
            content = content.Replace("\r\n", "\n").TrimEnd() + "\n";
            if (checkOnly)
            {
                if (!File.Exists(path) || File.ReadAllText(path).Replace("\r\n", "\n") != content)
                    throw new InvalidOperationException($"Stale schema artifact: {name}; run --export-schema.");
            }
            else
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, content, new UTF8Encoding(false));
            }
        }
    }
}
