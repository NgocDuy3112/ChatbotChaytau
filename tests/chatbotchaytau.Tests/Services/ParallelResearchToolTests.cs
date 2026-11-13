using System.ClientModel;
using chatbotchaytau.Data;
using chatbotchaytau.Services.Agents;
using chatbotchaytau.Services.Usage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OpenAI;

namespace chatbotchaytau.Tests.Services;

public class ParallelResearchToolTests
{
    // --- fakes ---------------------------------------------------------------

    private sealed class RecordingTracker : IUsageTracker
    {
        public Task RecordAsync(UsageEvent evt, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "chatbotchaytau.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }

    /// <summary>Runs the real batching/retry logic with a faked worker — no network.</summary>
    private sealed class TestTool : ParallelResearchTool
    {
        public TestTool()
            : base(
                new OpenAIClient(new ApiKeyCredential("test-key"),
                    new OpenAIClientOptions { Endpoint = new Uri("https://example.invalid/") }),
                new RecordingTracker(),
                new PricingTable(new ConfigurationBuilder().Build()),
                new FakeWebHostEnvironment())
        {
        }

        public List<string> Seen { get; } = [];
        public bool CancelNextCall { get; set; }

        internal override Task<string> RunWorkerAsync(
            string task, string userId, string model, CancellationToken ct)
        {
            Seen.Add(task);
            ct.ThrowIfCancellationRequested();

            if (CancelNextCall) throw new OperationCanceledException(ct);
            if (task.Contains("bad", StringComparison.Ordinal))
                throw new InvalidOperationException("worker exploded");

            return Task.FromResult($"OK:{task}");
        }
    }

    // --- tests ----------------------------------------------------------------

    [Fact]
    public async Task Empty_or_blank_tasks_return_an_error_without_running_workers()
    {
        var tool = new TestTool();

        var result = await tool.RunAsync([], "user-1", "model-x", CancellationToken.None);
        Assert.StartsWith("Error:", result);

        result = await tool.RunAsync(["  ", ""], "user-1", "model-x", CancellationToken.None);
        Assert.StartsWith("Error:", result);

        Assert.Empty(tool.Seen);
    }

    [Fact]
    public async Task Runs_all_tasks_and_merges_their_findings()
    {
        var tool = new TestTool();

        var result = await tool.RunAsync(
            ["summarise chapter 1", "extract key dates"],
            "user-1", "model-x", CancellationToken.None);

        Assert.Equal(2, tool.Seen.Count);
        Assert.Contains("=== sub-task 1/2: summarise chapter 1 ===", result);
        Assert.Contains("=== sub-task 2/2: extract key dates ===", result);
        Assert.Contains("OK:summarise chapter 1", result);
        Assert.Contains("OK:extract key dates", result);
    }

    [Fact]
    public async Task Caps_the_fan_out_at_three_workers_and_says_so()
    {
        var tool = new TestTool();

        var result = await tool.RunAsync(
            ["task a", "task b", "task c", "task d", "task e"],
            "user-1", "model-x", CancellationToken.None);

        Assert.Equal(ParallelResearchTool.MaxWorkers, tool.Seen.Count);
        Assert.Equal(["task a", "task b", "task c"], tool.Seen);
        Assert.Contains("only the first 3 of 5 tasks were run", result);
        Assert.DoesNotContain("task d", result);
    }

    [Fact]
    public async Task One_failing_task_retries_once_and_never_kills_the_batch()
    {
        var tool = new TestTool();

        var result = await tool.RunAsync(
            ["healthy task", "bad task"],
            "user-1", "model-x", CancellationToken.None);

        // failing task attempted twice (retry), healthy once
        Assert.Equal(2, tool.Seen.Count(t => t == "bad task"));
        Assert.Single(tool.Seen, t => t == "healthy task");

        Assert.Contains("failed after 2 attempts: worker exploded", result);
        Assert.Contains("OK:healthy task", result);   // sibling result survives
    }

    [Fact]
    public async Task Cancellation_aborts_the_fan_out_instead_of_retrying()
    {
        var tool = new TestTool { CancelNextCall = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            tool.RunAsync(["task a"], "user-1", "model-x", CancellationToken.None));
    }
}
