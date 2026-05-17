using System.Data.Entity.ModelConfiguration;
using BeastPc.Models.Tables;

namespace BeastPc.Models.Maps
{
    public class OrderMap : EntityTypeConfiguration<Order>
    {
        public OrderMap()
        {
            ToTable("orders");
            HasKey(x => x.Id);

            Property(x => x.Id).HasColumnName("id");
            Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(10, 2).IsRequired();
            Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        }
    }
}
