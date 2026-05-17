using System.Data.Entity.ModelConfiguration;
using BeastPc.Models.Tables;

namespace BeastPc.Models.Maps
{
    public class PcBuildMap : EntityTypeConfiguration<PcBuild>
    {
        public PcBuildMap()
        {
            ToTable("pc_builds");
            HasKey(x => x.Id);

            Property(x => x.Id).HasColumnName("id");
            Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            Property(x => x.Description).HasColumnName("description").IsOptional();
            Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2).IsRequired();
            Property(x => x.Cpu).HasColumnName("cpu").HasMaxLength(120).IsOptional();
            Property(x => x.Gpu).HasColumnName("gpu").HasMaxLength(120).IsOptional();
            Property(x => x.Ram).HasColumnName("ram").HasMaxLength(80).IsOptional();
            Property(x => x.Storage).HasColumnName("storage").HasMaxLength(120).IsOptional();
            Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(255).IsOptional();
            Property(x => x.Stock).HasColumnName("stock").IsRequired();
            Property(x => x.Active).HasColumnName("is_active").IsRequired();
            Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        }
    }
}
