using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace ReleasePilot.Agent;

public static class AgentServiceRegistration
{
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        var openAiKey = configuration.GetValue<string>("OpenAI:ApiKey");
        var openAiModel = configuration.GetValue<string>("OpenAI:Model") ?? "gpt-4";

        if (!string.IsNullOrEmpty(openAiKey))
        {
            // Register Semantic Kernel with a real LLM backend
            services.AddSingleton(sp =>
            {
                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(openAiModel, openAiKey);
                return builder.Build();
            });
        }
        else
        {
            // No LLM key configured — agent will use mocked backend
            services.AddSingleton<Kernel>(sp => null!);
        }

        services.AddScoped<ReleaseNotesAgent>();
    }
}
