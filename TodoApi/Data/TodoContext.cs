using Microsoft.EntityFrameworkCore;
using TodoApi.Models;   

namespace TodoApi.Data;

public class TodoContext : DbContext
{
    public TodoContext(DbContextOptions<TodoContext> options) : base(options) { }


    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TodoList configuration
        modelBuilder.Entity<TodoList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.HasIndex(e => e.UpdatedAt);
            
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.TodoList)
                  .HasForeignKey(e => e.TodoListId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TodoItem configuration
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.IsCompleted).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.TodoListId).IsRequired();
            
            entity.HasIndex(e => e.TodoListId);
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => e.IsCompleted);
        });

        // SyncState configuration
        modelBuilder.Entity<SyncState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastSyncedAt).IsRequired();
            
            // Create unique indexes for lookups
            entity.HasIndex(e => new { e.EntityType, e.LocalTodoListId })
                  .IsUnique()
                  .HasFilter("[LocalTodoListId] IS NOT NULL");
                  
            entity.HasIndex(e => new { e.EntityType, e.ExternalTodoListId })
                  .IsUnique()
                  .HasFilter("[ExternalTodoListId] IS NOT NULL");
                  
            entity.HasIndex(e => new { e.EntityType, e.LocalTodoItemId })
                  .IsUnique()
                  .HasFilter("[LocalTodoItemId] IS NOT NULL");
                  
            entity.HasIndex(e => new { e.EntityType, e.ExternalTodoItemId })
                  .IsUnique()
                  .HasFilter("[ExternalTodoItemId] IS NOT NULL");
        });

        // SyncLog configuration
        modelBuilder.Entity<SyncLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Success).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.RetryCount).IsRequired();
            
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.Success, e.Timestamp });
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });
    }
}
