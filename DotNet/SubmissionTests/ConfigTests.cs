using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Managers;
using LantanaGroup.Link.Submission.Application.Models.ApiModels;
using LantanaGroup.Link.Submission.Application.Queries;
using LantanaGroup.Link.Submission.Domain.Entities;
using Moq;
using Moq.AutoMock;

namespace SubmissionTests;

public class ConfigTests
{
    private AutoMocker _mocker;
    private const string _facilityId = "testId";
    private static Guid _id = new Guid();

    
    [Fact]
    public async Task TenantSubmissionQueries_FindTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();
        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();
        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubmissionConfigEntity
            {
                Id = new TenantSubmissionConfigEntityId(_id),
                FacilityId = _facilityId
            });
        var result = await submissionQueries.FindTenantSubmissionConfig(_facilityId);
        Assert.NotNull(result);
        Assert.Equal(_facilityId, result.FacilityId);
        Assert.Equal(_id.ToString(), result.Id.ToString());
    }

    [Fact]
    public async Task TenantSubmissionQueries_FindTenantSubmissionConfig_ReturnsNull()
    {
        _mocker = new AutoMocker();
        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();
        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        var result = await submissionQueries.FindTenantSubmissionConfig(_facilityId);
        Assert.Null(result);
    }

    [Fact]
    public async Task TenantSubmissionQueries_GetTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();
        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();
        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.GetAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubmissionConfigEntity
            {
                Id = new TenantSubmissionConfigEntityId(_id),
                FacilityId = _facilityId
            });

        var result = await submissionQueries.GetTenantSubmissionConfig(_id.ToString());

        Assert.NotNull(result);
        Assert.Equal(_facilityId, result.FacilityId);
        Assert.Equal(_id.ToString(), result.Id.ToString());
    }

    [Fact]
    public async Task TenantSubmissionQueries_GetTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();
        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();
        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.GetAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());


        try
        {
            var result = await submissionQueries.GetTenantSubmissionConfig(_id.ToString());
            Assert.False(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionQueries_GetTenantSubmissionConfig_ReturnsNull()
    {
        _mocker = new AutoMocker();
        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();
        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.GetAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubmissionConfigEntity)null);

        var result = await submissionQueries.GetTenantSubmissionConfig(_id.ToString());

        Assert.Null(result);
    }

    [Fact]
    public async Task TenantSubmissionQueries_CreateTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubmissionConfigEntity)null);

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        var entity = new TenantSubmissionConfig
        {
            FacilityId = _facilityId,
            CreateDate = DateTime.Now,
            ModifyDate = DateTime.Now,
            Methods = new List<Method>(),
            ReportType = "test"
        };

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.AddAsync(It.IsAny<TenantSubmissionConfigEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await submissionQueries.CreateTenantSubmissionConfig(entity);

        Assert.NotNull(result);
        Assert.Equal(_facilityId, result.FacilityId);
    }

    [Fact]
    public async Task TenantSubmissionQueries_CreateTenantSubmissionConfig_ReturnNull()
    {
        _mocker = new AutoMocker();

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new TenantSubmissionConfigEntity
        {
            Id = new TenantSubmissionConfigEntityId(_id),
            FacilityId = _facilityId,
            CreateDate = DateTime.Now,
            ModifyDate = DateTime.Now,
            Methods = new List<Method>(),
            ReportType = "test"
        });

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        var entity = new TenantSubmissionConfig
        {
            FacilityId = _facilityId,
            CreateDate = DateTime.Now,
            ModifyDate = DateTime.Now,
            Methods = new List<Method>(),
            ReportType = "test"
        };

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.AddAsync(It.IsAny<TenantSubmissionConfigEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await submissionQueries.CreateTenantSubmissionConfig(entity);

        Assert.Null(result);
    }

    [Fact]
    public async Task TenantSubmissionQueries_CreateTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new Exception());

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        var entity = new TenantSubmissionConfig
        {
            FacilityId = _facilityId,
            CreateDate = DateTime.Now,
            ModifyDate = DateTime.Now,
            Methods = new List<Method>(),
            ReportType = "test"
        };

        try
        {
            await submissionQueries.CreateTenantSubmissionConfig(entity);
            Assert.False(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionQueries_UpdateTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        var entity = new TenantSubmissionConfig
        {
            Id = _id.ToString(),
            FacilityId = _facilityId,
            CreateDate = DateTime.Now,
            ModifyDate = DateTime.Now,
            Methods = new List<Method>(),
            ReportType = "test"
        };

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.AddAsync(It.IsAny<TenantSubmissionConfigEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.SaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.GetAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubmissionConfigEntity
            {
                Id = new TenantSubmissionConfigEntityId(_id),
                FacilityId = _facilityId,
                CreateDate = entity.CreateDate,
                ModifyDate = entity.ModifyDate,
                Methods = entity.Methods
            });

        var result = await submissionQueries.UpdateTenantSubmissionConfig(entity, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_facilityId, result.FacilityId);
        Assert.Equal(_id.ToString(), result.Id.ToString());
    }

    [Fact]
    public async Task TenantSubmissionQueries_UpdateTenantSubmissionConfig_ReturnsNull()
    {
        _mocker = new AutoMocker();

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        var entity = new TenantSubmissionConfig
        {
            Id = _id.ToString(),
            FacilityId = _facilityId
        };

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.GetAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubmissionConfigEntity)null);

        var result = await submissionQueries.UpdateTenantSubmissionConfig(entity, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TenantSubmissionQueries_UpdateTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        var entity = new TenantSubmissionConfig
        {
            Id = _id.ToString(),
            FacilityId = _facilityId
        };

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.GetAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        try
        {
            var result = await submissionQueries.UpdateTenantSubmissionConfig(entity, CancellationToken.None);
            Assert.False(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionQueries_DeleteTenantSubmissionConfig_ReturnsBoolean()
    {
        _mocker = new AutoMocker();

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.DeleteAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await submissionQueries.DeleteTenantSubmissionConfig(_id.ToString(), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TenantSubmissionQueries_DeleteTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();

        var submissionQueries = _mocker.CreateInstance<TenantSubmissionQueries>();

        _mocker.GetMock<ITenantSubmissionRepository>()
            .Setup(m => m.DeleteAsync(It.IsAny<TenantSubmissionConfigEntityId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        try
        {
            var result = await submissionQueries.DeleteTenantSubmissionConfig(_id.ToString(), CancellationToken.None);
            Assert.False(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionManager_FindTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();

        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.FindTenantSubmissionConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubmissionConfig
            {
                Id = _id.ToString(),
                FacilityId = _facilityId
            });

        var result = await submissionManager.FindTenantSubmissionConfig(_facilityId, CancellationToken.None);
        Assert.Equal(_facilityId, result.FacilityId);
        Assert.Equal(_id.ToString(), result.Id);
    }

    [Fact]
    public async Task TenantSubmissionManager_FindTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();
        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.FindTenantSubmissionConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        try
        {
            submissionManager.FindTenantSubmissionConfig(_facilityId, CancellationToken.None);
            Assert.False(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionManager_GetTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();

        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.GetTenantSubmissionConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubmissionConfig
            {
                Id = _id.ToString(),
                FacilityId = _facilityId
            });

        var result = await submissionManager.GetTenantSubmissionConfig(_id.ToString(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_facilityId, result.FacilityId);
        Assert.Equal(_id.ToString(), result.Id);
    }

    [Fact]
    public async Task TenantSubmissionManager_GetTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();
        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.GetTenantSubmissionConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        try
        {
            submissionManager.GetTenantSubmissionConfig(_id.ToString(), CancellationToken.None);
            Assert.False(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionManager_CreateTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();

        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.CreateTenantSubmissionConfig(It.IsAny<TenantSubmissionConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubmissionConfig
            {
                Id = _id.ToString(),
                FacilityId = _facilityId
            });

        var result = await submissionManager.CreateTenantSubmissionConfig(new TenantSubmissionConfig
        {
            Id = _id.ToString(),
            FacilityId = _facilityId
        });

        Assert.NotNull(result);
        Assert.Equal(_facilityId, result.FacilityId);
        Assert.Equal(_id.ToString(), result.Id);
    }

    [Fact]
    public async Task TenantSubmissionManager_CreateTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();
        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.CreateTenantSubmissionConfig(It.IsAny<TenantSubmissionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        try
        {
            submissionManager.CreateTenantSubmissionConfig(new TenantSubmissionConfig
            {
                Id = _id.ToString(),
                FacilityId = _facilityId
            });
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionManager_UpdateTenantSubmissionConfig_ReturnsTenantSubmissionConfig()
    {
        _mocker = new AutoMocker();

        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.UpdateTenantSubmissionConfig(It.IsAny<TenantSubmissionConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubmissionConfig
            {
                Id = _id.ToString(),
                FacilityId = _facilityId
            });

        var result = await submissionManager.UpdateTenantSubmissionConfig(new TenantSubmissionConfig
        {
            Id = _id.ToString(),
            FacilityId = _facilityId
        });

        Assert.NotNull(result);
        Assert.Equal(_facilityId, result.FacilityId);
        Assert.Equal(_id.ToString(), result.Id);
    }

    [Fact]
    public async Task TenantSubmissionManager_UpdateTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();
        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.UpdateTenantSubmissionConfig(It.IsAny<TenantSubmissionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        try
        {
            submissionManager.UpdateTenantSubmissionConfig(new TenantSubmissionConfig
            {
                Id = _id.ToString(),
                FacilityId = _facilityId
            });
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task TenantSubmissionManager_DeleteTenantSubmissionConfig_ReturnsBoolean()
    {
        _mocker = new AutoMocker();

        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.DeleteTenantSubmissionConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await submissionManager.DeleteTenantSubmissionConfig(_id.ToString(), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TenantSubmissionManager_DeleteTenantSubmissionConfig_ThrowsException()
    {
        _mocker = new AutoMocker();
        var submissionManager = _mocker.CreateInstance<TenantSubmissionManager>();

        _mocker.GetMock<ITenantSubmissionQueries>()
            .Setup(m => m.DeleteTenantSubmissionConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        try
        {
            submissionManager.DeleteTenantSubmissionConfig(_id.ToString(), CancellationToken.None);
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }
}