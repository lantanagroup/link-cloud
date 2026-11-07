using LantanaGroup.Link.Shared.Application.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Shared
{
    /// <summary>
    /// Every log line names the instance that wrote it, so a scaled-out service's copies can be told apart: the name
    /// a host gives it, else the pod's (HOSTNAME), else the machine's.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class ServiceInstanceLoggingExtensionsTests
    {
        private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
            name => variables.FirstOrDefault(v => v.Name == name).Value;

        [Fact]
        public void The_name_a_host_sets_wins()
        {
            var name = ServiceInstanceLoggingExtensions.ResolveInstanceName(
                Environment(("LINK_SERVICE_INSTANCE", "validation-2"), ("HOSTNAME", "laptop")));

            Assert.Equal("validation-2", name);
        }

        [Fact]
        public void Without_one_the_pod_name_is_used()
        {
            var name = ServiceInstanceLoggingExtensions.ResolveInstanceName(
                Environment(("HOSTNAME", "report-7d9f8c6b5-x2kq4")));

            Assert.Equal("report-7d9f8c6b5-x2kq4", name);
        }

        [Fact]
        public void Without_either_the_machine_name_is_used()
        {
            var name = ServiceInstanceLoggingExtensions.ResolveInstanceName(Environment(("LINK_SERVICE_INSTANCE", "  ")));

            Assert.Equal(System.Environment.MachineName, name);
        }

        [Fact]
        public void It_is_added_as_a_Serilog_property()
        {
            var configuration = new ConfigurationManager();

            configuration.AddServiceInstanceLogProperty();

            Assert.False(string.IsNullOrWhiteSpace(configuration["Serilog:Properties:ServiceInstance"]));
        }

        [Fact]
        public void A_configured_value_is_left_alone()
        {
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Serilog:Properties:ServiceInstance"] = "explicit" });

            configuration.AddServiceInstanceLogProperty();

            Assert.Equal("explicit", configuration["Serilog:Properties:ServiceInstance"]);
        }
    }
}
