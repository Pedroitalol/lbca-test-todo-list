using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Infra.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(t => t.Description)
               .HasMaxLength(500);

        // Unique index: enforces DB-level uniqueness.
        // Prevents race conditions between concurrent imports/creates
        // that pass the in-memory check but would insert the same Title.
        builder.HasIndex(t => t.Title)
               .IsUnique()
               .HasDatabaseName("IX_TaskItems_Title_Unique");

        builder.Property(t => t.Status)
               .HasConversion<string>();

        builder.Property(t => t.Priority)
               .HasConversion<string>();
    }
}
