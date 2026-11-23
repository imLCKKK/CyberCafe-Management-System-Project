using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CyberManagement.Application.Exceptions;
using CyberManagement.Infrastructure.TeamBalancing;
using CyberManagement.TeamBalancing.Training;

if (args is not (["prepare" or "train", _, _, _] or ["verify", _, _]))
{
    Console.Error.WriteLine("Usage: TeamBalancing.Training prepare <source.json> <matches.jsonl> <new-output-directory>");
    Console.Error.WriteLine("       TeamBalancing.Training train <prepared-directory> <model-version> <new-output-directory>");
    Console.Error.WriteLine("       TeamBalancing.Training verify <model-directory> <trusted-manifest-sha256>");
    return 2;
}

try
{
    if (args[0] == "verify")
    {
        var artifact = TeamBalanceModelArtifact.Load(args[1], args[2]);
        Console.WriteLine($"Model {artifact.ModelVersion}; dataset={artifact.DatasetVersion}; model={artifact.ModelSha256}; manifest={args[2]}");
        return 0;
    }
    var output = Path.GetFullPath(args[3]);
    if (Directory.Exists(output) || File.Exists(output))
        throw new TeamBalanceException("OUTPUT_EXISTS", "Chọn thư mục output mới để giữ nguyên artifact trước đó.");
    if (args[0] == "train")
    {
        var training = ModelTraining.Run(args[1], args[2], output, ReadCommit());
        Console.WriteLine($"Model {training.Manifest.ModelVersion}; dataKind={training.Manifest.DataKind}; predictionGate={training.Manifest.PredictionGatePassed}; readyForInference={training.Manifest.ReadyForInference}; manifest={training.ManifestSha256}");
        return 0;
    }
    var sourceBytes = File.ReadAllBytes(args[1]);
    var matchBytes = File.ReadAllBytes(args[2]);
    string Hash(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
    var source = JsonSerializer.Deserialize<DatasetSource>(sourceBytes, DatasetPreparation.JsonOptions)
        ?? throw new TeamBalanceException("DATASET_INVALID", "Manifest rỗng.");
    if (source.MatchesSha256 != Hash(matchBytes))
        throw new TeamBalanceException("DATASET_INVALID", "Hash file matches không khớp manifest nguồn.");
    var records = new List<MatchRecord>();
    using (var reader = new StreamReader(new MemoryStream(matchBytes), new System.Text.UTF8Encoding(false, true)))
    {
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            records.Add(JsonSerializer.Deserialize<MatchRecord>(line, DatasetPreparation.JsonOptions)
                ?? throw new TeamBalanceException("DATASET_INVALID", "Match row rỗng."));
        }
    }
    var result = DatasetPreparation.Prepare(source, records, Hash(sourceBytes), ReadCommit());
    Directory.CreateDirectory(output);
    var jsonlOptions = new JsonSerializerOptions(DatasetPreparation.JsonOptions) { WriteIndented = false };
    foreach (var (fold, rows) in result.Rows)
        File.WriteAllLines(Path.Combine(output, fold + ".jsonl"), rows.Select(r => JsonSerializer.Serialize(r, jsonlOptions)));
    var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(result.Manifest, DatasetPreparation.JsonOptions);
    File.WriteAllBytes(Path.Combine(output, "split-manifest.json"), manifestBytes);
    File.WriteAllText(Path.Combine(output, "split-manifest.sha256"), Hash(manifestBytes) + "\n");
    File.WriteAllText(Path.Combine(output, "baseline.json"), JsonSerializer.Serialize(result.EvaluationBaseline, DatasetPreparation.JsonOptions));
    var fileHashes = Directory.GetFiles(output).Order(StringComparer.Ordinal).ToDictionary(path => Path.GetFileName(path)!,
        path => Hash(File.ReadAllBytes(path)));
    File.WriteAllText(Path.Combine(output, "artifact-hashes.json"), JsonSerializer.Serialize(fileHashes, DatasetPreparation.JsonOptions));
    Console.WriteLine($"Prepared {result.Manifest.OriginalMatches} original matches. No model trained; artifacts are not approved for inference.");
    return 0;
}
catch (TeamBalanceException error)
{
    Console.Error.WriteLine($"{error.Code}: {error.Message}");
    return 1;
}

catch (Exception error) when (error is JsonException or IOException or InvalidDataException or UnauthorizedAccessException or FormatException
    or ArgumentException or InvalidOperationException or KeyNotFoundException or System.ComponentModel.Win32Exception)
{
    Console.Error.WriteLine("DATASET_INVALID: Không đọc/chuẩn bị/huấn luyện được dữ liệu; kiểm tra schema, đường dẫn và quyền truy cập. Output có thể chưa hoàn chỉnh.");
    return 1;
}

static string ReadCommit()
{
    var git = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    git.ArgumentList.Add("rev-parse");
    git.ArgumentList.Add("HEAD");
    using var process = Process.Start(git)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    _ = error.GetAwaiter().GetResult();
    if (process.ExitCode != 0) throw new TeamBalanceException("CODE_VERSION_UNAVAILABLE", "Chạy tool trong Git checkout của dự án.");
    return output.GetAwaiter().GetResult().Trim();
}
