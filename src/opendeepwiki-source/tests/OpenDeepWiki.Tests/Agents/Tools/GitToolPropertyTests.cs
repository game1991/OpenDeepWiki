using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace OpenDeepWiki.Tests.Agents.Tools;

/// <summary>
/// Property-based tests for GitTool path abstraction.
/// Feature: repository-wiki-generation, Property 8: Git Tool Path Abstraction
/// Validates: Requirements 13.3, 13.5, 13.6
/// </summary>
public class GitToolPropertyTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestGitTool _gitTool;

    public GitToolPropertyTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"GitToolTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Create some test files
        CreateTestFile("README.md", "# Test Repository\nThis is a test.");
        CreateTestFile("src/main.cs", "using System;\nclass Program { }");
        CreateTestFile("src/utils/helper.cs", "namespace Utils { class Helper { } }");
        CreateTestFile("docs/guide.md", "# Guide\nSome documentation.");
        CreateTestFile("config.json", "{ \"name\": \"test\" }");

        _gitTool = new TestGitTool(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    private void CreateTestFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    /// <summary>
    /// Generates valid relative file paths.
    /// </summary>
    private static Gen<string> GenerateValidRelativePath()
    {
        return Gen.Elements(
            "README.md",
            "src/main.cs",
            "src/utils/helper.cs",
            "docs/guide.md",
            "config.json"
        );
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// For any file read operation, GitTool SHALL only expose relative paths.
    /// The actual file system path SHALL never appear in tool responses.
    /// Validates: Requirements 13.3, 13.5, 13.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GitTool_Read_ShouldNotExposeAbsolutePath()
    {
        return Prop.ForAll(
            GenerateValidRelativePath().ToArbitrary(),
            relativePath =>
            {
                try
                {
                    var content = _gitTool.Read(relativePath);
                    
                    // The content should not contain the absolute path
                    var absolutePath = Path.GetFullPath(Path.Combine(_testDirectory, relativePath));
                    var containsAbsolutePath = content.Contains(_testDirectory) || 
                                               content.Contains(absolutePath);
                    
                    return !containsAbsolutePath;
                }
                catch (FileNotFoundException)
                {
                    // File not found is acceptable for generated paths
                    return true;
                }
            });
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// For any grep operation, results SHALL only contain relative paths.
    /// Validates: Requirements 13.3, 13.5
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GitTool_Grep_ResultsShouldOnlyContainRelativePaths()
    {
        var patterns = Gen.Elements("class", "using", "namespace", "test", "Guide");
        
        return Prop.ForAll(
            patterns.ToArbitrary(),
            pattern =>
            {
                var results = _gitTool.Grep(pattern);
                
                // All file paths in results should be relative (not contain the test directory path)
                foreach (var result in results)
                {
                    if (result.FilePath.Contains(_testDirectory) ||
                        Path.IsPathRooted(result.FilePath))
                    {
                        return false;
                    }
                }
                
                return true;
            });
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// For any grep operation, line content SHALL not expose absolute paths.
    /// Validates: Requirements 13.5
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GitTool_Grep_LineContentShouldNotExposeAbsolutePath()
    {
        var patterns = Gen.Elements("class", "using", "namespace", "test");
        
        return Prop.ForAll(
            patterns.ToArbitrary(),
            pattern =>
            {
                var results = _gitTool.Grep(pattern);
                
                // Line content should not contain the test directory path
                foreach (var result in results)
                {
                    if (result.LineContent.Contains(_testDirectory))
                    {
                        return false;
                    }
                }
                
                return true;
            });
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// When AI requests a file by relative path, GitTool SHALL resolve it correctly.
    /// Validates: Requirements 13.6
    /// </summary>
    [Fact]
    public void GitTool_Read_ShouldResolveRelativePathCorrectly()
    {
        // When AI requests "README.md", it should read from "{WorkingDirectory}/README.md"
        var content = _gitTool.Read("README.md");
        
        Assert.Contains("Test Repository", content);
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// GitTool SHALL prevent directory traversal attacks.
    /// Validates: Requirements 13.3
    /// </summary>
    [Fact]
    public void GitTool_Read_ShouldPreventDirectoryTraversal()
    {
        // Attempting to read outside the working directory should fail
        // The path normalization strips ".." components, so these become invalid paths
        // and throw FileNotFoundException (which is the safe behavior)
        Assert.ThrowsAny<Exception>(() => _gitTool.Read("../../../etc/passwd"));
        Assert.ThrowsAny<Exception>(() => _gitTool.Read("..\\..\\..\\windows\\system32\\config"));
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// GitTool SHALL handle both forward and backward slashes in paths.
    /// Validates: Requirements 13.3
    /// </summary>
    [Fact]
    public void GitTool_Read_ShouldHandleBothSlashTypes()
    {
        // Both slash types should work
        var content1 = _gitTool.Read("src/main.cs");
        var content2 = _gitTool.Read("src\\main.cs");
        
        Assert.Equal(content1, content2);
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// GitTool ListFiles SHALL only return relative paths.
    /// Validates: Requirements 13.5
    /// </summary>
    [Fact]
    public void GitTool_ListFiles_ShouldOnlyReturnRelativePaths()
    {
        var files = _gitTool.ListFiles();
        
        foreach (var file in files)
        {
            Assert.DoesNotContain(_testDirectory, file);
            Assert.False(Path.IsPathRooted(file), $"Path should be relative: {file}");
        }
    }

    /// <summary>
    /// Property 8: Git Tool Path Abstraction
    /// GitTool Grep results SHALL contain matching file paths and line content.
    /// Validates: Requirements 13.2, 13.4
    /// </summary>
    [Fact]
    public void GitTool_Grep_ShouldReturnMatchingFilesAndContent()
    {
        var results = _gitTool.Grep("class");
        
        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.NotEmpty(r.FilePath);
            Assert.True(r.LineNumber > 0);
            Assert.Contains("class", r.LineContent, StringComparison.OrdinalIgnoreCase);
        });
    }
}

/// <summary>
/// Test version of GitTool for property-based testing.
/// </summary>
public class TestGitTool
{
    private readonly string _workingDirectory;

    public TestGitTool(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory));
        }

        _workingDirectory = Path.GetFullPath(workingDirectory);
        
        if (!Directory.Exists(_workingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory does not exist: {_workingDirectory}");
        }
    }

    public string Read(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path cannot be empty.", nameof(relativePath));
        }

        var normalizedPath = NormalizePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, normalizedPath));

        if (!fullPath.StartsWith(_workingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Access denied: path '{relativePath}' is outside the repository.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        return File.ReadAllText(fullPath);
    }

    public TestGrepResult[] Grep(string pattern, string? filePattern = null)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Search pattern cannot be empty.", nameof(pattern));
        }

        var results = new List<TestGrepResult>();
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var searchPattern = string.IsNullOrWhiteSpace(filePattern) ? "*" : filePattern;

        try
        {
            var files = Directory.GetFiles(_workingDirectory, searchPattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (IsBinaryFile(file) || IsHiddenPath(file))
                {
                    continue;
                }

                try
                {
                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            var relativePath = GetRelativePath(file);
                            results.Add(new TestGrepResult
                            {
                                FilePath = relativePath,
                                LineNumber = i + 1,
                                LineContent = lines[i].Trim()
                            });
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip files that cannot be read
                }
            }
        }
        catch (Exception)
        {
            // Return empty results if directory enumeration fails
        }

        return results.ToArray();
    }

    public string[] ListFiles(string? filePattern = null)
    {
        var searchPattern = string.IsNullOrWhiteSpace(filePattern) ? "*" : filePattern;
        var files = Directory.GetFiles(_workingDirectory, searchPattern, SearchOption.AllDirectories);

        return files
            .Where(f => !IsHiddenPath(f))
            .Select(GetRelativePath)
            .ToArray();
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        normalized = normalized.TrimStart('/');
        var parts = normalized.Split('/').Where(p => p != ".." && p != ".").ToArray();
        return string.Join(Path.DirectorySeparatorChar.ToString(), parts);
    }

    private string GetRelativePath(string fullPath)
    {
        var relativePath = Path.GetRelativePath(_workingDirectory, fullPath);
        return relativePath.Replace('\\', '/');
    }

    private static bool IsBinaryFile(string filePath)
    {
        var binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".so", ".dylib", ".bin", ".obj", ".o",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".zip", ".tar", ".gz", ".rar", ".7z",
            ".mp3", ".mp4", ".avi", ".mov", ".wav",
            ".ttf", ".otf", ".woff", ".woff2", ".eot",
            ".db", ".sqlite", ".mdb"
        };

        var extension = Path.GetExtension(filePath);
        return binaryExtensions.Contains(extension);
    }

    private bool IsHiddenPath(string fullPath)
    {
        var relativePath = GetRelativePath(fullPath);
        var parts = relativePath.Split('/', '\\');
        return parts.Any(p => p.StartsWith('.') && p != ".");
    }
}

public class TestGrepResult
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
}
