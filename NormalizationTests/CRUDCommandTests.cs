using LantanaGroup.Link.Normalization.Application.Commands.Config;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace NormalizationTests;

public class CRUDCommandTests
{
    [Fact]
    public async Task DeleteConfigCommand_Success() 
    {
        var logger = new Mock<ILogger<DeleteConfigCommandHandler>>();
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var command = new DeleteConfigCommand
        {
            FacilityId = "test"
        };

        var handler = new DeleteConfigCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        var command = new DeleteConfigCommand
        {
            FacilityId = null
        };

        var handler = new DeleteConfigCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfigEntity());

        var command = new GetConfigurationEntityQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationEntityQueryHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfigEntity)null);

        var command = new GetConfigurationEntityQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationEntityQueryHandler(logger.Object, configRepo.Object);

        try
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.Null(result);
        }
        catch(NoEntityFoundException)
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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfigEntity());

        var command = new GetConfigurationModelQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationModelQueryHandler(configRepo.Object, logger.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfigEntity)null);

        var command = new GetConfigurationModelQuery
        {
            FacilityId = "test"
        };

        var handler = new GetConfigurationModelQueryHandler(configRepo.Object, logger.Object);

        try
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.Null(result);
        }
        catch(NoEntityFoundException)
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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfigEntity());
        configRepo.Setup(x => x.UpdateAsync(It.IsAny<NormalizationConfigEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfigEntity)null);

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfigEntity)null);
        configRepo.Setup(x => x.AddAsync(It.IsAny<NormalizationConfigEntity>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = null,
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = null
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        var command = new SaveConfigEntityCommand
        {
            NormalizationConfigModel = new NormalizationConfigModel
            {
                FacilityId = "test",
                OperationSequence = new Dictionary<string, INormalizationOperation>()
            },
            Source = SaveTypeSource.Update
        };

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NormalizationConfigEntity { FacilityId = "test" });

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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
        var configRepo = new Mock<IConfigRepository>();

        configRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((NormalizationConfigEntity)null);

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

        var handler = new SaveConfigEntityCommandHandler(logger.Object, configRepo.Object);

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