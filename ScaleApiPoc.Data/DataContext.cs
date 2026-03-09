using Microsoft.EntityFrameworkCore;

namespace ScaleApiPoc.Data;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options)
        : base(options)
    {
    }

    public DbSet<MyPhrase> MyPhrases => Set<MyPhrase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MyPhrase>(entity =>
        {
            entity.ToTable("my_phrases");
            entity.HasKey(e => e.id);
            entity.Property(e => e.name).IsRequired();
        });
    }
}
