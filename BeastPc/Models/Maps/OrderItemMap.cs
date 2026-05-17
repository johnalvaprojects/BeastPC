using System.Data.Entity.ModelConfiguration;
using BeastPc.Models.Tables;

namespace BeastPc.Models.Maps
{
    public class OrderItemMap : EntityTypeConfiguration<OrderItem>
    {
        public OrderItemMap()
        {
            ToTable("order_items");
            HasKey(x => x.Id);

            Property(x => x.Id).HasColumnName("id");
            Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
            Property(x => x.PcBuildId).HasColumnName("build_id").IsRequired();
            Property(x => x.Qty).HasColumnName("quantity").IsRequired();
            Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(10, 2).IsRequired();
        }
    }
}
