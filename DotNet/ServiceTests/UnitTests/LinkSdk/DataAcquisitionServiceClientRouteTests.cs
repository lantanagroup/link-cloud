using System.Collections;
using System.Reflection;
using FluentAssertions;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

/// <summary>
/// Calls every <see cref="IDataAcquisitionServiceClient"/> method against Data Acquisition's real routes and
/// checks each request lands on a controller action with a matching HTTP method.
/// </summary>
/// <remarks>
/// The per-method tests in DataAcquisitionServiceClientTests assert an expected path, so they only catch a
/// mistake in a method someone wrote a test for. This one needs no expected paths, so it covers every method,
/// including ones added later.
/// </remarks>
[Trait("Category", "UnitTests")]
public class DataAcquisitionServiceClientRouteTests : IClassFixture<DataAcquisitionRouteProbeServer>
{
    // Guid-shaped, so it also satisfies {id:guid} route constraints wherever a string id lands in a path.
    private const string SampleId = "7d9f7c1e-3b1a-4c55-9d7e-2f1e0f4a6b10";
    private const int MaxObjectDepth = 3;

    private readonly DataAcquisitionRouteProbeServer _server;

    public DataAcquisitionServiceClientRouteTests(DataAcquisitionRouteProbeServer server)
    {
        _server = server;
    }

    public static TheoryData<string> ClientMethods()
    {
        var names = new TheoryData<string>();
        foreach (var name in typeof(IDataAcquisitionServiceClient).GetMethods().Select(m => m.Name).Distinct())
        {
            names.Add(name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(ClientMethods))]
    public async Task ClientMethod_AgainstDataAcquisitionRoutes_ReachesAControllerAction(string methodName)
    {
        using var client = CreateClient(_server.BaseUrl);
        var overloads = typeof(IDataAcquisitionServiceClient)
            .GetMethods()
            .Where(m => m.Name == methodName);

        foreach (var method in overloads)
        {
            _server.TakeRequests();
            var arguments = method
                .GetParameters()
                .Select(p => CreateValue(p.ParameterType, depth: 0))
                .ToArray();

            await ((Task)method.Invoke(client, arguments)!).WaitAsync(TimeSpan.FromSeconds(10));

            var requests = _server.TakeRequests();
            requests.Should().NotBeEmpty("{0} should call Data Acquisition", method);
            foreach (var request in requests)
            {
                request.MatchedAction
                    .Should()
                    .NotBeNull("{0} sent {1} {2}, which no Data Acquisition action serves",
                               method.Name,
                               request.Method,
                               request.Path);
            }
        }
    }

    private static object? CreateValue(Type type, int depth)
    {
        if (type == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return CreateValue(underlying, depth);
        }

        if (type == typeof(string))
        {
            return SampleId;
        }

        if (type == typeof(Guid))
        {
            return Guid.Parse(SampleId);
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(DateTime))
        {
            return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        if (type == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsPrimitive || type == typeof(decimal))
        {
            return Convert.ChangeType(1, type);
        }

        if (type == typeof(object))
        {
            return new { };
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            var listType = typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]);
            if (type.IsAssignableFrom(listType))
            {
                return Activator.CreateInstance(listType);
            }
        }

        if (depth >= MaxObjectDepth || type.IsAbstract || type.IsInterface)
        {
            return null;
        }

        var parameterless = type.GetConstructor(Type.EmptyTypes);
        if (parameterless is not null)
        {
            return parameterless.Invoke(null);
        }

        // Records and other types with only parameterised constructors.
        var constructor = type
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        return constructor?.Invoke(constructor
            .GetParameters()
            .Select(p => CreateValue(p.ParameterType, depth + 1))
            .ToArray());
    }

    private static DataAcquisitionServiceClient CreateClient(string baseUrl)
    {
        var serviceRegistry = Options.Create(new ServiceRegistry
        {
            DataAcquisitionServiceUrl = baseUrl
        });

        var bearerOptions = Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions
        {
            AllowAnonymous = true
        });

        var tokenSettings = Options.Create(new LinkTokenServiceSettings
        {
            SigningKey = "test"
        });

        return new DataAcquisitionServiceClient(serviceRegistry,
                                                bearerOptions,
                                                tokenSettings,
                                                new Mock<ICreateSystemToken>().Object);
    }
}
