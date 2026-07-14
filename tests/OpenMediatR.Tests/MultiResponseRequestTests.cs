using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR.Tests;

/// <summary>
/// A request type may implement IRequest&lt;T&gt; more than once (typically one interface
/// inherited from a base record plus one declared directly). Send&lt;TResponse&gt; must route
/// to the handler matching the requested response type instead of guessing the response
/// type from the first IRequest&lt;&gt; interface reflection happens to return.
/// </summary>
public sealed class MultiResponseRequestTests
{
    private static ServiceProvider BuildProvider(bool withOpenBehaviors = false)
    {
        var services = new ServiceCollection();
        services.AddOpenMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<MultiResponseRequestTests>();
            if (withOpenBehaviors)
                cfg.AddOpenBehavior(typeof(TestPipelineBehavior1<,>));
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_MultiResponseRequest_WithDirectResponseType_ResolvesMatchingHandler()
    {
        using var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send<int>(new MultiResponseRequest());

        result.Should().Be(42);
    }

    [Fact]
    public async Task Send_MultiResponseRequest_WithInheritedResponseType_ResolvesMatchingHandler()
    {
        using var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send<string>(new MultiResponseRequest());

        result.Should().Be("base");
    }

    [Fact]
    public async Task Send_MultiResponseRequest_InterleavedResponseTypes_DoNotPoisonMetadataCache()
    {
        using var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var request = new MultiResponseRequest();

        var first = await sender.Send<string>(request);
        var second = await sender.Send<int>(request);
        var third = await sender.Send<string>(request);

        first.Should().Be("base");
        second.Should().Be(42);
        third.Should().Be("base");
    }

    [Fact]
    public async Task Send_MultiResponseRequest_WithPipelineBehaviors_RoutesBothResponseTypes()
    {
        using var provider = BuildProvider(withOpenBehaviors: true);
        var sender = provider.GetRequiredService<ISender>();

        var direct = await sender.Send<int>(new MultiResponseRequest());
        var inherited = await sender.Send<string>(new MultiResponseRequest());

        direct.Should().Be(42);
        inherited.Should().Be("base");
    }

    [Fact]
    public async Task CreateStream_MultiResponseStreamRequest_ResolvesHandlerMatchingRequestedResponseType()
    {
        using var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var ints = new List<int>();
        await foreach (var value in sender.CreateStream<int>(new MultiResponseStreamRequest(3)))
            ints.Add(value);

        var strings = new List<string>();
        await foreach (var value in sender.CreateStream<string>(new MultiResponseStreamRequest(2)))
            strings.Add(value);

        ints.Should().Equal(0, 1, 2);
        strings.Should().Equal("base0", "base1");
    }
}
