using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ircica.Entities;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Bot> Bots { get; set; } = null!;
    public DbSet<Channel> Channels { get; set; } = null!;
    public DbSet<Release> Releases { get; set; } = null!;
    public DbSet<Server> Servers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Bot>(e =>
        {
            e.HasKey(p => p.BotId);
            e.HasMany(p => p.Releases).WithOne(p => p.Bot!);
        });

        builder.Entity<Channel>(e =>
        {
            e.HasKey(p => p.ChannelId);
            e.HasMany(p => p.Releases).WithOne(p => p.Channel!);
        });

        builder.Entity<Release>(e =>
        {
            e.HasKey(p => p.ReleaseId);
        });

        builder.Entity<Server>(e =>
        {
            e.HasKey(p => p.ServerId);
            e.HasMany(p => p.Releases).WithOne(p => p.Server!);
        });

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var dtProperties = entityType.ClrType.GetProperties()
                .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));

            foreach (var property in dtProperties)
                builder
                    .Entity(entityType.Name)
                    .Property(property.Name)
                    .HasConversion(new DateTimeToBinaryConverter());

            var decProperties = entityType.ClrType.GetProperties()
                .Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?));

            foreach (var property in decProperties)
                builder
                    .Entity(entityType.Name)
                    .Property(property.Name)
                    .HasConversion<double>();
        }
    }
}