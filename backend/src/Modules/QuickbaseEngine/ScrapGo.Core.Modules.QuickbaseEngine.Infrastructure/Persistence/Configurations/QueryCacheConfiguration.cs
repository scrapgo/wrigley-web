using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Persistence.Configurations;

public sealed class QueryCacheConfiguration : IEntityTypeConfiguration<QueryCache>
{
    public void Configure(EntityTypeBuilder<QueryCache> builder)
    {
        builder.ToTable("query_caches");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.QueryHash)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(q => q.TableId)
            .HasColumnType("text")
            .IsRequired();

        // jsonb: validated and queryable (e.g. inspecting cached queries per
        // table). A cache hit returns semantically identical JSON, but
        // normalized, so not byte-identical to Quickbase's original body.
        builder.Property(q => q.RequestJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(q => q.ResponseJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(q => q.FetchedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(q => q.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // The cache key, and the ON CONFLICT arbiter for QueryCacheStore's upsert.
        builder.HasIndex(q => q.QueryHash)
            .IsUnique()
            .HasDatabaseName(QueryCacheStore.QueryHashConstraint);

        // For purging or invalidating every cached query on one table.
        builder.HasIndex(q => q.TableId)
            .HasDatabaseName("ix_query_caches_table_id");

        builder.HasIndex(q => q.FetchedAt)
            .HasDatabaseName("ix_query_caches_fetched_at");
    }
}
