using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Infra.Mapper;


internal class ClientMapper : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Client")
            .HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .HasColumnName("Id");

        builder.Property(x => x.Name)
            .HasColumnName("Name").IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("Role")
            .HasDefaultValue(Roles.Client);

        builder.Property(x => x.Plan)
            .HasColumnName("Plan");
        
        builder.Property(x => x.Email).HasColumnName("Email").IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("PasswordHash");
        builder.Property(x => x.FirebaseUid).HasColumnName("FirebaseUid").HasMaxLength(256);
        builder.Property(x => x.AuthProvider).HasColumnName("AuthProvider").HasMaxLength(64);
        builder.Property(x => x.StripeCustomerId).HasColumnName("StripeCustomerId").HasMaxLength(128);
        builder.Property(x => x.StripeSubscriptionId).HasColumnName("StripeSubscriptionId").HasMaxLength(128);
        builder.Property(x => x.StripePriceId).HasColumnName("StripePriceId").HasMaxLength(128);
        builder.Property(x => x.StripeSubscriptionStatus).HasColumnName("StripeSubscriptionStatus").HasMaxLength(64);
        builder.Property(x => x.StripePendingPaymentIntentId).HasColumnName("StripePendingPaymentIntentId").HasMaxLength(128);
        builder.Property(x => x.StripePendingPaymentIntentCreatedAtUtc).HasColumnName("StripePendingPaymentIntentCreatedAtUtc");
        builder.Property(x => x.StripeHostedInvoiceUrl).HasColumnName("StripeHostedInvoiceUrl").HasMaxLength(2048);

        builder.HasIndex(x => x.Email)
            .HasDatabaseName("Index_AuthEmail")
            .IsUnique();

        builder.HasIndex(x => x.FirebaseUid)
            .HasDatabaseName("Index_FirebaseUid")
            .IsUnique()
            .HasFilter("\"FirebaseUid\" IS NOT NULL");

        builder.HasIndex(x => x.StripeCustomerId)
            .HasDatabaseName("Index_StripeCustomerId")
            .HasFilter("\"StripeCustomerId\" IS NOT NULL");

        builder.Property(x => x.IsActive)
            .HasDefaultValue(false)
            .HasColumnName("IsActive").IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt").IsRequired();

        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
    }
}


