using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string BasePath = "/devops-tools";
const long MaxUploadBytes = 500L * 1024 * 1024;
const int MaxProjectRequestBytes = 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Environment.GetEnvironmentVariable("DEVOPS_DATA_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "data");
var projectsDirectory = Path.Combine(dataDirectory, "projects");
Directory.CreateDirectory(projectsDirectory);
var app = builder.Build();

app.MapPost($"{BasePath}/api/projects", async (HttpRequest request) =>
{
    if (request.ContentLength > MaxProjectRequestBytes)
        return Results.Json(new { error = "request too large" }, statusCode: StatusCodes.Status413PayloadTooLarge);

    ProjectRequest? input;
    try
    {
        input = await JsonSerializer.DeserializeAsync<ProjectRequest>(request.Body);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "invalid JSON" });
    }

    if (input is null || !Regex.IsMatch(input.Name ?? "", "^[A-Za-z0-9_-]{1,64}$"))
        return Results.BadRequest(new { error = "invalid project name" });
    if (string.IsNullOrWhiteSpace(input.SiteName) || input.Apps is not { Count: > 0 }
        || input.Apps.Any(a => string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.TargetPath)))
        return Results.BadRequest(new { error = "siteName and apps are required" });

    var projectDirectory = Path.Combine(projectsDirectory, input.Name!);
    if (Directory.Exists(projectDirectory))
        return Results.Conflict(new { error = "project exists" });
    Directory.CreateDirectory(projectDirectory);
    var projectPath = Path.Combine(projectDirectory, "project.json");
    var project = new ProjectConfig(
        input.Name!,
        input.SiteName,
        input.BackendAppPool ?? "",
        input.ClientBuildCommands ?? [],
        input.ClientOutputPath ?? "publish",
        input.Apps!,
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant());
    try
    {
        await using var file = new FileStream(projectPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(file, project, AppJson.Options);
    }
    catch (IOException) when (File.Exists(projectPath))
    {
        return Results.Conflict(new { error = "project exists" });
    }

    var installUrl = $"{request.Scheme}://{request.Host}{BasePath}/install-client"
        + $"?project={Uri.EscapeDataString(project.Name)}&token={Uri.EscapeDataString(project.Token)}";
    return Results.Json(new { name = project.Name, path = $"{BasePath}/{project.Name}", installUrl },
        statusCode: StatusCodes.Status201Created);
});

app.MapGet($"{BasePath}/install-client", async (HttpRequest request, string? project, string? token) =>
{
    var config = await LoadProjectAsync(projectsDirectory, project);
    if (config is null || !TokenMatches(token, config.Token))
        return Results.NotFound(new { error = "not found" });
    var content = GenerateClientScript(config, $"{request.Scheme}://{request.Host}");
    return Results.File(Encoding.UTF8.GetBytes(content), "application/octet-stream", $"deploy-{config.Name}.bat");
});

app.MapPost($"{BasePath}/api/projects/{{name}}/deploy", async (HttpContext context, string name) =>
{
    var config = await LoadProjectAsync(projectsDirectory, name);
    if (config is null || !TokenMatches(context.Request.Headers["X-Devops-Token"].FirstOrDefault(), config.Token))
        return Results.Unauthorized();

    var uploadDirectory = Path.Combine(projectsDirectory, config.Name);
    var zipPath = Path.Combine(uploadDirectory, $"upload-{Guid.NewGuid():N}.zip");
    try
    {
        await using (var output = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer)) > 0)
            {
                total += count;
                if (total > MaxUploadBytes)
                    return Results.Json(new { error = "package too large" }, statusCode: StatusCodes.Status413PayloadTooLarge);
                await output.WriteAsync(buffer.AsMemory(0, count));
            }
            if (total == 0)
                return Results.BadRequest(new { error = "empty package" });
        }

        try
        {
            var log = await RunDeployAsync(config, zipPath);
            return Results.Ok(new { status = "deployed", log });
        }
        catch (Exception exception)
        {
            return Results.Json(new { status = "failed", error = exception.Message },
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    finally
    {
        if (File.Exists(zipPath))
            File.Delete(zipPath);
    }
});

app.Run();

static async Task<ProjectConfig?> LoadProjectAsync(string projectsDirectory, string? name)
{
    if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, "^[A-Za-z0-9_-]{1,64}$"))
        return null;
    var path = Path.Combine(projectsDirectory, name, "project.json");
    if (!File.Exists(path))
        return null;
    try
    {
        await using var file = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ProjectConfig>(file, AppJson.Options);
    }
    catch (JsonException)
    {
        return null;
    }
}

static bool TokenMatches(string? provided, string expected)
{
    if (provided is null)
        return false;
    var suppliedBytes = Encoding.UTF8.GetBytes(provided);
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    return suppliedBytes.Length == expectedBytes.Length
        && CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
}

static string GenerateClientScript(ProjectConfig project, string serverUrl)
{
    var builds = string.Join("\r\n\r\n", project.ClientBuildCommands.Select(command =>
        $"call {command}\r\nif errorlevel 1 exit /b 1"));
    var outputPath = project.ClientOutputPath.Replace("'", "''", StringComparison.Ordinal);
    return string.Join("\r\n",
    [
        "@echo off",
        "rem Generated by devops-tools. Run from the root of the project in your machine.",
        $"set DEVOPS_URL={serverUrl}{BasePath}/api/projects/{project.Name}/deploy",
        $"set DEVOPS_TOKEN={project.Token}",
        $"set DEVOPS_PACKAGE=%TEMP%\\{project.Name}-package.zip",
        builds,
        $"powershell -NoProfile -Command \"if (Test-Path '{outputPath}') {{ if (Test-Path $env:DEVOPS_PACKAGE) {{ Remove-Item $env:DEVOPS_PACKAGE }}; Compress-Archive -Path '{outputPath}\\*' -DestinationPath $env:DEVOPS_PACKAGE }} else {{ exit 1 }}\"",
        "if errorlevel 1 (echo Could not package output & exit /b 1)",
        "curl.exe --fail -X POST -H \"X-Devops-Token: %DEVOPS_TOKEN%\" --data-binary \"@%DEVOPS_PACKAGE%\" \"%DEVOPS_URL%\"",
        "if errorlevel 1 (echo Deploy failed & exit /b 1)",
        "echo Deploy finished.",
        ""
    ]);
}

static async Task<string> RunDeployAsync(ProjectConfig project, string zipPath)
{
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("IIS deployment requires Windows.");

    var script = Environment.GetEnvironmentVariable("DEVOPS_DEPLOY_SCRIPT")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "scripts", "deploy.bat");
    var startInfo = new ProcessStartInfo("cmd.exe")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    startInfo.ArgumentList.Add("/c");
    startInfo.ArgumentList.Add(script);
    startInfo.ArgumentList.Add(zipPath);
    startInfo.Environment["DEVOPS_APPS"] = JsonSerializer.Serialize(project.Apps, AppJson.Options);
    startInfo.Environment["DEVOPS_BACKEND_APP_POOL"] = string.IsNullOrEmpty(project.BackendAppPool)
        ? "-" : project.BackendAppPool;

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start the IIS deployment script.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var log = await stdout + await stderr;
    if (process.ExitCode != 0)
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(log) ? $"exit {process.ExitCode}" : log);
    return log;
}

public sealed record ProjectRequest(
    string? Name,
    string? SiteName,
    string? BackendAppPool,
    List<string>? ClientBuildCommands,
    string? ClientOutputPath,
    List<AppConfig>? Apps);

public sealed record ProjectConfig(
    string Name,
    string SiteName,
    string BackendAppPool,
    List<string> ClientBuildCommands,
    string ClientOutputPath,
    List<AppConfig> Apps,
    string Token);

public sealed record AppConfig(string Name, string TargetPath, string Type = "static");

internal static class AppJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
