using LantanaGroup.Link.Normalization.Application.Commands.Config;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;

namespace NormalizationTests;

public class CRUDCommandTests
{
    [Fact]
    public async Task DeleteConfigCommand_Success()
    {
        var logger = new Mock<ILogger<DeleteConfigCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "DeleteConfigCommand_Success")
            .Options;

        var command = new DeleteConfigCommand
        {
            FacilityId = "test"
        };

        var context = new NormalizationDbContext(options);

        context.Add(new NormalizationConfig()
        {
            FacilityId = "test",
        });

        await context.SaveChangesAsync();

        var handler = new DeleteConfigCommandHandler(logger.Object, context);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(true);
        }
        catch (ConfigOperationNullException)
        {
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task DeleteConfigCommand_NullFacilityId()
    {
        var logger = new Mock<ILogger<DeleteConfigCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "DeleteConfigCommand_NullFacilityId")
            .Options;

        var command = new DeleteConfigCommand
        {
            FacilityId = null
        };

        var handler = new DeleteConfigCommandHandler(logger.Object, new NormalizationDbContext(options));

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (ConfigOperationNullException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task GetConfigurationEntityQuery_Success()
    {
        var logger = new Mock<ILogger<GetConfigurationEntityQueryHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "GetConfigurationEntityQuery_Success")
            .Options;

        var context = new NormalizationDbContext(options);

        //var mocker = new AutoMocker();
        //mocker.GetMock<IServiceScopeFactory>().Setup(f => f.CreateScope()).Returns(mocker.GetMock<IServiceScope>().Object);
        //mocker.GetMock<IServiceScope>().Setup(s => s.ServiceProvider.GetRequiredService<NormalizationDbContext>()).Returns(context);

        context.Add(new NormalizationConfig()
        {
            FacilityId = "test",
        });

        await context.SaveChangesAsync();

        var command = new GetConfigurationEntityQuery
        {
            FacilityId = "test"
        };

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(s => s.GetService(typeof(NormalizationDbContext))).Returns(context);
        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(s => s.CreateScope()).Returns(serviceScope.Object);

        var handler = new GetConfigurationEntityQueryHandler(logger.Object, scopeFactory.Object);

        try
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(result);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task GetConfigurationEntityQuery_NoConfigExists()
    {
        var logger = new Mock<ILogger<GetConfigurationEntityQueryHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "GetConfigurationEntityQuery_NoConfigExists")
            .Options;
        var context = new NormalizationDbContext(options);

        var command = new GetConfigurationEntityQuery
        {
            FacilityId = "test"
        };

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(s => s.GetService(typeof(NormalizationDbContext))).Returns(context);
        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(s => s.CreateScope()).Returns(serviceScope.Object);
        var handler = new GetConfigurationEntityQueryHandler(logger.Object, scopeFactory.Object);

        try
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.Null(result);
        }
        catch (NoEntityFoundException)
        {
            Assert.True(true);
        }
        catch (Exception ex)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task GetConfigurationModelQuery_Success()
    {
        var logger = new Mock<ILogger<GetConfigurationModelQueryHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "GetConfigurationModelQuery_Success")
            .Options;

        var context = new NormalizationDbContext(options);

        context.Add(new NormalizationConfig()
        {
            FacilityId = "test",
        });

        await context.SaveChangesAsync();

        var command = new GetConfigurationModelQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationModelQueryHandler(context, logger.Object);

        try
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(result);
        }
        catch (Exception ex)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task GetConfigurationMoldeQuery_NoConfigExists()
    {
        var logger = new Mock<ILogger<GetConfigurationModelQueryHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "GetConfigurationMoldeQuery_NoConfigExists")
            .Options;

        var command = new GetConfigurationModelQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationModelQueryHandler(new NormalizationDbContext(options), logger.Object);

        try
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.Null(result);
        }
        catch (NoEntityFoundException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_Update_Success()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_Update_Success")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var context = new NormalizationDbContext(options);

        context.Add(new NormalizationConfig()
        {
            FacilityId = "test",
        });

        await context.SaveChangesAsync();

        var command = new SaveConfigEntityCommand
        {
            FacilityId = "test",
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = new Dictionary<string, INormalizationOperation>
                {
                    { "1", new CopyElementOperation{ Name = "copyElement" } }
                }
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, context, apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_Create_Success()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_Create_Success")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = new Dictionary<string, INormalizationOperation>
                {
                    { "1", new CopyElementOperation{ Name = "copyElement" } }
                }
            },
            Source = SaveTypeSource.Create
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, new NormalizationDbContext(options), apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_Update_NoFacilityId()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_Update_NoFacilityId")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "",
                OperationSequence = new Dictionary<string, INormalizationOperation>
                {
                    { "1", new CopyElementOperation{ Name = "copyElement" } }
                }
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, new NormalizationDbContext(options), apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (ConfigOperationNullException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_NullConfigModel()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_NullConfigModel")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = null,
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, new NormalizationDbContext(options), apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (ConfigOperationNullException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_NullFacilityId()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_NullFacilityId")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = null,
                OperationSequence = new Dictionary<string, INormalizationOperation>
                {
                    { "1", new CopyElementOperation{ Name = "copyElement" } }
                }
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, new NormalizationDbContext(options), apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (ConfigOperationNullException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_NullOperationSequence()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_NullOperationSequence")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = null
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, new NormalizationDbContext(options), apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (ConfigOperationNullException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_EmptyOperationSequence()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_EmptyOperationSequence")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = new Dictionary<string, INormalizationOperation>()
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, new NormalizationDbContext(options), apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (ConfigOperationNullException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_Create_EntityAlreadyExists()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_Create_EntityAlreadyExists")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var context = new NormalizationDbContext(options);
        context.Add(new NormalizationConfig()
        {
            FacilityId = "test",
        });

        await context.SaveChangesAsync();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = new Dictionary<string, INormalizationOperation>
                {
                    { "1", new CopyElementOperation{ Name = "copyElement" } }
                }
            },
            Source = SaveTypeSource.Create
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, context, apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (EntityAlreadyExistsException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public async Task SaveConfigEntityCommand_Update_NoEntityFound()
    {
        var logger = new Mock<ILogger<SaveConfigEntityCommandHandler>>();

        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseInMemoryDatabase(databaseName: "SaveConfigEntityCommand_Update_NoEntityFound")
            .Options;

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var serviceRegistry = _mocker.CreateInstance<ServiceRegistry>();
        serviceRegistry.TenantService = settings;
        _mocker.Use(serviceRegistry);

        var ios = Options.Create(serviceRegistry);
        _mocker.Use(ios);

        var apiService = _mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            FacilityId = "test",
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = new Dictionary<string, INormalizationOperation>
                {
                    { "1", new CopyElementOperation{ Name = "copyElement" } }
                }
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, new NormalizationDbContext(options), apiService);

        try
        {
            await handler.Handle(command, CancellationToken.None);
            Assert.True(false);
        }
        catch (NoEntityFoundException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }
}