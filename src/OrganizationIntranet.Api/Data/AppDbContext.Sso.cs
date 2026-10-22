using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public partial class AppDbContext
{
    public DbSet<SsoTicket> SsoTickets => Set<SsoTicket>();

    private static void ConfigureSso(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SsoTicket>(e =>
        {
            e.ToTable("SsoTicket", "Sec", t => t.HasCheckConstraint("CK_SsoTicket_Lifetime", "[ExpiresAt] > [CreatedAt]"));
            e.HasKey(x => x.SsoTicketId);
            e.Property(x => x.TicketHash).HasMaxLength(32).IsFixedLength().IsRequired();
            e.HasIndex(x => x.TicketHash).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
