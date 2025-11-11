using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TodoApi.Data;
using TodoApi.Models;
using TodoApi.Services.Sync;
using TodoApi.Services.ExternalApi;
using TodoApi.Dtos.ExternalApi;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace TodoApi.Tests.Services.Sync
{
    public class SyncServiceTests : IDisposable
    {
        private readonly TodoContext _context;
        private readonly Mock<IExternalTodoApiClient> _mockExternalApi;
        private readonly Mock<ILogger<SyncService>> _mockLogger;
        private readonly SyncService _sut;

        public SyncServiceTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new TodoContext(options);
            _mockExternalApi = new Mock<IExternalTodoApiClient>();
            _mockLogger = new Mock<ILogger<SyncService>>();
            _sut = new SyncService(_context, _mockExternalApi.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SyncAllAsync_WhenExternalHasNewList_CreatesLocalCopy()
        {
            // Arrange
            var externalLists = new List<ExternalTodoListDto>
            {
                new ExternalTodoListDto
                {
                    Id = 100,
                    Name = "External List",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            _mockExternalApi
                .Setup(x => x.GetTodoListsAsync())
                .ReturnsAsync(externalLists);

            _mockExternalApi
                .Setup(x => x.GetTodoItemsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ExternalTodoItemDto>());

            // Act
            var result = await _sut.SyncAllAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.EntitiesSynced.Should().BeGreaterThan(0);

            var localLists = await _context.TodoLists.ToListAsync();
            localLists.Should().HaveCount(1);
            localLists[0].Name.Should().Be("External List");

            var syncState = await _context.Set<SyncState>()
                .FirstOrDefaultAsync(s => s.ExternalTodoListId == 100);
            syncState.Should().NotBeNull();
            syncState!.LocalTodoListId.Should().NotBeNull();
        }

        [Fact]
        public async Task SyncAllAsync_WhenLocalHasNewList_PushesToExternal()
        {
            // Arrange
            var localList = new TodoList
            {
                Name = "Local List",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.TodoLists.Add(localList);
            await _context.SaveChangesAsync();

            var createdExternalList = new ExternalTodoListDto
            {
                Id = 200,
                Name = "Local List",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockExternalApi
                .Setup(x => x.GetTodoListsAsync())
                .ReturnsAsync(new List<ExternalTodoListDto>());

            _mockExternalApi
                .Setup(x => x.CreateTodoListAsync(It.IsAny<CreateExternalTodoListDto>()))
                .ReturnsAsync(createdExternalList);

            _mockExternalApi
                .Setup(x => x.GetTodoItemsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ExternalTodoItemDto>());

            // Act
            var result = await _sut.SyncAllAsync();

            // Assert
            result.Success.Should().BeTrue();
            
            _mockExternalApi.Verify(
                x => x.CreateTodoListAsync(It.Is<CreateExternalTodoListDto>(
                    dto => dto.Name == "Local List"
                )),
                Times.Once
            );

            var syncState = await _context.Set<SyncState>()
                .FirstOrDefaultAsync(s => s.LocalTodoListId == localList.Id);
            syncState.Should().NotBeNull();
            syncState!.ExternalTodoListId.Should().Be(200);
        }

        [Fact]
        public async Task SyncAllAsync_WhenExternalIsNewer_UpdatesLocal()
        {
            // Arrange
            var localList = new TodoList
            {
                Name = "Old Name",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            };
            _context.TodoLists.Add(localList);
            await _context.SaveChangesAsync();

            var syncState = new SyncState
            {
                EntityType = "TodoList",
                LocalTodoListId = localList.Id,
                ExternalTodoListId = 300,
                LastSyncedAt = DateTime.UtcNow.AddDays(-1)
            };
            _context.Set<SyncState>().Add(syncState);
            await _context.SaveChangesAsync();

            var externalLists = new List<ExternalTodoListDto>
            {
                new ExternalTodoListDto
                {
                    Id = 300,
                    Name = "Updated Name",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    UpdatedAt = DateTime.UtcNow // Newer than sync state
                }
            };

            _mockExternalApi
                .Setup(x => x.GetTodoListsAsync())
                .ReturnsAsync(externalLists);

            _mockExternalApi
                .Setup(x => x.GetTodoItemsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ExternalTodoItemDto>());

            // Act
            var result = await _sut.SyncAllAsync();

            // Assert
            result.Success.Should().BeTrue();

            var updatedList = await _context.TodoLists.FindAsync(localList.Id);
            updatedList!.Name.Should().Be("Updated Name");
        }

        [Fact]
        public async Task SyncAllAsync_WhenLocalIsNewer_PushesToExternal()
        {
            // Arrange
            var localList = new TodoList
            {
                Name = "Updated Locally",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow // Just updated
            };
            _context.TodoLists.Add(localList);
            await _context.SaveChangesAsync();

            var syncState = new SyncState
            {
                EntityType = "TodoList",
                LocalTodoListId = localList.Id,
                ExternalTodoListId = 400,
                LastSyncedAt = DateTime.UtcNow.AddDays(-1) // Older than local update
            };
            _context.Set<SyncState>().Add(syncState);
            await _context.SaveChangesAsync();

            _mockExternalApi
                .Setup(x => x.GetTodoListsAsync())
                .ReturnsAsync(new List<ExternalTodoListDto>());

            _mockExternalApi
                .Setup(x => x.UpdateTodoListAsync(400, It.IsAny<UpdateExternalTodoListDto>()))
                .Returns(Task.CompletedTask);

            _mockExternalApi
                .Setup(x => x.GetTodoItemsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ExternalTodoItemDto>());

            // Act
            var result = await _sut.SyncAllAsync();

            // Assert
            result.Success.Should().BeTrue();

            _mockExternalApi.Verify(
                x => x.UpdateTodoListAsync(400, It.Is<UpdateExternalTodoListDto>(
                    dto => dto.Name == "Updated Locally"
                )),
                Times.Once
            );
        }

        [Fact]
        public async Task SyncAllAsync_WhenExternalApiFails_LogsErrorAndContinues()
        {
            // Arrange
            var localList = new TodoList
            {
                Name = "Local List",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.TodoLists.Add(localList);
            await _context.SaveChangesAsync();

            _mockExternalApi
                .Setup(x => x.GetTodoListsAsync())
                .ReturnsAsync(new List<ExternalTodoListDto>());

            _mockExternalApi
                .Setup(x => x.CreateTodoListAsync(It.IsAny<CreateExternalTodoListDto>()))
                .ThrowsAsync(new HttpRequestException("API is down"));

            _mockExternalApi
                .Setup(x => x.GetTodoItemsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ExternalTodoItemDto>());

            // Act
            var result = await _sut.SyncAllAsync();

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorCount.Should().BeGreaterThan(0);
            result.Errors.Should().Contain(e => e.Contains("API is down"));

            var syncLog = await _context.Set<SyncLog>()
                .FirstOrDefaultAsync(l => l.EntityId == localList.Id);
            syncLog.Should().NotBeNull();
            syncLog!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task SyncAllAsync_SyncsTodoItemsForMappedLists()
        {
            // Arrange
            var localList = new TodoList
            {
                Name = "List with Items",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.TodoLists.Add(localList);
            await _context.SaveChangesAsync();

            var syncState = new SyncState
            {
                EntityType = "TodoList",
                LocalTodoListId = localList.Id,
                ExternalTodoListId = 500,
                LastSyncedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            _context.Set<SyncState>().Add(syncState);
            await _context.SaveChangesAsync();

            var externalItems = new List<ExternalTodoItemDto>
            {
                new ExternalTodoItemDto
                {
                    Id = 1001,
                    Title = "External Item",
                    Description = "From external API",
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _mockExternalApi
                .Setup(x => x.GetTodoListsAsync())
                .ReturnsAsync(new List<ExternalTodoListDto>());

            _mockExternalApi
                .Setup(x => x.GetTodoItemsAsync(500))
                .ReturnsAsync(externalItems);

            // Act
            var result = await _sut.SyncAllAsync();

            // Assert
            result.Success.Should().BeTrue();

            var localItems = await _context.TodoItems
                .Where(i => i.TodoListId == localList.Id)
                .ToListAsync();
            
            localItems.Should().HaveCount(1);
            localItems[0].Title.Should().Be("External Item");

            var itemSyncState = await _context.Set<SyncState>()
                .FirstOrDefaultAsync(s => s.ExternalTodoItemId == 1001);
            itemSyncState.Should().NotBeNull();
        }

        [Fact]
        public async Task GetSyncStatusAsync_ReturnsCorrectStatus()
        {
            // Arrange
            var successLog = new SyncLog
            {
                EntityType = "TodoList",
                EntityId = 1,
                Operation = "Create",
                Success = true,
                Timestamp = DateTime.UtcNow.AddMinutes(-10)
            };

            var errorLog = new SyncLog
            {
                EntityType = "TodoItem",
                EntityId = 2,
                Operation = "Update",
                Success = false,
                ErrorMessage = "Connection failed",
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                RetryCount = 2
            };

            _context.Set<SyncLog>().AddRange(successLog, errorLog);
            await _context.SaveChangesAsync();

            // Act
            var status = await _sut.GetSyncStatusAsync();

            // Assert
            status.Should().NotBeNull();
            status.LastSuccessfulSync.Should().BeCloseTo(successLog.Timestamp, TimeSpan.FromSeconds(1));
            status.FailedSyncCount.Should().BeGreaterThan(0);
            status.RecentErrors.Should().NotBeEmpty();
            status.RecentErrors[0].Message.Should().Be("Connection failed");
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }

    // Integration Test Example
    public class SyncIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public SyncIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task TriggerSync_ReturnsAccepted()
        {
            // Act
            var response = await _client.PostAsync("/api/sync/trigger", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("jobId");
        }

        [Fact]
        public async Task GetSyncStatus_ReturnsStatus()
        {
            // Act
            var response = await _client.GetAsync("/api/sync/status");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var status = await response.Content.ReadFromJsonAsync<SyncStatus>();
            status.Should().NotBeNull();
        }
    }
}