using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class AiPatchConflictPromptWriterTests
{
    [Fact]
    public void CreateChinesePrompt_WhenCreatedThenIncludesPatchPath()
    {
        var prompt = AiPatchConflictPromptWriter.CreateChinesePrompt(CreateContext());

        Assert.Contains("C:\\relay\\source.patch", prompt);
    }

    [Fact]
    public void CreateChinesePrompt_WhenCreatedThenForbidsCommit()
    {
        var prompt = AiPatchConflictPromptWriter.CreateChinesePrompt(CreateContext());

        Assert.Contains("不要执行 `git commit`", prompt);
    }

    [Fact]
    public void CreateEnglishPrompt_WhenCreatedThenForbidsCommit()
    {
        var prompt = AiPatchConflictPromptWriter.CreateEnglishPrompt(CreateContext());

        Assert.Contains("Do not run `git commit`", prompt);
    }

    [Fact]
    public void CreateEnglishPrompt_WhenCreatedThenIncludesResumeCommand()
    {
        var prompt = AiPatchConflictPromptWriter.CreateEnglishPrompt(CreateContext());

        Assert.Contains("--resume-workspace \"C:\\relay\" --allow-untrusted-build", prompt);
    }

    private static AiPatchConflictPromptContext CreateContext() =>
        new
        (
            "C:\\relay",
            "https://github.com/dotnet/wpf/pull/11124",
            "1111111111111111111111111111111111111111",
            "2222222222222222222222222222222222222222",
            "lindexi/WpfRuntime",
            "main",
            "C:\\relay\\repository",
            "C:\\relay\\source.patch",
            "C:\\relay",
            "D:\\WpfRuntime\\eng\\Builder\\Builder.csproj"
        );
}
