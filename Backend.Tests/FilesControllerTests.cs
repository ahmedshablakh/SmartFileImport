using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartFileImport.Api.Configuration;
using SmartFileImport.Api.Controllers;

namespace SmartFileImport.Api.Tests;

public sealed class FilesControllerTests : IDisposable
{
    private readonly string _testDirectory;

    public FilesControllerTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "SmartFileImportTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testDirectory);
    }

    [Theory]
    [InlineData("employees.csv")]
    [InlineData("employees.xlsx")]
    public async Task Upload_WhenFileIsSupported_SavesFileToIncomingAndReturnsAccepted(string fileName)
    {
        var incomingFolder = GetIncomingFolder();
        var controller = CreateController(incomingFolder);
        var file = CreateFormFile(fileName, "test file contents");

        var result = await controller.Upload(file);

        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var response = Assert.IsType<FileUploadResponse>(acceptedResult.Value);
        Assert.Equal(fileName, response.FileName);
        Assert.Contains("queued", response.Message);
        Assert.Equal("test file contents", File.ReadAllText(Path.Combine(incomingFolder, fileName)));
    }

    [Fact]
    public async Task Upload_WhenFileTypeIsUnsupported_ReturnsBadRequestAndDoesNotSaveFile()
    {
        var incomingFolder = GetIncomingFolder();
        var controller = CreateController(incomingFolder);
        var file = CreateFormFile("employees.txt", "test file contents");

        var result = await controller.Upload(file);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<FileUploadErrorResponse>(badRequestResult.Value);
        Assert.Contains(".csv and .xlsx", response.Error);
        Assert.False(Directory.Exists(incomingFolder));
    }

    [Fact]
    public async Task Upload_WhenFileIsEmpty_ReturnsBadRequestAndDoesNotSaveFile()
    {
        var incomingFolder = GetIncomingFolder();
        var controller = CreateController(incomingFolder);
        var file = CreateFormFile("employees.csv", string.Empty);

        var result = await controller.Upload(file);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<FileUploadErrorResponse>(badRequestResult.Value);
        Assert.Contains("empty", response.Error);
        Assert.False(Directory.Exists(incomingFolder));
    }

    [Fact]
    public async Task Upload_WhenFileIsMissing_ReturnsBadRequestAndDoesNotSaveFile()
    {
        var incomingFolder = GetIncomingFolder();
        var controller = CreateController(incomingFolder);

        var result = await controller.Upload(null);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<FileUploadErrorResponse>(badRequestResult.Value);
        Assert.Contains("required", response.Error);
        Assert.False(Directory.Exists(incomingFolder));
    }

    [Fact]
    public async Task Upload_WhenFileNameAlreadyExists_SavesWithUniqueFileName()
    {
        var incomingFolder = GetIncomingFolder();
        Directory.CreateDirectory(incomingFolder);
        File.WriteAllText(Path.Combine(incomingFolder, "employees.csv"), "existing file");
        var controller = CreateController(incomingFolder);
        var file = CreateFormFile("employees.csv", "new file");

        var result = await controller.Upload(file);

        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var response = Assert.IsType<FileUploadResponse>(acceptedResult.Value);
        Assert.Equal("employees_1.csv", response.FileName);
        Assert.Equal("existing file", File.ReadAllText(Path.Combine(incomingFolder, "employees.csv")));
        Assert.Equal("new file", File.ReadAllText(Path.Combine(incomingFolder, "employees_1.csv")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private FilesController CreateController(string incomingFolder)
    {
        return new FilesController(
            Options.Create(new FileProcessingOptions
            {
                InputFolder = incomingFolder
            }),
            NullLogger<FilesController>.Instance);
    }

    private string GetIncomingFolder()
    {
        return Path.Combine(_testDirectory, "Incoming");
    }

    private static IFormFile CreateFormFile(string fileName, string contents)
    {
        var bytes = Encoding.UTF8.GetBytes(contents);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }
}
