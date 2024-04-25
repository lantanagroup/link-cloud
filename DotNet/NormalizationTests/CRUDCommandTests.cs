using Grpc.Net.Client.Balancer;
using LantanaGroup.Link.Normalization.Application.Commands.Config;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using System.Net.Sockets;

namespace NormalizationTests;

public class CRUDCommandTests
{
    [Fact]
    public async Task DeleteConfigCommand_Success()
    {
        var logger = new Mock<ILogger<DeleteConfigCommandHandler>>();
        var mockContext = new Mock<NormalizationDbContext>();

        mockContext.Setup(x => x.Remove(It.IsAny<NormalizationConfig>()));

        var command = new DeleteConfigCommand
        {
            FacilityId = "test"
        };

        var handler = new DeleteConfigCommandHandler(logger.Object, mockContext.Object);

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
    public async Task DeleteConfigCommand_NullFacilityId()
    {
        var logger = new Mock<ILogger<DeleteConfigCommandHandler>>();
        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var command = new DeleteConfigCommand
        {
            FacilityId = null
        };

        var handler = new DeleteConfigCommandHandler(logger.Object, mockContext.Object);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfig());

        var command = new GetConfigurationEntityQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationEntityQueryHandler(logger.Object, mockContext.Object);

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


        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfig?)null);

        var command = new GetConfigurationEntityQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationEntityQueryHandler(logger.Object, mockContext.Object);

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
    public async Task GetConfigurationModelQuery_Success()
    {
        var logger = new Mock<ILogger<GetConfigurationModelQueryHandler>>();

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfig());

        var command = new GetConfigurationModelQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationModelQueryHandler(mockContext.Object, logger.Object);

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
    public async Task GetConfigurationMoldeQuery_NoConfigExists()
    {
        var logger = new Mock<ILogger<GetConfigurationModelQueryHandler>>();

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfig?)null);

        var command = new GetConfigurationModelQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationModelQueryHandler(mockContext.Object, logger.Object);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfig());
        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfig?)null);

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfig?)null);
        mockContext.Setup(x => x.NormalizationConfigs.AddAsync(It.IsAny<NormalizationConfig>(), It.IsAny<CancellationToken>()));

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = null,
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = null
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = new Dictionary<string, INormalizationOperation>()
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var service = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfig { FacilityId = "test" });

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, service);

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

        var mockSet = new Mock<DbSet<NormalizationConfig>>();

        var mockContext = new Mock<NormalizationDbContext>();
        mockContext.Setup(m => m.NormalizationConfigs).Returns(mockSet.Object);

        var _mocker = new AutoMocker();
        var settings = _mocker.CreateInstance<TenantServiceRegistration>();
        settings.CheckIfTenantExists = false;
        _mocker.Use(settings);

        var apiService = (ITenantApiService)_mocker.CreateInstance<TenantApiService>();

        mockContext.Setup(x => x.NormalizationConfigs.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfig?)null);

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, mockContext.Object, apiService);

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