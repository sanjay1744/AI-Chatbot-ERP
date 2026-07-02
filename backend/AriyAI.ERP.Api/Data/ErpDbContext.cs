using Microsoft.EntityFrameworkCore;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Data
{
    public class ErpDbContext : DbContext
    {
        public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Agent> Agents => Set<Agent>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<SalesEnquiry> SalesEnquiries => Set<SalesEnquiry>();
        public DbSet<EnquiryProduct> EnquiryProducts => Set<EnquiryProduct>();
        public DbSet<Quotation> Quotations => Set<Quotation>();
        public DbSet<QuotationProduct> QuotationProducts => Set<QuotationProduct>();
        public DbSet<SalesRecord> SalesRecords => Set<SalesRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure cascade paths if needed
            modelBuilder.Entity<SalesEnquiry>()
                .HasMany(e => e.EnquiryProducts)
                .WithOne()
                .HasForeignKey(p => p.SalesEnquiryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Quotation>()
                .HasMany(q => q.QuotationProducts)
                .WithOne()
                .HasForeignKey(p => p.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relate AssignedAgent mapping
            modelBuilder.Entity<SalesEnquiry>()
                .HasOne(e => e.AssignedAgent)
                .WithMany()
                .HasForeignKey(e => e.AssignToId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
