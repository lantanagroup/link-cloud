using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class QueryPlanTemplateResolverTests
{
    private static QueryPlanTemplate MakeTemplate(string name = "T1", string ehr = "Epic", string lookBack = "P30D")
    {
        return new QueryPlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            EhrDescription = ehr,
            LookBack = lookBack,
            InitialQueries =
            [
                new QueryEntry
                {
                    ResourceType = "Encounter",
                    QueryConfigType = "Parameter",
                    Parameters =
                    [
                        new QueryParameter
                        {
                            ParameterType = "Variable",
                            Name = "patient",
                            Variable = 0,
                            Format = "Patient/{0}",
                        }
                    ]
                }
            ],
            SupplementalQueries =
            [
                new QueryEntry
                {
                    ResourceType = "Condition",
                    QueryConfigType = "Reference",
                    OperationType = 2,
                    Paged = 100,
                }
            ],
        };
    }

    [Fact]
    public async Task Returns_BuiltIn_Default_when_no_template_id_and_no_default_in_store()
    {
        var store = new Mock<IQueryPlanTemplateStore>();
        store.Setup(s => s.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((QueryPlanTemplate?)null);
        var resolver = new QueryPlanTemplateResolver(store.Object);

        var result = await resolver.ResolveAsync(templateId: null);

        result.Input.Should().BeNull();
        result.Name.Should().Be("Built-in Default");
        store.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Falls_back_to_store_default_when_template_id_is_null()
    {
        var defaultTemplate = MakeTemplate(name: "Default Plan");
        var store = new Mock<IQueryPlanTemplateStore>();
        store.Setup(s => s.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(defaultTemplate);
        var resolver = new QueryPlanTemplateResolver(store.Object);

        var result = await resolver.ResolveAsync(templateId: null);

        result.Name.Should().Be("Default Plan");
        result.Input.Should().NotBeNull();
        result.Input!.EhrDescription.Should().Be("Epic");
        store.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Falls_back_to_store_default_when_explicit_template_id_not_found()
    {
        var defaultTemplate = MakeTemplate(name: "Fallback Default");
        var unknownId = Guid.NewGuid();
        var store = new Mock<IQueryPlanTemplateStore>();
        store.Setup(s => s.GetByIdAsync(unknownId, It.IsAny<CancellationToken>())).ReturnsAsync((QueryPlanTemplate?)null);
        store.Setup(s => s.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(defaultTemplate);
        var resolver = new QueryPlanTemplateResolver(store.Object);

        var result = await resolver.ResolveAsync(unknownId);

        result.Name.Should().Be("Fallback Default");
        result.Input.Should().NotBeNull();
    }

    [Fact]
    public async Task Returns_explicit_template_when_id_resolves()
    {
        var template = MakeTemplate(name: "Explicit Plan", ehr: "Cerner", lookBack: "P14D");
        var store = new Mock<IQueryPlanTemplateStore>();
        store.Setup(s => s.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var resolver = new QueryPlanTemplateResolver(store.Object);

        var result = await resolver.ResolveAsync(template.Id);

        result.Name.Should().Be("Explicit Plan");
        result.Input.Should().NotBeNull();
        result.Input!.EhrDescription.Should().Be("Cerner");
        result.Input.LookBack.Should().Be("P14D");
        store.Verify(s => s.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never,
            "an explicit, resolvable template id must short-circuit the default lookup");
    }

    [Fact]
    public async Task Maps_initial_and_supplemental_queries_with_parameters()
    {
        var template = MakeTemplate();
        var store = new Mock<IQueryPlanTemplateStore>();
        store.Setup(s => s.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var resolver = new QueryPlanTemplateResolver(store.Object);

        var result = await resolver.ResolveAsync(template.Id);

        var input = result.Input!;
        input.InitialQueries.Should().ContainSingle();
        input.InitialQueries[0].ResourceType.Should().Be("Encounter");
        input.InitialQueries[0].QueryConfigType.Should().Be("Parameter");
        input.InitialQueries[0].Parameters.Should().ContainSingle();
        var param = input.InitialQueries[0].Parameters[0];
        param.ParameterType.Should().Be("Variable");
        param.Name.Should().Be("patient");
        param.Variable.Should().Be(0);
        param.Format.Should().Be("Patient/{0}");

        input.SupplementalQueries.Should().ContainSingle();
        input.SupplementalQueries[0].ResourceType.Should().Be("Condition");
        input.SupplementalQueries[0].QueryConfigType.Should().Be("Reference");
        input.SupplementalQueries[0].OperationType.Should().Be(2);
        input.SupplementalQueries[0].Paged.Should().Be(100);
    }
}
