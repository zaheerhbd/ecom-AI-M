using Agent.Evals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    /// <summary>Maps an eval run to the EvalRuns table (see docs/agentic/04 §3).</summary>
    public class EvalRunConfiguration : IEntityTypeConfiguration<EvalRunRecord>
    {
        public void Configure(EntityTypeBuilder<EvalRunRecord> builder)
        {
            builder.ToTable("EvalRuns");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Status).IsRequired().HasMaxLength(20);
            builder.Property(r => r.Error).HasMaxLength(200);
            builder.Property(r => r.Model).IsRequired().HasMaxLength(50);
            builder.Property(r => r.TotalCostUsd).HasColumnType("numeric(10,6)");

            builder.HasIndex(r => r.StartedAt); // the evals list is newest first
        }
    }

    /// <summary>Maps one case result to the EvalCaseResults table. Deleting an eval run deletes its results.</summary>
    public class EvalCaseResultConfiguration : IEntityTypeConfiguration<EvalCaseResultRecord>
    {
        public void Configure(EntityTypeBuilder<EvalCaseResultRecord> builder)
        {
            builder.ToTable("EvalCaseResults");
            builder.HasKey(r => r.Id); // int key: the database numbers the rows

            builder.Property(r => r.CaseId).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Category).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Input).IsRequired();
            builder.Property(r => r.Answer).IsRequired();
            builder.Property(r => r.Checks).HasJsonColumn();

            // No navigation properties on the records, so the link is set up by type.
            builder.HasOne<EvalRunRecord>()
                .WithMany()
                .HasForeignKey(r => r.EvalRunId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
