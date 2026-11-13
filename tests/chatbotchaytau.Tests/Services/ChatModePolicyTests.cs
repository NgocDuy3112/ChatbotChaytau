using chatbotchaytau.Services;

namespace chatbotchaytau.Tests.Services;

public class ChatModePolicyTests
{
    [Theory]
    [InlineData(ChatMode.Ask, false)]
    [InlineData(ChatMode.Agent, true)]
    public void Tools_run_only_in_agent_mode(ChatMode mode, bool expected)
    {
        Assert.Equal(expected, ChatModePolicy.UsesTools(mode));
    }

    [Theory]
    [InlineData(ChatMode.Ask)]
    [InlineData(ChatMode.Agent)]
    public void User_pin_always_wins(ChatMode mode)
    {
        Assert.Equal("pinned/model",
            ChatModePolicy.ForcedModel(mode, pinnedModel: "pinned/model", strongModel: "strong/model"));
    }

    [Fact]
    public void Ask_leaves_model_choice_to_the_router()
    {
        Assert.Null(ChatModePolicy.ForcedModel(ChatMode.Ask, pinnedModel: null, strongModel: "strong/model"));
    }

    [Fact]
    public void Agent_forces_the_strong_model_without_a_pin()
    {
        Assert.Equal("strong/model",
            ChatModePolicy.ForcedModel(ChatMode.Agent, pinnedModel: null, strongModel: "strong/model"));
    }

    [Fact]
    public void Missing_strong_config_degrades_to_router_instead_of_crashing()
    {
        Assert.Null(ChatModePolicy.ForcedModel(ChatMode.Agent, pinnedModel: null, strongModel: null));
        Assert.Null(ChatModePolicy.ForcedModel(ChatMode.Agent, pinnedModel: null, strongModel: "  "));
    }
}
